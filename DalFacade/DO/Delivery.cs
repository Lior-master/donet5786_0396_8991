namespace DO
{
    /// <summary>
    /// Represents a delivery assignment linking an order to a courier, with timing,
    /// distance and transport metadata used to track and manage the delivery lifecycle.
    /// </summary>
    /// <param name="Id">Unique identifier of the delivery.</param>
    /// <param name="OrderId">Identifier of the associated order.</param>
    /// <param name="CourierId">Identifier of the assigned courier.</param>
    /// <param name="PickupTime">
    /// Scheduled or actual pickup time for the delivery. Prefer using UTC to avoid
    /// ambiguity across services; callers should normalize to UTC when appropriate.
    /// </param>
    /// <param name="ArrivalTime">Optional arrival / completion time. Null until delivery is completed or recorded.</param>
    /// <param name="Distance">Optional measured distance of the delivery in kilometers. Null when not measured.</param>
    /// <param name="Transport">Transport mode used for the delivery. See <see cref="DeliveryTransport"/>.</param>
    /// <param name="Status">Current delivery status. See <see cref="OrderStatus"/>; null if not set.</param>
    /// <remarks>
    /// - Nullable fields (<see cref="ArrivalTime"/>, <see cref="Distance"/>, <see cref="Status"/>) indicate values
    ///   that may not be known at creation time and can be populated later.
    /// - Use UTC for time fields across services to avoid timezone issues.
    /// - Validate and sanitize any external input used to populate address or distance-related fields before persisting.
    /// </remarks>
    public record Delivery
    (
        int Id,
        int OrderId,
        DeliveryTransport Transport,
        int CourierId,
        DateTime PickupTime,
        DateTime? ArrivalTime = null,
        double? Distance = null,
        DeliveredStatus? DeliveredStatus = null
    )
    {
        /// <summary>
        /// Initializes a new instance of <see cref="Delivery"/> with default values.
        /// </summary>
        /// <remarks>
        /// Defaults:
        /// - Id = 0
        /// - OrderId = 0
        /// - CourierId = 0
        /// - PickupTime = <see cref="DateTime.Now"/> (consider using UTC in callers)
        /// - ArrivalTime = null
        /// - Distance = null
        /// - Transport = <see cref="DeliveryTransport.Car"/>
        /// - Status = <see cref="DeliveredStatus.Pending"/> (may be null depending on caller)
        /// </remarks>
        public Delivery() : this(0, 0,DeliveryTransport.Car, 0, DateTime.Now) { }
    }
}