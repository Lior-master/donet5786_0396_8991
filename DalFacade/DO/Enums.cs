namespace DO;

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
    Failed
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
    Foot
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
    Dessert
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
    Critical
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
    ExtremelyFragile
}


/// <summary>
/// Defines supported administrator values.
/// </summary>
public enum Administrator
{
    Director,
    Courier,
    Customer
}