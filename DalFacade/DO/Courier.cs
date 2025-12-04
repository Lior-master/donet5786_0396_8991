namespace DO;

/// <summary>
/// Represents a courier who performs deliveries.
/// This is an immutable positional record: use the primary constructor or
/// the generated `with` expression to create modified copies.
/// </summary>
/// <param name="Id">Unique identifier of the courier (database id or similar).</param>
/// <param name="Name">Full name of the courier. Use this for display and logging.</param>
/// <param name="Phone">Primary contact phone number (stored as string). Prefer E.164 format.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Password">
/// Authentication credential for the courier. Do NOT store plain-text passwords.
/// Store a securely salted hash instead and treat this field as the hashed value.
/// </param>
/// <param name="IsActive">
/// Indicates whether the courier is currently active and available in the compagny.
/// A value of <c>false</c> typically means the courier should not receive new orders.
/// </param>
/// <param name="Transport">Primary transport mode used by the courier. See <see cref="DeliveryTransport"/>.</param>
/// <param name="StartDate">
/// Date and time when the courier started working with the system (UTC preferred).
/// Used for tenure, scheduling and historical calculations.
/// </param>
/// <param name="MaxDistance">
/// Optional maximum delivery distance (in kilometers) that the courier is willing to travel.
/// A value of <c>null</c> means no explicit maximum distance is set .
/// </param>
public record Courier
(
    int Id,
    string Name,
    string Phone,
    string Email,
    string Password,
    bool IsActive,
    DeliveryTransport Transport,
    DateTime StartDate,
    Administrator Administrator,
    double? MaxDistance = null
)
{
    /// <summary>
    /// Initializes a new instance of <see cref="Courier"/> with default values.
    /// </summary>
    /// <remarks>
    /// Defaults:
    /// - Id = 0
    /// - Name, Phone, Email, Password = empty string
    /// - IsActive = false
    /// - Transport = <see cref="DeliveryTransport.Car"/>
    /// - StartDate = <see cref="DateTime.Now"/> (consider using UTC in callers)
    /// - MaxDistance = null
    /// </remarks>
    public Courier() : this(0, string.Empty, string.Empty, string.Empty, string.Empty, false, DeliveryTransport.Car, DateTime.Now) { }
}