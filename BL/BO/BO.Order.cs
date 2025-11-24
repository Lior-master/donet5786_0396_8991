namespace BO;

/// <summary>
/// Represents a customer order with its delivery and scheduling information.
/// </summary>
public class Order
{
    /// <summary>
    /// Unique identifier of the order.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Type of the order or delivery service.
    /// </summary>
    public OrderType Type { get; set; }

    /// <summary>
    /// Optional description provided with the order.
    /// </summary>
    public string? OrderDescription { get; set; }

    /// <summary>
    /// Delivery address provided by the customer.
    /// </summary>
    public string CustomerAdress { get; set; }

    /// <summary>
    /// Geographic latitude of the delivery address.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Geographic longitude of the delivery address.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Calculated route distance or fallback straight-line distance to the delivery address.
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Customer full name.
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Customer contact phone number.
    /// </summary>
    public string CustomerPhone { get; set; }

    /// <summary>
    /// Weight of the package in kilograms, if provided.
    /// </summary>
    public double? Weight { get; set; }

    /// <summary>
    /// Fragility level of the package, if specified.
    /// </summary>
    public FragilityLevel? Fragility { get; set; }

    /// <summary>
    /// Volume of the package, if provided.
    /// </summary>
    public double? Volume { get; set; }

    /// <summary>
    /// Date and time when the order was placed.
    /// </summary>
    public DateTime OrderDate { get; init; }

    /// <summary>
    /// Estimated arrival date and time for the delivery, when available.
    /// </summary>
    public DateTime? ArrivalDateEstimeted { get; init; }

    /// <summary>
    /// Latest acceptable arrival date and time for the delivery.
    /// </summary>
    public DateTime? ArrivalDateMax { get; init; }

    /// <summary>
    /// Current workflow status of the order.
    /// </summary>
    public OrderStatus Status { get; init; }

    /// <summary>
    /// Scheduling status indicating whether delivery is on time, at risk, or late.
    /// </summary>
    public ScheduleStatus ScheduleStatus { get; init; }

    /// <summary>
    /// Estimated duration required to complete the delivery.
    /// </summary>
    public TimeSpan ArrivalTimeEstimeted { get; init; }

    /// <summary>
    /// List of deliveries associated with this order, useful for displaying delivery history.
    /// </summary>
    public List<DeliveryPerOrderInList>? DeliveriesPerOrder { get; init; }
}
