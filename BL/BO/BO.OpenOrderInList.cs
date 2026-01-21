using Helpers;

/// <summary>
/// Defines types for this application layer.
/// </summary>
namespace BO;

/// <summary>
/// Represents the open order in list component in this layer.
/// </summary>
public class OpenOrderInList
{
    /// <summary>
    /// Identifier of the courier assigned to the order, or <c>null</c> if none assigned.
    /// </summary>
    public int? CourierId { get; init; }

    /// <summary>
    /// Identifier of the order.
    /// </summary>
    public int OrderId { get; init; }

    /// <summary>
    /// Type of the order or delivery service.
    /// </summary>
    public OrderType OrderType { get; init; }

    /// <summary>
    /// Fragility level of the package, or <c>null</c> when not specified.
    /// </summary>
    public FragilityLevel? Fragility { get; init; }

    /// <summary>
    /// Customer delivery address.
    /// </summary>
    // Default to empty to keep nullable analysis clean for list items.
    /// <summary>
    /// Gets or sets the customer address value.
    /// </summary>
    public string CustomerAddress { get; init; } = string.Empty;

    /// <summary>
    /// Straight-line ("as-the-crow-flies") distance from the company to the customer, in kilometers.
    /// </summary>
    public double BirdDistance { get; init; }

    /// <summary>
    /// Route distance to the customer in kilometers, or <c>null</c> when not calculated.
    /// </summary>
    public double? Distance { get; init; }

    /// <summary>
    /// Time elapsed since the order was added to the system, or <c>null</c> when not applicable.
    /// </summary>
    public TimeSpan? AddedTime { get; init; }

    /// <summary>
    /// Current scheduling status indicating whether the delivery is on time, at risk, or late.
    /// </summary>
    public ScheduleStatus ScheduleStatus { get; init; }

    /// <summary>
    /// Estimated time required to deliver the order.
    /// </summary>
    public TimeSpan EstimatedDeliveryTime { get; init; }

    /// <summary>
    /// Latest acceptable delivery time for the order.
    /// </summary>
    public DateTime MaxDeliveredTime { get; init; }

    public override string ToString() => this.ToStringProperty();

}
