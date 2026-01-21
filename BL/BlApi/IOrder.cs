namespace BlApi;

using BO;
using System.Runtime.InteropServices.ObjectiveC;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for order operations.
/// </summary>
public interface IOrder : IObservable 
{
    /// <summary>
    /// Asynchronously gets the orders by summary value.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task<IEnumerable<int>> GetOrdersBySummaryAsync(int requesterId);
    /// <summary>
    /// Asynchronously order In Lists.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="filter">The filter value.</param>
    /// <param name="Object">The object value.</param>
    /// <param name="sorter">The sorter value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task<IEnumerable<BO.OrderInList>> orderInListsAsync(int requesterId,Enum? filter,object? Object,Enum? sorter);
    /// <summary>
    /// Asynchronously gets the order details value.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="orderId">The order id value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task<BO.Order> GetOrderDetailsAsync(int requesterId,int orderId);
    /// <summary>
    /// Asynchronously updates the order details.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="order">The order value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task UpdateOrderDetailsAsync(int requesterId,BO.Order order);
    /// <summary>
    /// Cancel Order.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="orderId">The order id value.</param>
    void CancelOrder(int requesterId,int orderId);
    /// <summary>
    /// Removes the order.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="orderId">The order id value.</param>
    void RemoveOrder(int requesterId,int orderId);
    /// <summary>
    /// Asynchronously adds the order.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="order">The order value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task AddOrderAsync(int requesterId,BO.Order order);
    /// <summary>
    /// Asynchronously finish Order.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="courierId">The courier id value.</param>
    /// <param name="deliveryId">The delivery id value.</param>
    /// <param name="deliveredStatus">The delivered status value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task FinishOrderAsync(int requesterId,int courierId,int deliveryId, BO.DeliveredStatus deliveredStatus);
    /// <summary>
    /// Asynchronously assign Order To Courier.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="orderId">The order id value.</param>
    /// <param name="courierId">The courier id value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task AssignOrderToCourierAsync(int requesterId,int orderId,int courierId);
    /// <summary>
    /// Asynchronously gets the order in progress snapshot value.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="courierId">The courier id value.</param>
    /// <param name="orderId">The order id value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task<BO.OrderInProgress> GetOrderInProgressSnapshotAsync(int requesterId, int courierId, int orderId);
    /// <summary>
    /// Gets the closed deliveries for courier value.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="courierId">The courier id value.</param>
    /// <param name="filter">The filter value.</param>
    /// <param name="sorter">The sorter value.</param>
    /// <returns>The operation result.</returns>
    IEnumerable<BO.ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId,int courierId,OrderType? filter,Enum? sorter);
    /// <summary>
    /// Asynchronously gets the open orders for courier value.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="courierId">The courier id value.</param>
    /// <param name="filter">The filter value.</param>
    /// <param name="sorter">The sorter value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task<IEnumerable<BO.OpenOrderInList>> GetOpenOrdersForCourierAsync(int requesterId,int courierId, OrderType? filter,Enum? sorter);
    /// <summary>
    /// Asynchronously order In Lists Double Filter.
    /// </summary>
    /// <param name="requesterId">The requester id value.</param>
    /// <param name="filter1">The filter 1 value.</param>
    /// <param name="filter2">The filter 2 value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task<IEnumerable<BO.OrderInList>> orderInListsDoubleFilterAsync(int requesterId, Enum? filter1, Enum? filter2);

}
