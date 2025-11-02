namespace Dal;

public static class Config
{

    internal const int startOrderId = 1000;
    internal const int startDeliveryId = 2000;

    internal static int nextOrderId = startOrderId;
    internal static int nextDeliveryId = startDeliveryId;

    internal static int NextOrderId => nextOrderId++;
    internal static int NextDeliveryId => nextDeliveryId++;

    internal static DateTime Clock { get; set; } = DateTime.Now;

    internal static int BossId { get; set; } = 0;
    internal static string BossPassword { get; set; } = string.Empty; // modifiable plus tard

    internal static string CompanyAddress { get; set; } = string.Empty;
    internal static double Latitude { get; set; } = 0;
    internal static double Longitude { get; set; } = 0;

    internal static double SpeedOfCar { get; set; } = 0;
    internal static double SpeedOfMotorBike { get; set; } = 0;
    internal static double SpeedOfBike { get; set; } = 0;
    internal static double SpeedOfWalking { get; set; } = 0;

    internal static double MaxDistance { get; set; } = 0; 
    internal static TimeSpan MaxDeliveryTimeRange { get; set; } = TimeSpan.FromHours(3);
    internal static TimeSpan RiskRange { get; set; } = TimeSpan.FromHours(2);
    internal static TimeSpan InactivityTime { get; set; } = TimeSpan.FromHours(1);

    internal static void Reset()
    {
        nextOrderId = startOrderId;
        nextDeliveryId = startDeliveryId;
        Clock = DateTime.Now;
    }
}
