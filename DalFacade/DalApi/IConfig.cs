using DO;

/// <summary>
/// Defines types for this application layer.
/// </summary>
namespace DalApi;

/// <summary>
/// Defines the contract for config operations.
/// </summary>
public interface IConfig
{
    /// <summary>
    /// Gets or sets the clock value.
    /// </summary>
    DateTime Clock { get; set; }
    /// <summary>
    /// Gets or sets the boss id value.
    /// </summary>
    int BossId { get; set; }
    /// <summary>
    /// Gets or sets the boss password value.
    /// </summary>
    string BossPassword { get; set; }
    /// <summary>
    /// Gets or sets the car speed value.
    /// </summary>
    double CarSpeed { get; set; }
    /// <summary>
    /// Gets or sets the motorcycle speed value.
    /// </summary>
    double MotorcycleSpeed { get; set; }
    /// <summary>
    /// Gets or sets the bike speed value.
    /// </summary>
    double BikeSpeed { get; set; }
    /// <summary>
    /// Gets or sets the walking speed value.
    /// </summary>
    double WalkingSpeed { get; set; }
    /// <summary>
    /// Gets or sets the max time delivery value.
    /// </summary>
    TimeSpan MaxTimeDelivery { get; set; }
    /// <summary>
    /// Gets or sets the risk range value.
    /// </summary>
    TimeSpan RiskRange { get; set; }
    /// <summary>
    /// Gets or sets the inactivity value.
    /// </summary>
    TimeSpan Inactivity { get; set; }
    /// <summary>
    /// Gets or sets the company adress value.
    /// </summary>
    string CompanyAdress { get; set; }
    /// <summary>
    /// Gets or sets the latitude value.
    /// </summary>
    double Latitude { get; set; }
    /// <summary>
    /// Gets or sets the longitude value.
    /// </summary>
    double Longitude { get; set; }
    /// <summary>
    /// Gets or sets the max distance value.
    /// </summary>
    double MaxDistance { get; set; }


    /// <summary>
    /// Resets the component.
    /// </summary>
    void Reset();
}
