namespace Dal;

/// <summary>
/// Global configuration settings used by the DAL.
/// All members are static and the class is initialized once via the static constructor.
/// </summary>
internal static class Config
{
    /// <summary>
    /// Starting order identifier (constant).
    /// </summary>
    internal const int InitialOrderId = 1000;

    /// <summary>
    /// Backing field for <see cref="NextOrderId"/>.
    /// </summary>
    private static int nextOrderId = InitialOrderId;

    /// <summary>
    /// Returns the next available order identifier and advances the internal counter.
    /// </summary>
    internal static int NextOrderId { get => nextOrderId++; }

    /// <summary>
    /// Starting delivery identifier (constant).
    /// </summary>
    internal const int NextDeliveryId = 5000;

    /// <summary>
    /// Backing field for <see cref="NextDeliveryIdValue"/>.
    /// </summary>
    private static int nextDeliveryId = NextDeliveryId;

    /// <summary>
    /// Returns the next available delivery identifier and advances the internal counter.
    /// </summary>
    internal static int NextDeliveryIdValue { get => nextDeliveryId++; }

    /// <summary>
    /// Global clock used by the DAL (current or simulated time).
    /// </summary>
    internal static DateTime Clock;

    /// <summary>
    /// Boss / administrator identifier.
    /// </summary>
    internal static int BossId ;

    /// <summary>
    /// Boss / administrator password.
    /// </summary>
    internal static string BossPassword;

    /// <summary>
    /// Average car speed in km/h.
    /// </summary>
    internal static double CarSpeed = 40; // in km/h

    /// <summary>
    /// Average motorcycle speed in km/h.
    /// </summary>
    internal static double MotorcycleSpeed = 50; // in km/h

    /// <summary>
    /// Average bicycle speed in km/h.
    /// </summary>
    internal static double BikeSpeed = 20; // in km/h

    /// <summary>
    /// Average walking speed in km/h.
    /// </summary>
    internal static double WalkingSpeed = 5; // in km/h

    /// <summary>
    /// Maximum allowed delivery time span.
    /// </summary>
    internal static TimeSpan MaxTimeDelivery;

    /// <summary>
    /// Time range after which a delivery is considered at risk.
    /// </summary>
    internal static TimeSpan RiskRange;

    /// <summary>
    /// Inactivity time after which the courier is considered inactive.
    /// </summary>
    internal static TimeSpan Inactivity;

    /// <summary>
    /// Company address, if known.
    /// </summary>
    internal static string? CompanyAddress = null;

    /// <summary>
    /// Company latitude, if known.
    /// </summary>
    internal static double? Latitude = null;

    /// <summary>
    /// Company longitude, if known.
    /// </summary>
    internal static double? Longitude = null;

    /// <summary>
    /// Maximum delivery distance, if applicable.
    /// </summary>
    internal static double? MaxDistance = null;

    /// <summary>
    /// Static constructor: initializes default values for the configuration.
    /// Executes once before the first use of the class.
    /// </summary>
    static Config()
    {
        Clock = DateTime.Now;
        BossId = 0;
        BossPassword = "";
        MaxTimeDelivery = TimeSpan.FromHours(1);
        RiskRange = TimeSpan.FromMinutes(30);
        Inactivity = TimeSpan.FromDays(30);
    }

    internal static void Reset()
    {
        nextOrderId = InitialOrderId;
        nextDeliveryId = NextDeliveryId;
        Clock = DateTime.Now;
        BossId = 0;
        BossPassword = "";
        MaxTimeDelivery = TimeSpan.FromHours(1);
        RiskRange = TimeSpan.FromMinutes(30);
        Inactivity = TimeSpan.FromDays(30);
        CompanyAddress = null;
        Latitude = null;
        Longitude = null;
        MaxDistance = null;
    }
}
