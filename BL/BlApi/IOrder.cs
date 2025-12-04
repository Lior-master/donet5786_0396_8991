namespace BlApi;

using BO;
using System.Runtime.InteropServices.ObjectiveC;

public interface IOrder
{
    IEnumerable<BO.Order> GetOrders(int requesterId);
    IEnumerable<BO.OrderInList> orderInLists(int requesterId,Enum? filter,object? Object,Enum? sorter);
    BO.Order GetOrderDetails(int requesterId,int orderId);
    void UpdateOrderDetails(int requesterId,BO.Order order);
    void CancelOrder(int requesterId,int orderId);
    void RemoveOrder(int requesterId,int orderId);
    void AddOrder(int requesterId,BO.Order order);
    void FinishOrder(int requesterId,int courierId,int deliveryId);
    void AssignOrderToCourier(int requesterId,int orderId,int courierId);
    IEnumerable<BO.ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId,int courierId,OrderType? filter,Enum? sorter);
    IEnumerable<BO.OpenOrderInList> GetOpenOrdersForCourier(int requesterId,int courierId, OrderType? filter,DeliveredStatus? sorter);
}
