using Helpers;

namespace BO;

/// <summary>
/// Lightweight view model for displaying couriers in lists.
/// </summary>
public class CourierInList
{
    /// <summary>
    /// Unique identifier of the courier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Display name of the courier.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Indicates whether the courier is currently active and available.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Primary transport method used by the courier.
    /// </summary>
    public DeliveryTransport Transport { get; init; }

    /// <summary>
    /// Date when the courier started working.
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// Number of deliveries completed on time.
    /// </summary>
    public int NumberOfOnTimeDeliveries { get; init; }

    /// <summary>
    /// Number of deliveries completed late.
    /// </summary>
    public int NumberOfLateDeliveries { get; init; }

    /// <summary>
    /// Identifier of the current order assigned to the courier, or <c>null</c> if idle.
    /// </summary>
    public int? ActualOrder { get; init; }

    public override string ToString() => this.ToStringProperty();
}
