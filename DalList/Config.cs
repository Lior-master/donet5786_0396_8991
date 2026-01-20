using System.Runtime.CompilerServices;

namespace Dal;

/// <summary>
/// Global configuration settings used by the DAL.
/// All members are static and the class is initialized once via the static constructor.
/// All property access is synchronized for thread-safety (Stage 7).
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
    /// Thread-safe access with synchronized getter.
    /// </summary>
    internal static int NextOrderId
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get => nextOrderId++;
    }

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
    /// Thread-safe access with synchronized getter.
    /// </summary>
    internal static int NextDeliveryIdValue
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get => nextDeliveryId++;
    }

    /// <summary>
    /// Global clock used by the DAL (current or simulated time).
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static DateTime Clock
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    }

    /// <summary>
    /// Boss / administrator identifier.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static int BossId
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    }

    /// <summary>
    /// Boss / administrator password.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static string BossPassword
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    } = "";

    /// <summary>
    /// Average car speed in km/h.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static double CarSpeed
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    } = 40; // in km/h

    /// <summary>
    /// Average motorcycle speed in km/h.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static double MotorcycleSpeed
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    } = 50; // in km/h

    /// <summary>
    /// Average bicycle speed in km/h.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static double BikeSpeed
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    } = 20; // in km/h

    /// <summary>
    /// Average walking speed in km/h.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static double WalkingSpeed
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    } = 5; // in km/h

    /// <summary>
    /// Maximum allowed delivery time span.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static TimeSpan MaxTimeDelivery
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    }

    /// <summary>
    /// Time range after which a delivery is considered at risk.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static TimeSpan RiskRange
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    }

    /// <summary>
    /// Inactivity time after which the courier is considered inactive.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static TimeSpan Inactivity
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    }

    /// <summary>
    /// Company address, if known.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static string? CompanyAddress
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    } = null;

    /// <summary>
    /// Company latitude, if known.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static double? Latitude
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    } = null;

    /// <summary>
    /// Company longitude, if known.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static double? Longitude
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    } = null;

    /// <summary>
    /// Maximum delivery distance, if applicable.
    /// Thread-safe access with synchronized getter and setter.
    /// </summary>
    internal static double? MaxDistance
    {
        [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
        get; set;
    } = null;

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

    /// <summary>
    /// Thread-safe reset of configuration to initial values.
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
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
