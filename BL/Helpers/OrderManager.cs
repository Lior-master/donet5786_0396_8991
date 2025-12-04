using BO;
using DalApi;
using DO;
using System;
using System.Linq;

namespace Helpers;

internal static class OrderManager
{
    private static readonly IDal s_dal = Factory.Get;
    internal static IEnumerable<BO.Order> GetOrders(int requesterId)
    {
        return s_dal.Order.Read(o => o.OrderId == requesterId);
    }
    internal static IEnumerable<BO.OrderInList> orderInLists(int requesterId,Enum? filter,object? Object,Enum? sorter)
    {
        // 1. TODO : vérifier permissions pour requesterId

        // 2. Lire toutes les commandes DO
        var doOrders = s_dal.Order.ReadAll();

        // 3. TODO : convertir DO.Order → BO.OrderInList
        // Je ne peux PAS inventer la structure de BO.OrderInList,
        // donc je laisse un constructeur vide à remplir par toi.
        var list = doOrders.Select(doOrder =>
        {
            return new BO.OrderInList
            {
                // TODO : remplir selon TA classe BO.OrderInList
            };
        });

        // 4. Filtrage (exactement comme l’image le demande)
        if (filter != null && Object != null)
        {
            // TODO : appliquer le filtre selon 'filter' et selon 'Object'
        }

        // 5. Tri (exactement comme l’image le demande)
        if (sorter != null)
        {
            // TODO : appliquer le tri selon 'sorter'
        }

        return list;
    }

    internal static BO.Order GetOrderDetails(int requesterId,int orderId)
    {
        throw new NotImplementedException();
    }
    internal static void UpdateOrderDetails(int requesterId,BO.Order order)
    {
        throw new NotImplementedException();
    }
    internal static void CancelOrder(int requesterId,int orderId)
    {
        throw new NotImplementedException();
    }
    internal static void RemoveOrder(int requesterId,int orderId)
    {
        throw new NotImplementedException();
    }
    internal static int AddOrder(int requesterId,BO.Order order)
    {
        throw new NotImplementedException();
    }
    internal static void FinishOrder(int requesterId,int courierId,int deliveryId)
    {
        throw new NotImplementedException();
    }
    internal static void AssignOrderToCourier(int requesterId,int orderId,int courierId)
    {
        throw new NotImplementedException();
    }
    internal static IEnumerable<BO.ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId,int courierId,OrderType? filter,Enum? sorter)
    {
        throw new NotImplementedException();
    }
    internal static IEnumerable<BO.OpenOrderInList> GetOpenOrdersForCourier(int requesterId,int courierId, OrderType? filter,DeliveredStatus? sorter)
    {
        throw new NotImplementedException();
    }

}
