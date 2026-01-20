using BO;
using DalApi;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Helpers;

/// <summary>
/// Provides utility helper methods for the Business Logic layer.
/// Includes distance calculations, status transformations, geocoding operations, and scheduling utilities.
/// </summary>
internal static class Tools
{
    private static readonly IDal s_dal = Factory.Get;
    internal const string InvalidAddressMarker = "INVALID_ADDRESS";

    /// <summary>
    /// Converts an object to a formatted string representation of its public properties.
    /// </summary>
    /// <typeparam name="T">The type of the object to convert.</typeparam>
    /// <param name="t">The object instance to convert. If null, returns an empty string.</param>
    /// <returns>
    /// A formatted string showing the type name and all public properties with their values.
    /// Format: "TypeName { PropertyName = value, ... }".
    /// Returns an empty string if the input is null.
    /// </returns>
    /// <example>
    /// If <paramref name="t"/> is a Courier object with Id=1 and CourierName="John",
    /// returns: "Courier { Id = 1, CourierName = John, ... }".
    /// </example>
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

    /// <summary>
    /// Central ETA computation when distance is already known.
    /// Uses a consistent speed source (configuration) and a single formula: ETA = pickupTime + distance/speed.
    /// Falls back to a conservative default if inputs are invalid.
    /// </summary>
    public static DateTime EstimateArrival(DateTime pickupTime, DO.DeliveryTransport transport, double distanceKm)
    {
        var config = AdminManager.GetConfig();
        double speed = GetSpeed(transport, config);

        // Defensive fallback: if we can't compute a meaningful ETA, return a safe default
        if (distanceKm <= 0 || speed <= 0)
            return EstimateArrivalFallback(pickupTime);

        return pickupTime.AddHours(distanceKm / speed);
    }

    /// <summary>
    /// Safe fallback ETA used when distance is unknown or cannot be computed.
    /// Keeps behavior explicit and centralized (instead of scattered "AddMinutes(30)" in multiple places).
    /// </summary>
    public static DateTime EstimateArrivalFallback(DateTime pickupTime)
        => pickupTime.AddMinutes(30);

    /// <summary>
    /// Calculates the great-circle distance between two geographic points using the Haversine formula.
    /// This is the shortest distance over the earth's surface (ignoring elevation and roads).
    /// </summary>
    /// <param name="lat1">Latitude of the first point in degrees (-90 to 90).</param>
    /// <param name="lon1">Longitude of the first point in degrees (-180 to 180).</param>
    /// <param name="lat2">Latitude of the second point in degrees (-90 to 90).</param>
    /// <param name="lon2">Longitude of the second point in degrees (-180 to 180).</param>
    /// <returns>The straight-line distance between the two points in kilometers.</returns>
    /// <remarks>
    /// Uses Earth's mean radius of 6371 km.
    /// This method does not account for actual road networks; use <see cref="CalculateRouteDistanceAsync"/>
    /// for route-based distance calculations.
    /// </remarks>
    public static double BirdDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Earth's mean radius in kilometers
        const double R = 6371;
        
        // Convert latitude and longitude differences from degrees to radians
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;

        // Haversine formula: a = sin²(Δlat/2) + cos(lat1) * cos(lat2) * sin²(Δlon/2)
        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        // Distance = R * 2 * atan2(√a, √(1−a))
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>
    /// Determines the overall order status based on delivery records, prioritizing successful deliveries.
    /// </summary>
    /// <param name="deliveries">A list of delivery records associated with the order. Can be null or empty.</param>
    /// <returns>
    /// The status of the order based on delivery outcomes, with successful deliveries taking priority.
    /// If the list is null or empty, returns <see cref="BO.OrderStatus.Pending"/>.
    /// </returns>
    /// <remarks>
    /// Priority logic:
    /// 1. If any delivery is in progress (DeliveredStatus == null), return Processing
    /// 2. If any delivery was successful (DeliveredStatus == Delivered), return Delivered
    /// 3. Otherwise, use the status of the most recent completed delivery
    /// </remarks>
    public static BO.OrderStatus CalculateOrderStatus(List<DO.Delivery> deliveries)
    {
        // No deliveries => order not yet started
        if (deliveries == null || deliveries.Count == 0)
            return BO.OrderStatus.Pending;

        // If there exists at least one delivery that is not finished yet
        // (DeliveredStatus is the delivery END type; null = still in progress)
        if (deliveries.Any(d => d.DeliveredStatus == null))
            return BO.OrderStatus.Processing;

        // FIXED: Check if ANY delivery was successful - successful delivery trumps failed ones
        var successfulDelivery = deliveries
            .FirstOrDefault(d => d.DeliveredStatus == DO.DeliveredStatus.Delivered);

        // If we have a successful delivery, the order is delivered regardless of other failed attempts
        if (successfulDelivery != null)
            return BO.OrderStatus.Delivered;

        // No successful deliveries - check the most recent finished delivery
        var lastFinished = deliveries
            .Where(d => d.DeliveredStatus != null)
            .OrderByDescending(d => d.ArrivalTime)
            .FirstOrDefault();

        if (lastFinished == null)
            return BO.OrderStatus.Pending;

        return lastFinished.DeliveredStatus switch
        {
            DO.DeliveredStatus.Delivered => BO.OrderStatus.Delivered, // Backup case
            DO.DeliveredStatus.Rejected => BO.OrderStatus.Returned,
            DO.DeliveredStatus.Canceled => BO.OrderStatus.Canceled,
            DO.DeliveredStatus.Failed => BO.OrderStatus.Pending,    // Failed attempts make order available for retry
            DO.DeliveredStatus.Absent => BO.OrderStatus.Pending,    // Absent attempts make order available for retry
            _ => BO.OrderStatus.Pending
        };
    }

    /// <summary>
    /// Calculates the actual road distance between two geographic points using the LocationIQ routing API.
    /// Provides more accurate distance estimates than the Haversine formula by following roads.
    /// </summary>
    /// <param name="lat1">Latitude of the starting point in degrees.</param>
    /// <param name="lon1">Longitude of the starting point in degrees.</param>
    /// <param name="lat2">Latitude of the destination point in degrees.</param>
    /// <param name="lon2">Longitude of the destination point in degrees.</param>
    /// <returns>
    /// The driving distance in kilometers between the two points.
    /// </returns>
    /// <exception cref="BLFailedOperation">
    /// Thrown if the HTTP request fails, times out, or the API returns an error.
    /// Also thrown if rate-limited (HTTP 429) after retry attempts.
    /// </exception>
    /// <exception cref="BLNotFoundException">Thrown if no route exists between the two points.</exception>
    /// <remarks>
    /// - Uses LocationIQ API with a pre-configured API key.
    /// - Includes retry logic for rate-limiting (HTTP 429).
    /// - Network timeout is set to 20 seconds.
    /// - Note: Longitude comes before latitude in the API request (standard GIS ordering).
    /// </remarks>
    public static async Task<double> CalculateRouteDistanceAsync(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        // Reuse the existing geocoding client + rate limiter to avoid 429 bursts.
        // This keeps behavior consistent and prevents creating many HttpClient instances.
        string apiKey = LocationIqKey;
        const string baseUrl = "https://us1.locationiq.com/v1/directions/driving";

        // LocationIQ expects coordinates in longitude,latitude order
        string url =
            $"{baseUrl}/" +
            $"{lon1.ToString(CultureInfo.InvariantCulture)},{lat1.ToString(CultureInfo.InvariantCulture)};" +
            $"{lon2.ToString(CultureInfo.InvariantCulture)},{lat2.ToString(CultureInfo.InvariantCulture)}" +
            $"?key={Uri.EscapeDataString(apiKey)}" +
            $"&overview=false&alternatives=false";

        // IMPORTANT: avoid logging the full URL because it contains the API key
        System.Diagnostics.Debug.WriteLine("LocationIQ routing request: [redacted]");

        // Retry a few times when rate-limited (HTTP 429)
        for (int attempt = 0; attempt < 3; attempt++)
        {
            // Use the existing throttling mechanism (same as geocoding)
            await EnforceGeoRateLimitAsync().ConfigureAwait(false);

            HttpResponseMessage response;
            try
            {
                // Use the shared client (already configured with timeout + headers in static ctor)
                response = await s_geoClient.GetAsync(url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LocationIQ routing GetAsync FAILED:");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                throw new BLFailedOperation($"Network/TLS error during routing: {ex.Message}", ex);
            }

            using (response)
            {
                // Rate-limited: wait and retry
                if (response.StatusCode == (HttpStatusCode)429)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    string err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new BLFailedOperation($"Routing failed with status {response.StatusCode}: {err}");
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

        // If we exhausted retries due to 429
        throw new BLFailedOperation("LocationIQ routing was rate-limited (HTTP 429) after retries.");
    }

    /// <summary>
    /// Determines the delivery speed based on the transport method.
    /// </summary>
    /// <param name="transport">The delivery transport method (Car, Motorcycle, Bike, Foot).</param>
    /// <param name="config">The configuration object containing speed settings for each transport type.</param>
    /// <returns>The average speed in kilometers per hour for the specified transport method.</returns>
    /// <remarks>
    /// Unknown transport types default to car speed.
    /// </remarks>
    public static double GetSpeed(DO.DeliveryTransport transport, BO.Config config)
    {
        // Return speed based on transport type
        return transport switch
        {
            DO.DeliveryTransport.Car => config.CarSpeed,
            DO.DeliveryTransport.Motorcycle => config.MotorcycleSpeed,
            DO.DeliveryTransport.Bike => config.BikeSpeed,
            DO.DeliveryTransport.Foot => config.WalkingSpeed,
            _ => config.CarSpeed
        };
    }

    /// <summary>
    /// Updates a courier's active status based on inactivity duration.
    /// Deactivates the courier if they have been inactive longer than the specified threshold.
    /// </summary>
    /// <param name="courier">The courier record to evaluate.</param>
    /// <param name="inactivityThreshold">The maximum allowed inactivity period before a courier is considered inactive.</param>
    /// <returns>
    /// The original courier record unchanged, or a modified copy with <see cref="DO.Courier.IsActive"/> set to false
    /// if the inactivity threshold has been exceeded.
    /// </returns>
    /// <remarks>
    /// Uses <see cref="AdminManager.Now"/> for the current time instead of <see cref="DateTime.Now"/> to ensure
    /// consistency with test scenarios and simulation environments where time may be simulated.
    /// </remarks>
    public static DO.Courier UpdateCourierActivity(DO.Courier courier, TimeSpan inactivityThreshold)
    {
        var lastDeliverie = s_dal.Delivery
            .ReadAll()
            .Where(d => d.CourierId == courier.Id)
            .OrderByDescending(d => d.PickupTime)
            .FirstOrDefault();

        if (lastDeliverie is not null && AdminManager.Now - lastDeliverie.ArrivalTime > inactivityThreshold) // last delivery time + threshold
            return courier with { IsActive = false };
        if (lastDeliverie is null && AdminManager.Now - courier.StartDate > inactivityThreshold) // no deliveries yet
            return courier with { IsActive = false };
        return courier;
    }

    /// <summary>
    /// Determines whether a delivery was completed on time.
    /// </summary>
    /// <param name="d">The delivery record to check.</param>
    /// <param name="expectedTime">The expected or maximum allowed arrival time.</param>
    /// <returns>
    /// <c>true</c> if the delivery was completed on time (arrival time is before or at the expected time);
    /// <c>false</c> if the delivery is not completed yet (ArrivalTime is null) or if it was completed late.
    /// </returns>
    /// <remarks>
    /// A delivery with a null ArrivalTime is considered not yet completed and therefore not on time.
    /// </remarks>
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

    /// <summary>
    /// LocationIQ API key used for geocoding and routing requests.
    /// </summary>
    private static readonly string LocationIqKey = "pk.e8d2b136630548a5295a8c88b56c1b82";

    /// <summary>
    /// LocationIQ search endpoint URL for forward geocoding (address to coordinates).
    /// </summary>
    private const string LocationIqSearchEndpoint = "https://us1.locationiq.com/v1/search";

    /// <summary>
    /// Shared HttpClient for geocoding requests with a 20-second timeout.
    /// </summary>
    private static readonly HttpClient s_geoClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// Semaphore to enforce rate limiting on geocoding API requests (one at a time).
    /// </summary>
    private static readonly SemaphoreSlim s_geoRateGate = new SemaphoreSlim(1, 1);
    
    /// <summary>
    /// Timestamp of the last geocoding API request (in UTC).
    /// Used to enforce minimum interval between requests.
    /// </summary>
    private static DateTime s_lastGeoRequestUtc = DateTime.MinValue;
    
    /// <summary>
    /// Minimum interval (in milliseconds) required between consecutive geocoding API requests.
    /// Set to 550ms to comply with LocationIQ rate limits.
    /// </summary>
    private static readonly TimeSpan s_minGeoInterval = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Thread-safe cache mapping normalized addresses to their geographic coordinates.
    /// Key: normalized address string (lowercase, trimmed, single spaces).
    /// Value: tuple containing latitude and longitude.
    /// </summary>
    private static readonly ConcurrentDictionary<string, (double lat, double lon)> s_geoCache = new();

    /// <summary>
    /// Thread-safe cache for route distance calculations to avoid duplicate concurrent requests.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Task<double>> s_routeDistanceCache = new();

    /// <summary>
    /// Static constructor: initializes TLS 1.2 and default HTTP headers for geocoding client.
    /// </summary>
    /// <remarks>
    /// Enforces TLS 1.2 for compatibility with WPF and .NET Framework environments.
    /// Sets default user agent and content type headers for LocationIQ API communication.
    /// </remarks>
    static Tools()
    {
        // IMPORTANT for many WPF/.NET Framework environments: enforce TLS 1.2
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        // Configure default headers for all geocoding requests
        s_geoClient.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetDeliveryProject/1.0");
        s_geoClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>
    /// Converts a textual address into geographic coordinates (Latitude, Longitude)
    /// using LocationIQ Forward Geocoding API.
    /// Results are cached to reduce API calls.
    /// </summary>
    /// <param name="address">
    /// The address string to geocode. Must not be null or whitespace.
    /// Example: "123 Main Street, Tel Aviv, Israel".
    /// </param>
    /// <returns>
    /// A tuple containing the latitude and longitude of the address.
    /// Example: (32.0853, 34.7818) for Tel Aviv coordinates.
    /// </returns>
    /// <exception cref="BLInvalidInputException">Thrown if the address is null or whitespace.</exception>
    /// <exception cref="BLNotFoundException">
    /// Thrown if the address cannot be found by the geocoding service
    /// or if the service returns no results.
    /// </exception>
    /// <exception cref="BLFailedOperation">
    /// Thrown if the API request fails due to network/TLS errors,
    /// rate limiting after retries, or unexpected server responses.
    /// </exception>
    /// <remarks>
    /// - Results are cached using a normalized address as the key (lowercase, trimmed, single spaces).
    /// - Rate limiting is enforced: minimum 550ms between consecutive API requests.
    /// - Includes retry logic for HTTP 429 (too many requests) responses.
    /// - Geographically limited to Israel ("countrycodes=il").
    /// - Uses UTF-8 JSON format with a limit of 1 result per query.
    /// </remarks>
    public static async Task<(double Latitude, double Longitude)> GetCoordinatesFromAddressAsync(string address)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(address))
            throw new BLInvalidInputException("Address cannot be null or empty");

        try
        {
            // Normalize the address for consistent caching
            string normalized = NormalizeAddress(address);

            // Check if the address is already cached
            if (s_geoCache.TryGetValue(normalized, out var cached))
                return (cached.lat, cached.lon);

            // Enforce rate limiting before making the API request
            await EnforceGeoRateLimitAsync().ConfigureAwait(false);

            // Build the LocationIQ search API request URL
            string url =
                $"{LocationIqSearchEndpoint}" +
                $"?key={Uri.EscapeDataString(LocationIqKey)}" +
                $"&q={Uri.EscapeDataString(address)}" +
                $"&countrycodes=il" +  // Limit to Israel
                $"&format=json&limit=1";

            System.Diagnostics.Debug.WriteLine($"LocationIQ geocoding request: {url}");

            // Retry up to 3 times for rate-limiting errors
            for (int attempt = 0; attempt < 3; attempt++)
            {
                HttpResponseMessage response;

                try
                {
                    // Send the geocoding request
                    response = await s_geoClient.GetAsync(url).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Log and re-throw network/TLS errors
                    System.Diagnostics.Debug.WriteLine("LocationIQ geocoding GetAsync FAILED:");
                    System.Diagnostics.Debug.WriteLine(ex.ToString());

                    throw new BLFailedOperation($"Network/TLS error during geocoding: {ex.Message}", ex);
                }

                using (response)
                {
                    System.Diagnostics.Debug.WriteLine($"LocationIQ geocoding response status: {response.StatusCode}");

                    // Handle rate limiting with retry
                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        // Wait 1 second before retrying
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                        continue;
                    }

                    // Check for other HTTP errors
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new BLFailedOperation(
                            $"Geocoding failed with status {response.StatusCode}: {errorContent}");
                    }

                    // Parse the JSON response
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"LocationIQ geocoding response: {json}");

                    // Validate that we received a non-empty response
                    if (string.IsNullOrWhiteSpace(json))
                        throw new BLNotFoundException("Empty response from geocoding service");

                    using var doc = JsonDocument.Parse(json);

                    // Validate that the response is an array with at least one result
                    if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                        throw new BLNotFoundException($"Address not found: {address}");

                    // Extract the first (best match) result
                    var first = doc.RootElement[0];

                    // Extract latitude and longitude strings
                    string? latStr = first.GetProperty("lat").GetString();
                    string? lonStr = first.GetProperty("lon").GetString();

                    // Validate that coordinates are present
                    if (string.IsNullOrWhiteSpace(latStr) || string.IsNullOrWhiteSpace(lonStr))
                        throw new BLNotFoundException("Coordinates missing in geocoding result");

                    // Parse coordinates using invariant culture to handle decimal formatting
                    double lat = double.Parse(latStr, CultureInfo.InvariantCulture);
                    double lon = double.Parse(lonStr, CultureInfo.InvariantCulture);

                    // Cache the result for future lookups
                    s_geoCache[normalized] = (lat, lon);

                    System.Diagnostics.Debug.WriteLine($"LocationIQ geocoding successful: Lat={lat}, Lon={lon}");
                    return (lat, lon);
                }
            }

            // If we exhaust retries for rate limiting
            throw new BLFailedOperation("LocationIQ geocoding was rate-limited (HTTP 429) after retries.");
        }
        catch (Exception ex) when (!(ex is BLNotFoundException || ex is BLInvalidInputException || ex is BLFailedOperation))
        {
            // Catch and wrap any unexpected exceptions
            System.Diagnostics.Debug.WriteLine($"Unexpected exception in LocationIQ geocoding: {ex.GetType().Name}: {ex.Message}");
            throw new BLFailedOperation($"Unexpected error during geocoding: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attempts to resolve coordinates for an address and returns null when the address is invalid.
    /// Network or service failures still throw so callers can decide whether to cancel the operation.
    /// </summary>
    public static async Task<(double Latitude, double Longitude)?> TryGetCoordinatesFromAddressAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        if (string.Equals(address.Trim(), InvalidAddressMarker, StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var coords = await GetCoordinatesFromAddressAsync(address).ConfigureAwait(false);
            return coords;
        }
        catch (BLNotFoundException)
        {
            return null;
        }
        catch (BLInvalidInputException)
        {
            return null;
        }
    }

    /// <summary>
    /// Cached wrapper around <see cref="CalculateRouteDistanceAsync"/> to prevent duplicate concurrent requests.
    /// </summary>
    public static async Task<double> CalculateRouteDistanceCachedAsync(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        string key = string.Format(CultureInfo.InvariantCulture, "Driving:{0:F6},{1:F6}:{2:F6},{3:F6}", lat1, lon1, lat2, lon2);
        var task = s_routeDistanceCache.GetOrAdd(key, _ => CalculateRouteDistanceAsync(lat1, lon1, lat2, lon2));

        try
        {
            return await task.ConfigureAwait(false);
        }
        catch
        {
            s_routeDistanceCache.TryRemove(key, out _);
            throw;
        }
    }

    /// <summary>
    /// Enforces the rate limit for geocoding API requests by introducing a minimum delay between consecutive requests.
    /// Uses a semaphore to ensure thread-safe execution.
    /// </summary>
    /// <remarks>
    /// Ensures that no two geocoding requests are made within 550 milliseconds of each other,
    /// complying with LocationIQ's rate-limiting requirements.
    /// Thread-safe via semaphore pattern.
    /// </remarks>
    private static async Task EnforceGeoRateLimitAsync()
    {
        // Acquire the semaphore to ensure only one thread enforces rate limiting at a time
        await s_geoRateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Get the current UTC time
            var now = DateTime.UtcNow;
            
            // Calculate time elapsed since the last geocoding request
            var elapsed = now - s_lastGeoRequestUtc;

            // If not enough time has passed, delay until the minimum interval is reached
            if (elapsed < s_minGeoInterval)
                await Task.Delay(s_minGeoInterval - elapsed).ConfigureAwait(false);

            // Update the timestamp of the last request
            s_lastGeoRequestUtc = DateTime.UtcNow;
        }
        finally
        {
            // Always release the semaphore
            s_geoRateGate.Release();
        }
    }

    /// <summary>
    /// Normalizes an address string for consistent caching by converting to lowercase,
    /// trimming whitespace, and ensuring single spaces between words.
    /// </summary>
    /// <param name="a">The address string to normalize.</param>
    /// <returns>
    /// A normalized address string in lowercase with trimmed whitespace and single spaces.
    /// Example: "  Tel Aviv   City  " becomes "tel aviv city".
    /// </returns>
    private static string NormalizeAddress(string a) =>
        string.Join(' ', a.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Calculates the estimated arrival time for a delivery based on distance and speed.
    /// </summary>
    /// <param name="orderDate">The order date/time to use as the base for calculating arrival.</param>
    /// <param name="distanceKm">The delivery distance in kilometers. Must be non-negative.</param>
    /// <param name="speedKmH">The average speed in kilometers per hour. Must be positive (greater than 0).</param>
    /// <returns>
    /// The estimated arrival date and time, calculated by adding the travel time (distance / speed) to the order date.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if speed is not positive (≤ 0).</exception>
    /// <remarks>
    /// Formula: Arrival Time = Order Date + (Distance / Speed) in hours.
    /// </remarks>
    public static DateTime CalculateEstimatedArrival(DateTime orderDate, double distanceKm, double speedKmH)
    {
        // Validate that speed is positive to avoid division by zero or negative times
        if (speedKmH <= 0)
            throw new ArgumentException("Speed must be positive.");

        // Calculate travel time in hours
        double hours = distanceKm / speedKmH;
        
        // Add travel time to the order date
        return orderDate.AddHours(hours);
    }

    /// <summary>
    /// Determines the schedule status of a delivery (OnTime, InRisk, or Late)
    /// based on order status, estimated arrival, maximum allowed arrival, and actual arrival times.
    /// </summary>
    /// <param name="status">The current order status.</param>
    /// <param name="orderDate">The date the order was placed (for reference).</param>
    /// <param name="estimatedArrival">The estimated arrival time. If null, status defaults to OnTime.</param>
    /// <param name="maxArrival">The maximum allowed arrival time. If null, status defaults to OnTime.</param>
    /// <param name="realArrival">The actual arrival time (populated only for delivered orders).</param>
    /// <returns>
    /// <see cref="BO.ScheduleStatus.OnTime"/> if the delivery is on schedule or early.
    /// <see cref="BO.ScheduleStatus.InRisk"/> if the current time is past the estimated arrival but before max arrival.
    /// <see cref="BO.ScheduleStatus.Late"/> if the current time exceeds max arrival or if delivered after estimated time.
    /// </returns>
    /// <remarks>
    /// - If estimatedArrival or maxArrival is null, immediately returns OnTime (insufficient data to assess).
    /// - For delivered orders: compares actual arrival time against estimated arrival time.
    /// - For pending/in-progress orders: compares current time (AdminManager.Now) against estimated and max arrival times.
    /// - Uses <see cref="AdminManager.Now"/> to respect simulation environments.
    /// </remarks>
    public static BO.ScheduleStatus CalculateScheduleStatus(
        BO.OrderStatus status,
        DateTime orderDate,
        DateTime? estimatedArrival,
        DateTime? maxArrival,
        DateTime? realArrival)
    {
        var config = AdminManager.GetConfig();
        DateTime now = config.Clock;

        // Maximum allowed delivery time:
        // According to the general description, it is calculated as
        // orderDate + MaxDeliveryTime
        DateTime maxSupplyTime = maxArrival ?? orderDate + config.MaxDeliveryTime;

        // Risk threshold:
        // If the remaining time is less than RiskRange, the order is considered "InRisk"
        DateTime riskThreshold = maxSupplyTime - config.RiskRange;

        // A closed order is an order whose final outcome is already known
        bool isClosed =
            status == BO.OrderStatus.Delivered ||
            status == BO.OrderStatus.Returned ||
            status == BO.OrderStatus.Canceled;

        // ---------------------------------------------------------
        // Closed orders:
        // Compare the actual finish time to the maximum supply time.
        // - finishTime <= maxSupplyTime  -> OnTime
        // - finishTime >  maxSupplyTime  -> Late
        //
        // If realArrival is missing (should not happen in a consistent system),
        // we fall back to the current system time to still return a valid status,
        // since "Unknown" does not exist.
        // ---------------------------------------------------------
        if (isClosed)
        {
            DateTime finishTime = realArrival ?? now;

            return finishTime <= maxSupplyTime
                ? BO.ScheduleStatus.OnTime
                : BO.ScheduleStatus.Late;
        }

        // ---------------------------------------------------------
        // Open (in-treatment) orders:
        // - If the maximum supply time has passed -> Late
        // - If the order is within the risk range -> InRisk
        // - Otherwise                            -> OnTime
        // ---------------------------------------------------------
        if (now >= maxSupplyTime)
            return BO.ScheduleStatus.Late;

        if (now >= riskThreshold)
            return BO.ScheduleStatus.InRisk;

        return BO.ScheduleStatus.OnTime;
    }
}
