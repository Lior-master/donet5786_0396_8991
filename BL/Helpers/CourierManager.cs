using DalApi;
using DO;
using System.Linq;
// Add the following using directive if GeoManager is defined in another namespace
// using <NamespaceWhereGeoManagerIsDefined>;

namespace Helpers;

internal static class CourierManager
{
    private static readonly IDal s_dal = Factory.Get;

    /// <summary>
    /// Called from AdminManager.UpdateClock after every clock update.
    /// Deactivates couriers whose last delivery is older than Config.Inactivity.
    /// </summary>
    internal static void PeriodicCouriersUpdates(DateTime oldClock, DateTime newClock)
    {
        TimeSpan inactivityThreshold = s_dal.Config.Inactivity;

        var allDeliveries = s_dal.Delivery.ReadAll();

        _ = s_dal.Courier.ReadAll()
            .Where(c => c.IsActive)
            .Select(c => new
            {
                Courier = c,
                LastArrival =
                    allDeliveries
                        .Where(d => d.CourierId == c.Id && d.ArrivalTime != null)
                        .OrderByDescending(d => d.ArrivalTime)
                        .Select(d => d.ArrivalTime)
                        .FirstOrDefault()
            })
            .Where(x => x.LastArrival != null)
            .Where(x =>
                (oldClock - x.LastArrival!.Value) <= inactivityThreshold &&
                (newClock - x.LastArrival!.Value) > inactivityThreshold)
            .Select(x =>
            {
                var updated = x.Courier with { IsActive = false };
                s_dal.Courier.Update(updated);
                return updated;
            })
            .ToList();
    }
    internal static void Create(Courier courier)
    {
        s_dal.Courier.Create(courier);
    }
    internal static Courier Read(int id)
    {
        return s_dal.Courier.Read(id);
    }
    internal static IEnumerable<BO.CourierInList> ReadAll()
    {
        return s_dal.Courier.ReadAll();
    }
    internal static void Update(BO.Courier courier)
    {
        s_dal.Courier.Update(courier);
    }
    internal static void Delete(int id)
    {
        s_dal.Courier.Delete(id);
    }
    internal static IEnumerable<BO.OpenOrderInList> GetEligibleOrders(int courierId)
    {
        // If GeoManager is a static class in your project, ensure it is defined and accessible.
        // If it is missing, you need to provide its implementation or clarify its location.
        // If you have a file or namespace for GeoManager, add the appropriate using statement above.

        return s_dal.Order.ReadAll()
            .Where(o => o.Status == OrderStatus.Pending)
            .Select(o => new BO.OpenOrderInList
            {
                CourierId = courierId,
                OrderId = o.Id,
                OrderType = (BO.OrderType)o.Status,
                Fragility = o.Fragility != null ? (BO.FragilityLevel?)o.Fragility : null,
                CustomerAddress = o.CustomerAddress,
                BirdDistance = Tools.BirdDistance(s_dal.Config.Latitude, s_dal.Config.Longitude, o.Latitude, o.Longitude),
                Distance = null,
                AddedTime = AdminManager.Now - o.OrderDate,
                ScheduleStatus = o.ScheduleStatus,
                EstimatedDeliveryTime = DeliveryManager.EstimateDeliveryTime(
                    courierId,
                    o.Id),
            });
    }
    internal static void TakeOrder(int courierId, int orderId)
    {
        var order = s_dal.Order.Read(orderId);
        if (order.Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException("Order is not ready for delivery.");
        }
        order = order with
        {
            Status = OrderStatus.Pending,
            CourierId = courierId,
            DepartureTime = AdminManager.Now
        };
        s_dal.Order.Update(order);
    }
    internal static OrderInProgress? GetCurrentOrder(int courierId)
    {
        var order = s_dal.Order.ReadAll()
            .FirstOrDefault(o => o.CourierId == courierId && o.Status == OrderStatus.OutForDelivery);
        if (order == null)
            return null;
        var customer = s_dal.Customer.Read(order.CustomerId);
        return new OrderInProgress
        {
            Id = order.Id,
            CustomerName = customer.Name,
            CustomerAddress = customer.Address,
            Weight = order.Weight,
            Priority = order.Priority,
            DepartureTime = order.DepartureTime.Value
        };
    }
    internal static IEnumerable<ClosedDeliveryInList> GetHistory(int courierId)
    {
        return s_dal.Delivery.ReadAll()
            .Where(d => d.CourierId == courierId && d.ArrivalTime != null)
            .Select(d =>
            {
                var order = s_dal.Order.Read(d.OrderId);
                var customer = s_dal.Customer.Read(order.CustomerId);
                return new ClosedDeliveryInList
                {
                    OrderId = order.Id,
                    CustomerName = customer.Name,
                    Weight = order.Weight,
                    Priority = order.Priority,
                    DepartureTime = order.DepartureTime.Value,
                    ArrivalTime = d.ArrivalTime.Value,
                    DeliveredStatus = d.DeliveredStatus
                };
            });
    }

}
