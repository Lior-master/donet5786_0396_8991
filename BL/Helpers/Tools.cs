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
        string url = $"http://router.project-osrm.org/route/v1/driving/{lon1},{lat1};{lon2},{lat2}?overview=false";

        var response = await client.GetStringAsync(url);
        var data = System.Text.Json.JsonSerializer.Deserialize<dynamic>(response);

        if (data == null || data.routes == null)
            throw new Exception("Routing service error");

        double meters = data.routes[0].distance;
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

    internal static void UpdateCourierActivity(DO.Courier courier, TimeSpan inactivityThreshold)
    {
        if (DateTime.Now - courier.StartDate > inactivityThreshold)
            courier.IsActive = false;
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
    public static async Task<(double Latitude, double Longitude)>  GetCoordinatesFromAddressAsync(string address)
    {
        using var client = new HttpClient();
        string url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(address)}";

        var response = await client.GetStringAsync(url);
        var results = System.Text.Json.JsonSerializer.Deserialize<List<dynamic>>(response);

        if (results == null || results.Count == 0)
            throw new Exception("Address not found");

        double lat = double.Parse((string)results[0].lat);
        double lon = double.Parse((string)results[0].lon);

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
