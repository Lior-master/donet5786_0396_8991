namespace BO;

/// <summary>
/// Represents a courier who performs deliveries.
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
    public string Name { get; set; }

    /// <summary>
    /// Contact phone number for the courier.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Contact email address for the courier.
    /// </summary>
    public string Email { get; set; }

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
    public int NumberOfOnTimeDeliveries { get; }

    /// <summary>
    /// Total number of deliveries completed late.
    /// </summary>
    public int NumberOfLateDeliveries { get; }

    /// <summary>
    /// The order the courier is currently working on, or <c>null</c> when idle.
    /// </summary>
    public BO.OrderInProgress? CurrentOrder { get; }

    /// <summary>
    /// Maximum distance (in kilometers) the courier is willing or allowed to travel for a delivery.
    /// Nullable when no limit is specified.
    /// </summary>
    public double? MaxDistance { get; set; }
}