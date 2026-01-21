using BO;
using System.Threading.Tasks;
/// <summary>
/// Defines public business-logic abstractions and contracts used by the presentation layer.
/// </summary>
namespace BlApi;

// Specify the type parameter for IObservable<T>.
// Assuming you want to observe Courier changes, use Courier as the type argument.
/// <summary>
/// Defines the contract for courier operations.
/// </summary>
public interface ICourier : IObservable
{
    /// <summary>
    /// Login.
    /// </summary>
    /// <param name="Id">The id value.</param>
    /// <param name="password">The password value.</param>
    /// <returns>The operation result.</returns>
    Administrator Login(int Id, string password);
    /// <summary>
    /// Gets the couriers list value.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="isActive">The is active value.</param>
    /// <param name="status">The status value.</param>
    /// <returns>The operation result.</returns>
    IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, Enum? status);
    /// <summary>
    /// Asynchronously gets the courier details value.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="courierId">The courier id value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task<BO.Courier> GetCourierDetailsAsync(int requesterId, int courierId);
    /// <summary>
    /// Updates the courier.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="updatedCourier">The updated courier value.</param>
    void UpdateCourier(int requesterId, Courier updatedCourier);
    /// <summary>
    /// Remove Courier.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="courierId">The courier id value.</param>
    void removeCourier(int requesterId, int courierId);
    /// <summary>
    /// Add Courier.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="newCourier">The new courier value.</param>
    void addCourier(int requesterId, Courier newCourier);
    /// <summary>
    /// Promote To Director.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="courierId">The courier id value.</param>
    void PromoteToDirector(int requesterId, int courierId);
}
