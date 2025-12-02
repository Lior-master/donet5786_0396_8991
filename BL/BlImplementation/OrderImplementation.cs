using BLApi;
using BO;
using Helpers;

namespace BlImplementation;

internal class OrderImplementation : IOrder
{
    public void AssignOrderToCourier(int courierId, int orderId)
        => OrderManager.AssignOrderToCourier(courierId, orderId);

    public int Create(Order order)
        => OrderManager.Create(order);

    public void Delete(int id)
        => OrderManager.Delete(id);

    public void FinishDelivery(int courierId, int orderId, DeliveredStatus status)
        => OrderManager.FinishDelivery(courierId, orderId, status);

    public OrderInProgress? GetCurrentOrderForCourier(int courierId)
        => OrderManager.GetCurrentOrderForCourier(courierId);

    public IEnumerable<ClosedDeliveryInList> GetDeliveryHistory(int courierId)
        => OrderManager.GetDeliveryHistory(courierId);

    public IEnumerable<OpenOrderInList> GetOpenOrdersForCourier(int courierId)
        => OrderManager.GetOpenOrdersForCourier(courierId);

    public Order Read(int id)
        => OrderManager.Read(id);

    public IEnumerable<OrderInList> ReadAll()
        => OrderManager.ReadAll();

    public void Update(Order order)
        => OrderManager.Update(order);
}
