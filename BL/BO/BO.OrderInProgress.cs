using Helpers;

namespace BO;

/// <summary>
/// Represents the live state of an order that is currently being delivered.
/// Provides both timeline and contact details needed while the delivery is in progress.
/// </summary>
public class OrderInProgress
{
    /// <summary>
    /// Identifier of the delivery record handling this order.
    /// </summary>
    public int DeliveryId { get; init; }

    /// <summary>
    /// Identifier of the related order.
    /// </summary>
    public int OrderId { get; init; }

    /// <summary>
    /// Numeric representation of the order status.
    /// This value can be used when a numeric code is required (for example when interacting with lower-level DAL).
    /// </summary>
    public OrderType OrderType { get; init; } 

    /// <summary>
    /// Calculated or measured route distance for the delivery, in kilometers when available.
    /// </summary>
    public double? Distance { get; init; }

    /// <summary>
    /// Time when the courier picked up the order.
    /// </summary>
    public DateTime PickupTime { get; init; }

    /// <summary>
    /// Time when the delivery arrived at the destination, or <c>null</c> if not yet arrived.
    /// </summary>
    public DateTime? ArrivalTime { get; init; }

    /// <summary>
    /// Scheduling status indicating whether the delivery is on time, at risk, or late.
    /// </summary>
    public ScheduleStatus ScheduleStatus { get; init; }

    /// <summary>
    /// Typed enum representation of the order status.
    /// Use this property in business logic when the enum form is required.
    /// </summary>
    public OrderStatus OrderStatus { get; init; }

    /// <summary>
    /// Optional textual description of the order or delivery notes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Customer full name.
    /// </summary>
    public string CustomerName { get; init; }

    /// <summary>
    /// Customer contact phone number.
    /// </summary>
    public string CustomerPhone { get; init; }

    /// <summary>
    /// Customer delivery address.
    /// </summary>
    public string CustomerAddress { get; init; }

    /// <summary>
    /// Date and time when the order was placed.
    /// </summary>
    public DateTime OrderDate { get; init; }

    /// <summary>
    /// Estimated arrival time for the delivery.
    /// </summary>
    public DateTime EstimatedArrivalTime { get; init; }

    /// <summary>
    /// Latest allowed delivery time for the order.
    /// </summary>
    public DateTime MaxDeliveryTime { get; init; }

    /// <summary>
    /// Time the order has been waiting since pickup or since it was queued, as applicable.
    /// </summary>
    public TimeSpan WaitingTime { get; init; }

    public override string ToString() => this.ToStringProperty();
}