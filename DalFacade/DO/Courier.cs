using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DO;
/// <summary>
/// Represents a courier who performs deliveries.
/// </summary>
/// <param name="Id">Unique identifier of the courier.</param>
/// <param name="Name">Full name of the courier.</param>
/// <param name="Phone">Primary contact phone number (stored as string).</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Password">Authentication credential for the courier (store securely; prefer hashing).</param>
/// <param name="IsActive">Indicates whether the courier is currently active and available for assignments.</param>
/// <param name="Transport">Primary transport mode used by the courier. See <see cref="DeliveryTransport"/>.</param>
/// <param name="MaxDistance">
/// Optional maximum delivery distance (in kilometers) that the courier is willing to travel.
/// A value of <c>null</c> means no explicit maximum distance is set.
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
    double? MaxDistance = null
)
{
    /// <summary>
    /// Initializes a new instance of <see cref="Courier"/> with default values.
    /// </summary>
    /// <remarks>
    /// Defaults: Id = 0, Name = empty, Phone = empty, Email = empty, Password = empty,
    /// IsActive = false, Transport = DeliveryTransport.Car.
    /// </remarks>
    public Courier() : this(0, "", "", "", "", false, DeliveryTransport.Car) { }
}