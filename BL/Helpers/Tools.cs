using BO;
using System.IO;
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
    

    // Accept nullable parameters and return null if any parameter is null.
    public static double BirdDistance(double? lat1, double? lon1, double? lat2, double? lon2)
    {
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue)
            return -1;

        const double R = 6371; // earth radius in kilometers

        double dLat = ToRadians(lat2.Value - lat1.Value);
        double dLon = ToRadians(lon2.Value - lon1.Value);

        double la1 = ToRadians(lat1.Value);
        double la2 = ToRadians(lat2.Value);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(la1) * Math.Cos(la2) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c; // Distance in kilometers
    }

    private static double ToRadians(double angle)
    {
        return angle * Math.PI / 180.0;
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

    public static DateTime CalculateExpectedArrivalTime(DO.Delivery d, BO.Config config)
    {
        if (d.Distance == null)
            throw new BLInvalidInputException("Cannot calculate expected time without distance");

        double speed = GetSpeed(d.Transport, config);

        // distance / speed = time in hours
        double hours = d.Distance.Value / speed;

        return d.PickupTime.AddHours(hours);
    }

    public static bool IsDeliveryOnTime(DO.Delivery d, DateTime expectedTime)
    {
        // If ArrivalTime is null, delivery is not completed yet, hence not on time
        if (d.ArrivalTime == null)
            return false;

        return d.ArrivalTime <= expectedTime;
    }

    private static readonly HttpClient client = new HttpClient();

    /// <summary>
    /// Converts a textual address into geographic coordinates (Latitude, Longitude)
    /// using OpenStreetMap Nominatim API (no API key required).
    /// </summary>
    public static async Task<(double Latitude, double Longitude)> GetCoordinatesFromAddressAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new BLInvalidInputException("Address cannot be empty.");

        // URL encode the address
        string urlAddress = Uri.EscapeDataString(address);

        string url = $"https://nominatim.openstreetmap.org/search?format=json&q={urlAddress}";

        // Required by Nominatim
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetProject/1.0");

        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            // Parse the JSON array
            using JsonDocument doc = JsonDocument.Parse(json);

            var results = doc.RootElement;

            if (results.GetArrayLength() == 0)
                throw new BLNotFoundException("No coordinates found for this address.");

            var first = results[0];

            double lat = double.Parse(first.GetProperty("lat").GetString()!);
            double lon = double.Parse(first.GetProperty("lon").GetString()!);

            return (lat, lon);
        }
        catch (Exception ex)
        {
            throw new BLNotFoundException($"Failed to geocode address '{address}': {ex.Message}", ex);
        }
    }

    public static DateTime CalculateEstimatedArrival(DateTime orderDate, double distanceKm, double speedKmH)
    {
        if (speedKmH <= 0)
            throw new ArgumentException("Speed must be positive.");

        double hours = distanceKm / speedKmH;
        return orderDate.AddHours(hours);
    }

    public static ScheduleStatus CalculateScheduleStatus(
    OrderStatus orderStatus,
    DateTime orderDate,
    DateTime? estimatedArrival,
    DateTime? maxArrival,
    DateTime? realArrival)
    {
        // If no estimates available → cannot determine schedule
        if (estimatedArrival == null || maxArrival == null)
            return ScheduleStatus.Unknown;

        DateTime now = DateTime.Now;

        // Case 1: Order was already delivered
        if (orderStatus == OrderStatus.Delivered && realArrival != null)
        {
            if (realArrival <= estimatedArrival)
                return ScheduleStatus.OnTime;
            else
                return ScheduleStatus.Late;
        }

        // Case 2: Order still active / not delivered

        // Late if we already passed the maximum allowed arrival time
        if (now > maxArrival)
            return ScheduleStatus.Late;

        // At risk if we passed the estimated arrival but not the max arrival
        if (now > estimatedArrival)
            return ScheduleStatus.InRisk;

        // Otherwise: on time
        return ScheduleStatus.OnTime;
    }

}
