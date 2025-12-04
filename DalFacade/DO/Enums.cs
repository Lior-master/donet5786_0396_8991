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

    /// <summary>Order has been delivered to the recipient.</summary>
    Delivered,

    /// <summary>Order was cancelled before shipment.</summary>
    Canceled,

    /// <summary>Order was returned after delivery.</summary>
    Returned
}

/// <summary>
/// Outcome of a delivery attempt as reported by the courier or delivery subsystem.
/// </summary>
public enum DeliveredStatus
{
    /// <summary>Package successfully delivered to the recipient.</summary>
    Delivered,

    /// <summary>Recipient refused the delivery or returned the package on receipt.</summary>
    Rejected,

    /// <summary>Delivery was cancelled before completion (by sender, system or courier).</summary>
    Canceled,

    /// <summary>Recipient was absent at the delivery location when the courier attempted delivery.</summary>
    Absent,

    /// <summary>Delivery attempt failed due to an error (invalid address, vehicle issue, etc.).</summary>
    Failed
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

    /// <summary>Car (larger volumes or longer distances).</summary>
    Car,

    /// <summary>foot delivery.</summary>
    Foot
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

/// <summary>
/// Schedule status indicating whether a delivery is on time, at risk of delay, or late.
/// </summary>
public enum ScheduleStatus
{
    /// <summary>Delivery is on schedule or ahead of expected time.</summary>
    OnTime,

    /// <summary>Delivery is at risk of being delayed;</summary>
    InRisk,

    /// <summary>Delivery has exceeded acceptable time thresholds and is considered late.</summary>
    Late
}

/// <summary>
/// say type of administrator
/// </summary>
public enum Administrator
{
    Director,
    Courier,
    Customer
}