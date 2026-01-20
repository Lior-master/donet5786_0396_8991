namespace BlApi;

using BO;
using System.Runtime.InteropServices.ObjectiveC;
using System.Threading.Tasks;

public interface IOrder : IObservable 
{
    Task<IEnumerable<int>> GetOrdersBySummaryAsync(int requesterId);
    Task<IEnumerable<BO.OrderInList>> orderInListsAsync(int requesterId,Enum? filter,object? Object,Enum? sorter);
    Task<BO.Order> GetOrderDetailsAsync(int requesterId,int orderId);
    Task UpdateOrderDetailsAsync(int requesterId,BO.Order order);
    void CancelOrder(int requesterId,int orderId);
    void RemoveOrder(int requesterId,int orderId);
    Task AddOrderAsync(int requesterId,BO.Order order);
    Task FinishOrderAsync(int requesterId,int courierId,int deliveryId, BO.DeliveredStatus deliveredStatus);
    Task AssignOrderToCourierAsync(int requesterId,int orderId,int courierId);
    Task<BO.OrderInProgress> GetOrderInProgressSnapshotAsync(int requesterId, int courierId, int orderId);
    IEnumerable<BO.ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId,int courierId,OrderType? filter,Enum? sorter);
    Task<IEnumerable<BO.OpenOrderInList>> GetOpenOrdersForCourierAsync(int requesterId,int courierId, OrderType? filter,Enum? sorter);
    Task<IEnumerable<BO.OrderInList>> orderInListsDoubleFilterAsync(int requesterId, Enum? filter1, Enum? filter2);

}
