namespace BlImplementation;

using BlApi;
using BO;
using Helpers;
using System;
using System.Collections.Generic;


internal class OrderImplementation : IOrder
{
    public void AddObserver(Action listObserver) =>
        OrderManager.Observers.AddListObserver(listObserver);
    public void AddObserver(int id, Action observer) =>
        OrderManager.Observers.AddObserver(id, observer);
    public void RemoveObserver(Action listObserver) =>
        OrderManager.Observers.RemoveListObserver(listObserver);
    public void RemoveObserver(int id, Action observer) =>
        OrderManager.Observers.RemoveObserver(id, observer);

    public void AddOrder(int requesterId, Order order)
        => OrderManager.AddOrder(requesterId, order);

    public void AssignOrderToCourier(int requesterId, int orderId, int courierId)
        => OrderManager.AssignOrderToCourier(requesterId, orderId, courierId);

    public void CancelOrder(int requesterId, int orderId)
        => OrderManager.CancelOrder(requesterId, orderId);

    public void FinishOrder(int requesterId, int courierId, int deliveryId)
        => OrderManager.FinishOrder(requesterId, courierId, deliveryId);

    public IEnumerable<ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId, int courierId, OrderType? filter, Enum? sorter)
        => OrderManager.GetClosedDeliveriesForCourier(requesterId, courierId, filter, sorter);

    public IEnumerable<OpenOrderInList> GetOpenOrdersForCourier(int requesterId, int courierId, OrderType? filter, Enum? sorter)
        => OrderManager.GetOpenOrdersForCourier(requesterId, courierId, filter, sorter);

    public Order GetOrderDetails(int requesterId, int orderId)
        => OrderManager.GetOrderDetails(requesterId, orderId);

    public IEnumerable<int> GetOrdersBySummary(int requesterId)
        => OrderManager.GetOrderSummary(requesterId);

    public IEnumerable<OrderInList> orderInLists(int requesterId, Enum? filter, object? Object, Enum? sorter)
        => OrderManager.orderInLists(requesterId, filter, Object, sorter);

    public void RemoveOrder(int requesterId, int orderId)
        => OrderManager.RemoveOrder(requesterId, orderId);

    public void UpdateOrderDetails(int requesterId, Order order)
        => OrderManager.UpdateOrderDetails(requesterId, order);
}
