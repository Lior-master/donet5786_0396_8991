namespace DO;

/// <summary>
/// Represents a customer order and its delivery-related metadata.
/// This immutable positional record contains the information required to process and route a delivery.
/// Use the primary constructor to create instances or the generated `with` expression to create modified copies.
/// </summary>
/// <param name="Id">Unique identifier of the order (database id or similar).</param>
/// <param name="Status">Status of the order/delivery service. See <see cref="OrderStatus"/>.</param>
/// <param name="CustomerName">Full name of the customer.</param>
/// <param name="CustomerAddress">Delivery address for the customer.</param>
/// <param name="CustomerPhone">Contact phone number for the customer (stored as string; prefer E.164 format).</param>
/// <param name="OrderDate">Date and time when the order was placed. Prefer UTC for consistency.</param>
/// <param name="Latitude">Optional latitude (decimal degrees) of the delivery location. Null if not provided.</param>
/// <param name="Longitude">Optional longitude (decimal degrees) of the delivery location. Null if not provided.</param>
/// <param name="Fragility">Optional fragility level of the package. Null if not specified.</param>
/// <param name="Description">Optional free-text description or delivery notes.</param>
public record Order
(
    int Id,
    OrderType Type,
    string CustomerName,
    string CustomerAddress,
    string CustomerPhone,
    DateTime OrderDate,
    double? size = null,
    double? weight = null,
    double? Latitude = null,
    double? Longitude = null,
    FragilityLevel? Fragility = null,
    string? Description = null
)
{
    /// <summary>
    /// Initializes a new instance of <see cref="Order"/> with default values.
    /// </summary>
    /// <remarks>
    /// Defaults:
    /// - Id = 0
    /// - Type = <see cref="OrderType.Standard"/>
    /// - OrderDescription, CustomerName, CustomerAddress, CustomerPhone = empty string
    /// - DeliveryTransport = <see cref="DeliveryTransport.Car"/>
    /// - Fragility = null
    /// - Description = null
    /// - Status = null
    /// </remarks>
public Order() : this(0, OrderType.FastFood, string.Empty, string.Empty, string.Empty, DateTime.Now) { }
}