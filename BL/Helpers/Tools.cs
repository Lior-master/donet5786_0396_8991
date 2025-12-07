using BO;
using System.Globalization;
using System.IO;
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

    public static async Task<double> CalculateRouteDistanceAsync(double lat1, double lon1, double lat2, double lon2)
    {
        using var client = new HttpClient();

        // OBLIGATOIRE : sinon OSRM renvoie 403
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MyDotNetApp/1.0)");

        string url =
            $"https://router.project-osrm.org/route/v1/driving/{lon1},{lat1};{lon2},{lat2}?overview=false";

        var response = await client.GetAsync(url);

        // Diagnostic si un jour ça recasse
        Console.WriteLine("StatusCode = " + (int)response.StatusCode);

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var route = doc.RootElement.GetProperty("routes")[0];
        double meters = route.GetProperty("distance").GetDouble();

        return meters / 1000.0;
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
        if (DateTime.Now - courier.StartDate > inactivityThreshold)
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

    private static readonly HttpClient client = new HttpClient();

    static Tools()
    {
        // User-Agent OBLIGATOIRE pour Nominatim
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "DotNetDeliveryProject/1.0 (your-email@something.com)");
    }

    /// <summary>
    /// Converts a textual address into geographic coordinates (Latitude, Longitude)
    /// using OpenStreetMap Nominatim API (no API key required).
    /// </summary>
    public static async Task<(double Latitude, double Longitude)> GetCoordinatesFromAddressAsync(string address)
    {
        // on réutilise le HttpClient statique + User-Agent déjà configuré
        string url =
            $"https://nominatim.openstreetmap.org/search?format=json&limit=1&q={Uri.EscapeDataString(address)}";

        using var response = await client.GetAsync(url);

        // si 403 / 500 etc -> HttpRequestException avec le message que tu voyais
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement;

        if (results.GetArrayLength() == 0)
            throw new Exception("Address not found");

        var first = results[0];

        // important : utiliser InvariantCulture pour les nombres avec point
        double lat = double.Parse(first.GetProperty("lat").GetString(), CultureInfo.InvariantCulture);
        double lon = double.Parse(first.GetProperty("lon").GetString(), CultureInfo.InvariantCulture);

        return (lat, lon);
    }


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

        DateTime now = DateTime.Now;

        if (now > maxArrival)
            return BO.ScheduleStatus.Late;

        if (now > estimatedArrival)
            return BO.ScheduleStatus.InRisk;

        return BO.ScheduleStatus.OnTime;
    }

}
