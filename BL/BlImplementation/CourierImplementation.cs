namespace BLImplementation;

using BlApi;
using BO;
using Helpers;
using System.Collections.Generic;

/// <summary>
/// Implementation of the <see cref="ICourier"/> interface that provides business logic operations
/// for managing couriers, including CRUD operations, authentication, and observer notifications.
/// </summary>
/// <remarks>
/// This class acts as a facade that delegates all courier-related operations to the <see cref="CourierManager"/>,
/// while providing a clean interface for the presentation layer. It supports observer patterns for both
/// list-level and entity-level changes.
/// </remarks>
internal class CourierImplementation : ICourier
{
    /// <summary>
    /// Registers an observer to be notified whenever the courier list changes.
    /// </summary>
    /// <param name="listObserver">An action to invoke when the courier list is modified.</param>
    /// <remarks>
    /// This method is part of stage 5 implementation. Multiple observers can be registered
    /// and will be invoked in the order they were added.
    /// </remarks>
    public void AddObserver(Action listObserver) =>
        CourierManager.Observers.AddListObserver(listObserver); //stage 5

    /// <summary>
    /// Registers an observer to be notified whenever a specific courier is modified.
    /// </summary>
    /// <param name="id">The unique identifier of the courier to observe.</param>
    /// <param name="observer">An action to invoke when the specified courier is updated.</param>
    /// <remarks>
    /// This method is part of stage 5 implementation. Multiple observers for the same courier ID
    /// can be registered and will be invoked in the order they were added.
    /// </remarks>
    public void AddObserver(int id, Action observer) =>
        CourierManager.Observers.AddObserver(id, observer); //stage 5

    /// <summary>
    /// Unregisters an observer that was previously registered for courier list changes.
    /// </summary>
    /// <param name="listObserver">The observer action to remove.</param>
    /// <remarks>
    /// This method is part of stage 5 implementation. If the observer is not currently registered,
    /// this method has no effect.
    /// </remarks>
    public void RemoveObserver(Action listObserver) =>
        CourierManager.Observers.RemoveListObserver(listObserver); //stage 5

    /// <summary>
    /// Unregisters an observer that was previously registered for a specific courier.
    /// </summary>
    /// <param name="id">The unique identifier of the courier being observed.</param>
    /// <param name="observer">The observer action to remove.</param>
    /// <remarks>
    /// This method is part of stage 5 implementation. If the observer is not currently registered,
    /// this method has no effect.
    /// </remarks>
    public void RemoveObserver(int id, Action observer) =>
        CourierManager.Observers.RemoveObserver(id, observer); //stage 5

    /// <summary>
    /// Adds a new courier to the system.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation (typically a manager or director).</param>
    /// <param name="newCourier">The courier object containing the details to be added.</param>
    /// <remarks>
    /// Authorization is typically validated by the <see cref="CourierManager"/>. The requester must have
    /// sufficient privileges to add new couriers. All registered observers will be notified of this change.
    /// </remarks>
    public void addCourier(int requesterId, Courier newCourier)
        => CourierManager.addCourier(requesterId, newCourier);

    /// <summary>
    /// Retrieves detailed information about a specific courier.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation.</param>
    /// <param name="courierId">The unique identifier of the courier to retrieve.</param>
    /// <returns>A <see cref="Courier"/> object containing all details about the requested courier.</returns>
    /// <remarks>
    /// Authorization is validated by the <see cref="CourierManager"/>. The requester must have appropriate
    /// permissions to view courier details.
    /// </remarks>
    public Courier GetCourierDetails(int requesterId, int courierId)
        => CourierManager.GetCourierDetails(requesterId, courierId);

    /// <summary>
    /// Retrieves a filtered list of couriers.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation.</param>
    /// <param name="isActive">Optional filter by active status. Pass <c>null</c> to include both active and inactive couriers.</param>
    /// <param name="status">Optional filter by administrator status (e.g., Director, Courier, Customer). Pass <c>null</c> to include all statuses.</param>
    /// <returns>An enumerable collection of <see cref="CourierInList"/> objects matching the specified criteria.</returns>
    /// <remarks>
    /// This method returns lightweight list view models suitable for display in user interfaces.
    /// Authorization is validated by the <see cref="CourierManager"/>.
    /// </remarks>
    public IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, Enum? status)
        => CourierManager.GetCouriersList(requesterId, isActive, status);

    /// <summary>
    /// Authenticates a user (director or courier) with their credentials.
    /// </summary>
    /// <param name="Id">The unique identifier of the user attempting to log in.</param>
    /// <param name="password">The password provided by the user for authentication.</param>
    /// <returns>An <see cref="Administrator"/> enum value indicating the user's role (Director, Courier, or Customer).</returns>
    /// <remarks>
    /// This method validates the provided credentials against stored authentication data. The returned value
    /// indicates the user's access level in the system.
    /// </remarks>
    public BO.Administrator Login(int Id, string password)
        => CourierManager.Login(Id, password);

    /// <summary>
    /// Removes a courier from the system by deactivating or deleting the record.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation (typically a manager or director).</param>
    /// <param name="courierId">The unique identifier of the courier to remove.</param>
    /// <remarks>
    /// Authorization is typically validated by the <see cref="CourierManager"/>. The requester must have
    /// sufficient privileges to remove couriers. All registered observers will be notified of this change.
    /// </remarks>
    public void removeCourier(int requesterId, int courierId)
        => CourierManager.removeCourier(requesterId, courierId);

    /// <summary>
    /// Updates the details of an existing courier.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation.</param>
    /// <param name="updatedCourier">The courier object with updated information.</param>
    /// <remarks>
    /// Authorization is validated by the <see cref="CourierManager"/>. The requester must have appropriate
    /// permissions to modify courier information. All registered observers will be notified of this change.
    /// </remarks>
    public void UpdateCourier(int requesterId, Courier updatedCourier)
        => CourierManager.UpdateCourier(requesterId, updatedCourier);

    /// <summary>
    /// Promotes a courier to director status, granting them elevated privileges and responsibilities.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this promotion (typically an existing director).</param>
    /// <param name="courierId">The unique identifier of the courier to promote.</param>
    /// <remarks>
    /// This operation typically requires director or administrator privileges for the requester.
    /// The courier's role will be changed from <see cref="Administrator.Courier"/> to <see cref="Administrator.Director"/>.
    /// All registered observers will be notified of this change.
    /// </remarks>
    public void PromoteToDirector(int requesterId, int courierId)
        => CourierManager.PromoteCourierToDirector(requesterId, courierId);
}