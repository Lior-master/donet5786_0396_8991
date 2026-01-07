using BO;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Helpers;

internal static class Tools
{
    public static string ToStringProperty<T>(this T t)
    {
        if (t is null)
            return string.Empty;

        var type = typeof(T);
        var props = type.GetProperties();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(type.Name + " {");

        foreach (var prop in props)
        {
            var value = prop.GetValue(t);
            sb.AppendLine($"  {prop.Name} = {value}");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    public static double BirdDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;

        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static BO.OrderStatus CalculateOrderStatus(List<DO.Delivery> deliveries)
    {
        if (deliveries == null || deliveries.Count == 0)
            return BO.OrderStatus.Pending;

        var last = deliveries.OrderByDescending(d => d.PickupTime).First();

        return last.Status switch
        {
            DO.OrderStatus.Pending => BO.OrderStatus.Pending,
            DO.OrderStatus.Processing => BO.OrderStatus.Processing,
            DO.OrderStatus.Delivered => BO.OrderStatus.Delivered,
            DO.OrderStatus.Canceled => BO.OrderStatus.Canceled,
            DO.OrderStatus.Returned => BO.OrderStatus.Returned,
            _ => BO.OrderStatus.Pending
        };
    }

    public static async Task<double> CalculateRouteDistanceAsync(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        // Reuse the same key
        string apiKey = LocationIqKey;

        const string baseUrl = "https://us1.locationiq.com/v1/directions/driving";

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetDeliveryProject/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        string url =
            $"{baseUrl}/{lon1.ToString(CultureInfo.InvariantCulture)},{lat1.ToString(CultureInfo.InvariantCulture)};" +
            $"{lon2.ToString(CultureInfo.InvariantCulture)},{lat2.ToString(CultureInfo.InvariantCulture)}" +
            $"?key={Uri.EscapeDataString(apiKey)}" +
            $"&overview=false&alternatives=false";

        System.Diagnostics.Debug.WriteLine($"LocationIQ routing request: {url}");

        HttpResponseMessage response;

        try
        {
            response = await client.GetAsync(url).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LocationIQ routing GetAsync FAILED:");
            System.Diagnostics.Debug.WriteLine(ex.ToString());
            throw new BLFailedOperation($"Network/TLS error during routing: {ex.Message}", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new BLFailedOperation("LocationIQ routing rate-limited (HTTP 429)");

            if (!response.IsSuccessStatusCode)
            {
                string err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new BLFailedOperation(
                    $"Routing failed with status {response.StatusCode}: {err}");
            }

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var routes = doc.RootElement.GetProperty("routes");
            if (routes.GetArrayLength() == 0)
                throw new BLNotFoundException("No route found between the two points");

            double meters = routes[0].GetProperty("distance").GetDouble();
            return meters / 1000.0;
        }
    }

    public static double GetSpeed(DO.DeliveryTransport transport, BO.Config config)
    {
        return transport switch
        {
            DO.DeliveryTransport.Car => config.CarSpeed,
            DO.DeliveryTransport.Motorcycle => config.MotorcycleSpeed,
            DO.DeliveryTransport.Bike => config.BikeSpeed,
            DO.DeliveryTransport.Foot => config.WalkingSpeed,
            _ => config.CarSpeed
        };
    }

    public static DO.Courier UpdateCourierActivity(DO.Courier courier, TimeSpan inactivityThreshold)
    {
        // Use central app clock (AdminManager.Now) instead of DateTime.Now so simulator / tests are consistent.
        if (AdminManager.Now - courier.StartDate > inactivityThreshold)
            return courier with { IsActive = false };

        return courier;
    }

    public static bool IsDeliveryOnTime(DO.Delivery d, DateTime expectedTime)
    {
        // If ArrivalTime is null, delivery is not completed yet, hence not on time
        if (d.ArrivalTime == null)
            return false;

        return d.ArrivalTime <= expectedTime;
    }

    // =========================
    // LocationIQ Geocoding
    // =========================

    private static readonly string LocationIqKey = "pk.e8d2b136630548a5295a8c88b56c1b82";

    private const string LocationIqSearchEndpoint = "https://us1.locationiq.com/v1/search";

    private static readonly HttpClient s_geoClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly SemaphoreSlim s_geoRateGate = new SemaphoreSlim(1, 1);
    private static DateTime s_lastGeoRequestUtc = DateTime.MinValue;
    private static readonly TimeSpan s_minGeoInterval = TimeSpan.FromMilliseconds(550);

    private static readonly ConcurrentDictionary<string, (double lat, double lon)> s_geoCache = new();

    static Tools()
    {
        // IMPORTANT for many WPF/.NET Framework environments: enforce TLS 1.2
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        s_geoClient.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetDeliveryProject/1.0");
        s_geoClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>
    /// Converts a textual address into geographic coordinates (Latitude, Longitude)
    /// using LocationIQ Forward Geocoding API (API key required).
    /// The key is read from the environment variable "LOCATIONIQ_KEY".
    /// </summary>
    public static async Task<(double Latitude, double Longitude)> GetCoordinatesFromAddressAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new BLInvalidInputException("Address cannot be null or empty");

        try
        {
            string normalized = NormalizeAddress(address);

            if (s_geoCache.TryGetValue(normalized, out var cached))
                return (cached.lat, cached.lon);

            await EnforceGeoRateLimitAsync().ConfigureAwait(false);

            string url =
                $"{LocationIqSearchEndpoint}" +
                $"?key={Uri.EscapeDataString(LocationIqKey)}" +
                $"&q={Uri.EscapeDataString(address)}" +
                $"&countrycodes=il" +
                $"&format=json&limit=1";

            System.Diagnostics.Debug.WriteLine($"LocationIQ geocoding request: {url}");

            for (int attempt = 0; attempt < 3; attempt++)
            {
                HttpResponseMessage response;

                try
                {
                    response = await s_geoClient.GetAsync(url).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Full diagnostics (includes InnerException)
                    System.Diagnostics.Debug.WriteLine("LocationIQ geocoding GetAsync FAILED:");
                    System.Diagnostics.Debug.WriteLine(ex.ToString());

                    throw new BLFailedOperation($"Network/TLS error during geocoding: {ex.Message}", ex);
                }

                using (response)
                {
                    System.Diagnostics.Debug.WriteLine($"LocationIQ geocoding response status: {response.StatusCode}");

                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new BLFailedOperation(
                            $"Geocoding failed with status {response.StatusCode}: {errorContent}");
                    }

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"LocationIQ geocoding response: {json}");

                    if (string.IsNullOrWhiteSpace(json))
                        throw new BLNotFoundException("Empty response from geocoding service");

                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                        throw new BLNotFoundException($"Address not found: {address}");

                    var first = doc.RootElement[0];

                    string? latStr = first.GetProperty("lat").GetString();
                    string? lonStr = first.GetProperty("lon").GetString();

                    if (string.IsNullOrWhiteSpace(latStr) || string.IsNullOrWhiteSpace(lonStr))
                        throw new BLNotFoundException("Coordinates missing in geocoding result");

                    double lat = double.Parse(latStr, CultureInfo.InvariantCulture);
                    double lon = double.Parse(lonStr, CultureInfo.InvariantCulture);

                    s_geoCache[normalized] = (lat, lon);

                    System.Diagnostics.Debug.WriteLine($"LocationIQ geocoding successful: Lat={lat}, Lon={lon}");
                    return (lat, lon);
                }
            }

            throw new BLFailedOperation("LocationIQ geocoding was rate-limited (HTTP 429) after retries.");
        }
        catch (Exception ex) when (!(ex is BLNotFoundException || ex is BLInvalidInputException || ex is BLFailedOperation))
        {
            System.Diagnostics.Debug.WriteLine($"Unexpected exception in LocationIQ geocoding: {ex.GetType().Name}: {ex.Message}");
            throw new BLFailedOperation($"Unexpected error during geocoding: {ex.Message}", ex);
        }
    }

    private static async Task EnforceGeoRateLimitAsync()
    {
        await s_geoRateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            var elapsed = now - s_lastGeoRequestUtc;

            if (elapsed < s_minGeoInterval)
                await Task.Delay(s_minGeoInterval - elapsed).ConfigureAwait(false);

            s_lastGeoRequestUtc = DateTime.UtcNow;
        }
        finally
        {
            s_geoRateGate.Release();
        }
    }

    private static string NormalizeAddress(string a) =>
        string.Join(' ', a.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static DateTime CalculateEstimatedArrival(DateTime orderDate, double distanceKm, double speedKmH)
    {
        if (speedKmH <= 0)
            throw new ArgumentException("Speed must be positive.");

        double hours = distanceKm / speedKmH;
        return orderDate.AddHours(hours);
    }

    public static BO.ScheduleStatus CalculateScheduleStatus(
        BO.OrderStatus status,
        DateTime orderDate,
        DateTime? estimatedArrival,
        DateTime? maxArrival,
        DateTime? realArrival)
    {
        if (estimatedArrival == null || maxArrival == null)
            return BO.ScheduleStatus.OnTime;

        if (status == BO.OrderStatus.Delivered && realArrival != null)
            return realArrival <= estimatedArrival
                ? BO.ScheduleStatus.OnTime
                : BO.ScheduleStatus.Late;

        DateTime now = AdminManager.Now;

        if (now > maxArrival)
            return BO.ScheduleStatus.Late;

        if (now > estimatedArrival)
            return BO.ScheduleStatus.InRisk;

        return BO.ScheduleStatus.OnTime;
    }
}
