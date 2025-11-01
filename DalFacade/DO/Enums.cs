namespace DO;

/// <summary>
/// Status of an order in the system.
/// </summary>
public enum OrderStatus
{
    /// <summary>Order has been created but not yet processed.</summary>
    Pending,

    /// <summary>Order is currently being processed.</summary>
    Processing,

    /// <summary>Order has been shipped and is in transit.</summary>
    Shipped,

    /// <summary>Order has been delivered to the recipient.</summary>
    Delivered,

    /// <summary>Order was cancelled before shipment.</summary>
    Cancelled,

    /// <summary>Order was returned after delivery.</summary>
    Returned
}

/// <summary>
/// Transport method used for delivery.
/// </summary>
public enum DeliveryTransport
{
    /// <summary>Motorcycle (fast, urban deliveries).</summary>
    Motorcycle,

    /// <summary>Bike (eco-friendly, short distances).</summary>
    Bike,

    /// <summary>Scooter or light motorized vehicle.</summary>
    Scooter,

    /// <summary>Car (larger volumes or longer distances).</summary>
    Car,

    /// <summary>Drone delivery (where supported).</summary>
    Drone
}

/// <summary>
/// Type of order or delivery service.
/// </summary>
public enum OrderType
{
    /// <summary>Standard delivery (regular timeframes).</summary>
    Standard,

    /// <summary>Express delivery (reduced delivery time).</summary>
    Express,

    /// <summary>Scheduled delivery at a specific time window.</summary>
    Scheduled,

    /// <summary>Customer pickup from a designated location.</summary>
    Pickup
}

/// <summary>
/// Priority level for order handling.
/// </summary>
public enum PriorityLevel
{
    /// <summary>Low priority — standard handling.</summary>
    Low,

    /// <summary>Normal priority.</summary>
    Medium,

    /// <summary>High priority — expedited handling.</summary>
    High,

    /// <summary>Critical priority — immediate action required.</summary>
    Critical
}

/// <summary>
/// Fragility level of the package content.
/// </summary>
public enum FragilityLevel
{
    /// <summary>Not fragile — no special handling required.</summary>
    Low,

    /// <summary>Moderately fragile — basic precautions required.</summary>
    Medium,

    /// <summary>Fragile — careful handling required.</summary>
    High,

    /// <summary>Extremely fragile — special packaging and transport required.</summary>
    ExtremelyFragile
}