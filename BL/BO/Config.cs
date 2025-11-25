using Helpers;

namespace BO;

/// <summary>
/// Configuration settings exposed to the presentation layer.
/// </summary>
public class Config
{
    /// <summary>
    /// Current system clock value.
    /// </summary>
    public DateTime Clock { get; set; }

    /// <summary>
    /// Administrator ID.
    /// </summary>
    public int BossId { get; set; }

    /// <summary>
    /// Administrator password.
    /// </summary>
    public string BossPassword { get; set; } = string.Empty;

    /// <summary>
    /// Speed of cars.
    /// </summary>
    public double CarSpeed { get; set; }

    /// <summary>
    /// Speed of motorcycles.
    /// </summary>
    public double MotorcycleSpeed { get; set; }

    /// <summary>
    /// Speed of bicycles.
    /// </summary>
    public double BikeSpeed { get; set; }

    /// <summary>
    /// Walking speed.
    /// </summary>
    public double WalkingSpeed { get; set; }

    /// <summary>
    /// Maximum allowed delivery time.
    /// </summary>
    public TimeSpan MaxDeliveryTime { get; set; }

    /// <summary>
    /// Time range indicating risk of delay.
    /// </summary>
    public TimeSpan RiskRange { get; set; }

    /// <summary>
    /// Time after which a courier is considered inactive.
    /// </summary>
    public TimeSpan InactivityThreshold { get; set; }

    /// <summary>
    /// Company address.
    /// </summary>
    public string CompanyAddress { get; set; } = string.Empty;

    /// <summary>
    /// Company latitude coordinate.
    /// </summary>
    public double CompanyLatitude { get; set; }

    /// <summary>
    /// Company longitude coordinate.
    /// </summary>
    public double CompanyLongitude { get; set; }

    /// <summary>
    /// Maximum allowed distance in the system.
    /// </summary>
    public double MaxDistance { get; set; }

    public override string ToString() => this.ToStringProperty();
}
