namespace BO;

/// <summary>
/// Defines supported order status values.
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
    Returned,

    All
}

/// <summary>
/// Defines supported delivered status values.
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
    Failed,

    All

}

/// <summary>
/// Defines supported delivery transport values.
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
    Foot,

    All
}

/// <summary>
/// Defines supported order type values.
/// </summary>
public enum OrderType
{
    FastFood,
    Pizza,
    Suchi,
    Shawarma,
    Dessert,
    All
}

/// <summary>
/// Defines supported priority level values.
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
    Critical,

    All
}

/// <summary>
/// Defines supported fragility level values.
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
    ExtremelyFragile,
    All
}

/// <summary>
/// Defines supported schedule status values.
/// </summary>
public enum ScheduleStatus
{
    /// <summary>Delivery is on schedule or ahead of expected time.</summary>
    OnTime,

    /// <summary>Delivery is at risk of being delayed;</summary>
    InRisk,

    /// <summary>Delivery has exceeded acceptable time thresholds and is considered late.</summary>
    Late,

    /// <summary>
    /// Specifies that all available options or items are selected. (Used for filtering or querying.)
    /// </summary>
    All


}
/// <summary>
/// Defines supported time unit values.
/// </summary>
public enum TimeUnit
{
    Second,
    Minute,
    Hour,
    Day,
    Month,
    Year
}

/// <summary>
/// Defines supported administrator values.
/// </summary>
public enum Administrator
{
    Director,
    Courier,
    Customer,
    All
}