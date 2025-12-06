using BO;
using DalApi;
using DO;
using System;
using System.Linq;

namespace Helpers;

internal static class OrderManager
{
    private static readonly IDal s_dal = Factory.Get;
    internal static IEnumerable<int> GetOrderSummary(int requesterId)
    {
        // Validate requester
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester does not exist.");

        var orders = s_dal.Order.ReadAll();

        int statusCount = Enum.GetValues(typeof(BO.OrderStatus)).Length;
        int[] summary = new int[statusCount];

        // Group orders by OrderStatus
        var groups = orders.GroupBy(o => o.Status);

        foreach (var g in groups)
        {
            summary[(int)g.Key] = g.Count();
        }

        return summary; // int[] is implicitly convertible to IEnumerable<int>
    }

    internal static IEnumerable<BO.OrderInList> orderInLists(int requesterId,Enum? filter,object? Object,Enum? sorter)
    {
        // 1. Validation of requesterId
        try
        {
            var requester = s_dal.Courier.Read(requesterId);
        }
        catch (Exception ex)
        {
            throw new BLNotFoundException("Requester does not exist." , ex);
        }

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

    internal static IEnumerable<BO.OrderInList> GetOrdersList(int requesterId, BO.OrderStatus? statusFilter, object? sortParameter)
    {
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester does not exist.");

        var config = AdminManager.GetConfig();
        var ordersDO = s_dal.Order.ReadAll().ToList();
        var deliveriesDO = s_dal.Delivery.ReadAll().ToList();

        var list = ordersDO.Select(order =>
        {
            var orderDeliveries = deliveriesDO
                .Where(d => d.OrderId == order.Id)
                .ToList();

            var lastDelivery = orderDeliveries
                .OrderByDescending(d => d.PickupTime)
                .FirstOrDefault();

            int? deliveryId = lastDelivery?.Id;

            // Coordinates
            double lat = order.Latitude ?? 0;
            double lon = order.Longitude ?? 0;

            if (lat == 0 && lon == 0)
            {
                var coords = Tools.GetCoordinatesFromAddressAsync(order.CustomerAddress).Result;
                lat = coords.Latitude;
                lon = coords.Longitude;
            }

            double distance = Tools.BirdDistance(
                config.CompanyLatitude,
                config.CompanyLongitude,
                lat,
                lon
            );

            DateTime? estArrival = distance > 0
                ? Tools.CalculateEstimatedArrival(order.OrderDate, distance, config.CarSpeed)
                : null;

            DateTime? maxArrival = estArrival?.Add(config.RiskRange);
            DateTime? realArrival = lastDelivery?.ArrivalTime;

            // REAL ORDER STATUS computed from deliveries
            var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);

            // REAL ScheduleStatus
            var schedule = Tools.CalculateScheduleStatus(
                orderStatus,
                order.OrderDate,
                estArrival,
                maxArrival,
                realArrival
            );

            return new BO.OrderInList
            {
                DeliveryId = deliveryId,
                OrderId = order.Id,
                Type = (BO.OrderType)order.Type,
                Distance = distance,
                Status = orderStatus,
                ScheduleStatus = schedule,
                OrderEndTime = realArrival != null ? realArrival.Value - order.OrderDate : DateTime.Now - order.OrderDate,
                TreatmentEndTime = lastDelivery != null ? lastDelivery.PickupTime - order.OrderDate : TimeSpan.Zero,
                NumberOfCouriers = orderDeliveries.Select(d => d.CourierId).Distinct().Count()
            };

        }).ToList();

        return list;
    }

    internal static BO.Order GetOrderDetails(int requesterId,int orderId)
    {
        throw new NotImplementedException();
    }
    internal static void UpdateOrderDetails(int requesterId, BO.Order order)
    {
        // Map BO.Order to DO.Order before passing to DAL
        var doOrder = new DO.Order
        {
            Id = order.Id,
            CustomerName = order.CustomerName,
            CustomerAddress = order.CustomerAddress,
            CustomerPhone = order.CustomerPhone,
            OrderDate = order.OrderDate,
            size = order.Volume,
            weight = order.Weight,
            Latitude = order.Latitude,
            Longitude = order.Longitude,
            Description = order.OrderDescription
        };
        s_dal.Order.Update(doOrder);
    }
    internal static void CancelOrder(int requesterId,int orderId)
    {
        s_dal.Order.Delete(orderId);

    }
    internal static void RemoveOrder(int requesterId,int orderId)
    {
        throw new NotImplementedException();
    }
    internal static void AddOrder(int requesterId, BO.Order order)
    {
        // Map BO.Order to DO.Order before passing to DAL
        var doOrder = new DO.Order
        {
            Id = order.Id,
            CustomerName = order.CustomerName,
            CustomerAddress = order.CustomerAddress,
            CustomerPhone = order.CustomerPhone,
            OrderDate = order.OrderDate,
            size = order.Volume,
            weight = order.Weight,
            Latitude = order.Latitude,
            Longitude = order.Longitude,
            Description = order.OrderDescription
        };
        s_dal.Order.Create(doOrder);
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
