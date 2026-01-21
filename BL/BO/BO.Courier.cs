using Helpers;

/// <summary>
/// Defines types for this application layer.
/// </summary>
namespace BO;

/// <summary>
/// Represents the courier component in this layer.
/// </summary>
public class Courier
{
    /// <summary>
    /// Unique identifier of the courier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Full name of the courier.
    /// </summary>
    // Default to empty to satisfy nullable analysis for DTO-style initialization.
    /// <summary>
    /// Gets or sets the name value.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Contact phone number for the courier.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Contact email address for the courier.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// password for the courier's account.
    ///</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the courier is currently active and available for assignments.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Primary transport method used by the courier.
    /// </summary>
    public DeliveryTransport Transport { get; set; }

    /// <summary>
    /// Date when the courier started working (immutable after creation).
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// Total number of deliveries completed on time.
    /// </summary>
    public int NumberOfOnTimeDeliveries { get; init; }

    /// <summary>
    /// Total number of deliveries completed late.
    /// </summary>
    public int NumberOfLateDeliveries { get; init; }

    /// <summary>
    /// The order the courier is currently working on, or <c>null</c> when idle.
    /// </summary>
    public OrderInProgress? CurrentOrder { get; init; }

    /// <summary>
    /// Maximum distance (in kilometers) the courier is willing or allowed to travel for a delivery.
    /// Nullable when no limit is specified.
    /// </summary>
    public double? MaxDistance { get; set; }

    /// <summary>
    /// Say if the courier is administrator or director
    /// </summary>
    public Administrator Administrator { get; init; }

    public override string ToString() => this.ToStringProperty();

}
