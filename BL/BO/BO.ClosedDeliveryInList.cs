using Helpers;

namespace BO;

/// <summary>
/// Represents a summary entry for a completed delivery shown in lists.
/// </summary>
public class ClosedDeliveryInList
{
    /// <summary>
    /// Unique identifier of the delivery record.
    /// </summary>
    public int DeliveryId { get; init; }

    /// <summary>
    /// Identifier of the associated order.
    /// </summary>
    public int OrderId { get; init; }

    /// <summary>
    /// Type of the order (e.g., Standard, Express, Scheduled, Pickup).
    /// </summary>
    public OrderType OrderType { get; init; }

    /// <summary>
    /// Delivery address provided by the customer.
    /// </summary>
    public string CustomerAdress { get; init; }

    /// <summary>
    /// Transport method used to perform the delivery.
    /// </summary>
    public DeliveryTransport DeliveryTransport { get; init; }

    /// <summary>
    /// Actual distance travelled for the delivery, in kilometers when available.
    /// </summary>
    public double? ActualDistance { get; init; }

    /// <summary>
    /// Total time elapsed to complete the delivery.
    /// </summary>
    public TimeSpan DeliveryTotalTime  { get; init; }

    /// <summary>
    /// Final outcome of the delivery attempt.
    /// </summary>
    public DeliveredStatus DeliveredStatus { get; init; }

    public override string ToString() => this.ToStringProperty();

}
