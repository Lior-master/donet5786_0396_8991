namespace BlImplementation;

using BlApi;
using BO;
using Helpers;
using System;
using System.Collections.Generic;


internal class OrderImplementation : IOrder
{
    public void AddOrder(int requesterId, Order order)
    {
        throw new NotImplementedException();
    }

    public void AssignOrderToCourier(int requesterId, int orderId, int courierId)
    {
        throw new NotImplementedException();
    }

    public void CancelOrder(int requesterId, int orderId)
    {
        throw new NotImplementedException();
    }

    public void FinishOrder(int requesterId, int courierId, int deliveryId)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId, int courierId, OrderType? filter, Enum? sorter)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<OpenOrderInList> GetOpenOrdersForCourier(int requesterId, int courierId, OrderType? filter, DeliveredStatus? sorter)
    {
        throw new NotImplementedException();
    }

    public Order GetOrderDetails(int requesterId, int orderId)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<int> GetOrdersBySummary(int requesterId)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<OrderInList> orderInLists(int requesterId, Enum? filter, object? Object, Enum? sorter)
    {
        throw new NotImplementedException();
    }

    public void RemoveOrder(int requesterId, int orderId)
    {
        throw new NotImplementedException();
    }

    public void UpdateOrderDetails(int requesterId, Order order)
    {
        throw new NotImplementedException();
    }
}
