using Helpers;

namespace BO;

/// <summary>
/// Lightweight view model representing a delivery entry associated with an order,
/// used for listing deliveries per order.
/// </summary>
public class DeliveryPerOrderInList
{
    /// <summary>
    /// Unique identifier of the delivery record.
    /// </summary>
    public int DeliveryId { get; init; }

    /// <summary>
    /// Identifier of the courier assigned to the delivery, or <c>null</c> if none.
    /// </summary>
    public int? CourierId { get; init; }

    /// <summary>
    /// Display name of the courier assigned to the delivery.
    /// </summary>
    public string CourierName { get; init; }

    /// <summary>
    /// Type of the order associated with this delivery.
    /// </summary>
    public DeliveryTransport transport { get; init; }

    /// <summary>
    /// Time when the courier picked up the order for delivery.
    /// </summary>
    public DateTime PickupTime { get; init; }

    /// <summary>
    /// Current status of the order, or <c>null</c> when not available.
    /// </summary>
    public DeliveredStatus? DeliveredStatus { get; init; }

    /// <summary>
    /// Time when the delivery arrived at the destination, or <c>null</c> if not yet arrived.
    /// </summary>
    public DateTime? ArrivalTime { get; init; }

    public override string ToString() => this.ToStringProperty();
}
