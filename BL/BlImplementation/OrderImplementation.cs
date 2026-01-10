namespace BlImplementation;

using BlApi;
using BO;
using Helpers;
using System;
using System.Collections.Generic;

/// <summary>
/// Implementation of the <see cref="IOrder"/> interface that provides business logic operations
/// for managing orders, including CRUD operations, courier assignment, delivery tracking, and observer notifications.
/// </summary>
/// <remarks>
/// This class acts as a facade that delegates all order-related operations to the <see cref="OrderManager"/>,
/// while providing a clean interface for the presentation layer. It supports observer patterns for both
/// list-level changes (when orders are added, removed, or modified) and entity-level changes (updates to specific orders).
/// </remarks>
internal class OrderImplementation : IOrder
{
    /// <summary>
    /// Registers an observer to be notified whenever the order list changes (orders added, removed, or modified).
    /// </summary>
    /// <param name="listObserver">An action to invoke when the order list is modified.</param>
    /// <remarks>
    /// Multiple observers can be registered and will be invoked in the order they were added.
    /// This supports the observer pattern for list-level changes.
    /// </remarks>
    public void AddObserver(Action listObserver) =>
        OrderManager.Observers.AddListObserver(listObserver);

    /// <summary>
    /// Registers an observer to be notified whenever a specific order is modified.
    /// </summary>
    /// <param name="id">The unique identifier of the order to observe.</param>
    /// <param name="observer">An action to invoke when the specified order is updated.</param>
    /// <remarks>
    /// Multiple observers for the same order ID can be registered and will be invoked in the order they were added.
    /// This supports the observer pattern for entity-level changes.
    /// </remarks>
    public void AddObserver(int id, Action observer) =>
        OrderManager.Observers.AddObserver(id, observer);

    /// <summary>
    /// Unregisters an observer that was previously registered for order list changes.
    /// </summary>
    /// <param name="listObserver">The observer action to remove.</param>
    /// <remarks>
    /// If the observer is not currently registered, this method has no effect.
    /// </remarks>
    public void RemoveObserver(Action listObserver) =>
        OrderManager.Observers.RemoveListObserver(listObserver);

    /// <summary>
    /// Unregisters an observer that was previously registered for a specific order.
    /// </summary>
    /// <param name="id">The unique identifier of the order being observed.</param>
    /// <param name="observer">The observer action to remove.</param>
    /// <remarks>
    /// If the observer is not currently registered, this method has no effect.
    /// </remarks>
    public void RemoveObserver(int id, Action observer) =>
        OrderManager.Observers.RemoveObserver(id, observer);

    /// <summary>
    /// Creates and adds a new order to the system.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation (typically a customer or manager).</param>
    /// <param name="order">The <see cref="Order"/> object containing the order details to be added.</param>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. The order will be assigned a unique ID
    /// and initial status of <see cref="OrderStatus.Pending"/>. All registered observers will be notified of this change.
    /// </remarks>
    public void AddOrder(int requesterId, Order order)
        => OrderManager.AddOrder(requesterId, order);

    /// <summary>
    /// Assigns an open order to a specific courier for delivery.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation (typically a dispatcher or manager).</param>
    /// <param name="orderId">The unique identifier of the order to assign.</param>
    /// <param name="courierId">The unique identifier of the courier to assign to the order.</param>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. The order's status is typically updated
    /// to <see cref="OrderStatus.Processing"/> upon successful assignment. All registered observers will be notified.
    /// </remarks>
    public void AssignOrderToCourier(int requesterId, int orderId, int courierId)
        => OrderManager.AssignOrderToCourier(requesterId, orderId, courierId);

    /// <summary>
    /// Cancels an open order, preventing it from being processed or delivered.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation.</param>
    /// <param name="orderId">The unique identifier of the order to cancel.</param>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. The order's status will be updated to
    /// <see cref="OrderStatus.Canceled"/>. This operation is typically only allowed for orders that have not
    /// yet been delivered. All registered observers will be notified.
    /// </remarks>
    public void CancelOrder(int requesterId, int orderId)
        => OrderManager.CancelOrder(requesterId, orderId);

    /// <summary>
    /// Marks a delivery as complete when a courier has successfully delivered an order.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation (typically the courier or supervisor).</param>
    /// <param name="courierId">The unique identifier of the courier who completed the delivery.</param>
    /// <param name="deliveryId">The unique identifier of the delivery record to mark as finished.</param>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. The delivery status is updated to reflect
    /// the completion state (e.g., <see cref="DeliveredStatus.Delivered"/>). All registered observers will be notified.
    /// </remarks>
    public void FinishOrder(int requesterId, int courierId, int deliveryId)
        => OrderManager.FinishOrder(requesterId, courierId, deliveryId);

    /// <summary>
    /// Retrieves a filtered and sorted list of closed (completed) deliveries for a specific courier.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation.</param>
    /// <param name="courierId">The unique identifier of the courier whose deliveries to retrieve.</param>
    /// <param name="filter">Optional filter by <see cref="OrderType"/>. Pass <c>null</c> to include all order types.</param>
    /// <param name="sorter">Optional sorting criteria. Pass <c>null</c> for default sorting.</param>
    /// <returns>An enumerable collection of <see cref="ClosedDeliveryInList"/> objects representing completed deliveries.</returns>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. Closed deliveries typically include those with
    /// status <see cref="DeliveredStatus.Delivered"/>, <see cref="DeliveredStatus.Rejected"/>, etc.
    /// </remarks>
    public IEnumerable<ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId, int courierId, OrderType? filter, Enum? sorter)
        => OrderManager.GetClosedDeliveriesForCourier(requesterId, courierId, filter, sorter);

    public IEnumerable<OpenOrderInList> GetOpenOrdersForCourier(int requesterId, int courierId, OrderType? filter, Enum? sorter)
        => OrderManager.GetOpenOrdersForCourier(requesterId, courierId, filter, sorter);

    /// <summary>
    /// Retrieves detailed information about a specific order.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation.</param>
    /// <param name="orderId">The unique identifier of the order to retrieve.</param>
    /// <returns>An <see cref="Order"/> object containing all details about the requested order.</returns>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. The requester must have appropriate
    /// permissions to view the order details. This method returns the complete order information including
    /// delivery address, type, and distance metrics.
    /// </remarks>
    public Order GetOrderDetails(int requesterId, int orderId)
        => OrderManager.GetOrderDetails(requesterId, orderId);

    /// <summary>
    /// Retrieves a summary of order IDs for a specific requester (typically counts or aggregate statistics).
    /// </summary>
    /// <param name="requesterId">The ID of the user for whom to retrieve order summary information.</param>
    /// <returns>An enumerable collection of order IDs related to the requester (e.g., orders they created or are associated with).</returns>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. The exact semantics of the summary
    /// depends on the requester's role (customer, courier, director, etc.).
    /// </remarks>
    public IEnumerable<int> GetOrdersBySummary(int requesterId)
        => OrderManager.GetOrderSummary(requesterId);

    /// <summary>
    /// Retrieves a filtered, optionally grouped, and sorted list of orders for a specific requester.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation.</param>
    /// <param name="filter">Optional filter criteria (e.g., by <see cref="OrderType"/>, status, etc.). Pass <c>null</c> to include all orders.</param>
    /// <param name="Object">Optional grouping object or parameter for organizing results. Pass <c>null</c> if no grouping is needed.</param>
    /// <param name="sorter">Optional sorting criteria (e.g., by status, date, distance, etc.). Pass <c>null</c> for default sorting.</param>
    /// <returns>An enumerable collection of <see cref="OrderInList"/> objects representing orders as lightweight list view models.</returns>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. This method returns lightweight list view models
    /// suitable for display in user interfaces. The specific orders returned depend on the requester's role and permissions.
    /// </remarks>
    public IEnumerable<OrderInList> orderInLists(int requesterId, Enum? filter, object? Object, Enum? sorter)
        => OrderManager.orderInLists(requesterId, filter, Object, sorter);

    /// <summary>
    /// Removes (deletes) an order from the system completely.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation (typically a manager or director).</param>
    /// <param name="orderId">The unique identifier of the order to remove.</param>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. The requester must have sufficient privileges to delete orders.
    /// This operation typically deletes the order record and associated delivery data. All registered observers will be notified.
    /// </remarks>
    public void RemoveOrder(int requesterId, int orderId)
        => OrderManager.RemoveOrder(requesterId, orderId);

    /// <summary>
    /// Updates the details of an existing order.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting this operation.</param>
    /// <param name="order">The <see cref="Order"/> object with updated information. Must include a valid order ID.</param>
    /// <remarks>
    /// Authorization is validated by the <see cref="OrderManager"/>. The requester must have appropriate permissions
    /// to modify the order. Updates typically allowed only for certain fields or statuses. All registered observers will be notified.
    /// </remarks>
    public void UpdateOrderDetails(int requesterId, Order order)
        => OrderManager.UpdateOrderDetails(requesterId, order);
}
