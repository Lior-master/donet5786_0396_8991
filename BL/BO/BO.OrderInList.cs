using Helpers;

/// <summary>
/// Defines types for this application layer.
/// </summary>
namespace BO;

/// <summary>
/// Represents the order in list component in this layer.
/// </summary>
public class OrderInList
{
    /// <summary>
    /// Identifier of the delivery associated with the order, or <c>null</c> if not assigned.
    /// </summary>
    public int? DeliveryId { get; init; }

    /// <summary>
    /// Unique identifier of the order.
    /// </summary>
    public int OrderId { get; init; }

    /// <summary>
    /// Type of the order or delivery service.
    /// </summary>
    public OrderType Type { get; init; }

    /// <summary>
    /// Distance to the delivery address (in kilometers).
    /// </summary>
    public double Distance { get; init; }

    /// <summary>
    /// Current processing status of the order.
    /// </summary>
    public OrderStatus Status { get; init; }

    /// <summary>
    /// Scheduling status indicating whether the delivery is on time, at risk, or late.
    /// </summary>
    public ScheduleStatus ScheduleStatus { get; init; }

    /// <summary>
    /// Elapsed time from order creation until the end of the order lifecycle or current cutoff.
    /// </summary>
    public TimeSpan OrderEndTime { get; init; }

    /// <summary>
    /// Elapsed time spent processing the order (handling, packing, dispatch).
    /// </summary>
    public TimeSpan TreatmentEndTime { get; init; }

    /// <summary>
    /// Number of couriers involved or available for this order.
    /// </summary>
    public int NumberOfCouriers { get; init; }

    public override string ToString() => this.ToStringProperty();
}
