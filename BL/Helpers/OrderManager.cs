using BO;
using DalApi;
using DO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Helpers;

internal static class OrderManager
{
    private static readonly IDal s_dal = Factory.Get;

    internal static void PeriodicOrdersUpdates(DateTime oldClock, DateTime newClock)
    {
        // Read config values once
        TimeSpan maxDeliveryTime = s_dal.Config.MaxTimeDelivery;
        TimeSpan riskRange = s_dal.Config.RiskRange;

        // Read all deliveries once to avoid multiple DAL calls
        var deliveriesAll = s_dal.Delivery.ReadAll().ToList();

        // Iterate over a snapshot of orders
        foreach (var o in s_dal.Order.ReadAll().ToList())
        {
            // deliveries for this order
            var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();

            // 1) Start deliveries whose pickup time was reached (Status == null -> Processing)
            foreach (var d in orderDeliveries.Where(d => d.Status == null && d.PickupTime > oldClock && d.PickupTime <= newClock))
            {
                var upd = d with { Status = DO.OrderStatus.Processing };
                s_dal.Delivery.Update(upd);
            }

            // 2) Cancel processing deliveries that exceed maxDeliveryTime (best-effort)
            //    We use order.OrderDate as the reference for allowed delivery window.
            foreach (var d in orderDeliveries.Where(d => d.Status == DO.OrderStatus.Processing && d.ArrivalTime == null))
            {
                if (newClock - o.OrderDate > maxDeliveryTime)
                {
                    var upd = d with
                    {
                        Status = DO.OrderStatus.Canceled,
                        ArrivalTime = newClock // mark finished so it won't be considered open anymore
                    };
                    s_dal.Delivery.Update(upd);
                }
            }

            // 3) Compute in-memory order status (BO) using deliveries + time-based escalation
            //    We DO NOT persist this on DO.Order (user requested no change to DO.Order).
            var deliveryBasedStatus = Tools.CalculateOrderStatus(orderDeliveries); // BO.OrderStatus
            TimeSpan elapsed = newClock - o.OrderDate;
            BO.OrderStatus timeBasedStatus = BO.OrderStatus.Pending;

            if (elapsed > maxDeliveryTime)
            {
                timeBasedStatus = BO.OrderStatus.Canceled;
            }
            else if (elapsed >= (maxDeliveryTime - riskRange))
            {
                if (deliveryBasedStatus == BO.OrderStatus.Pending)
                    timeBasedStatus = BO.OrderStatus.Processing;
                else
                    timeBasedStatus = deliveryBasedStatus;
            }
            else
            {
                timeBasedStatus = BO.OrderStatus.Pending;
            }

            BO.OrderStatus finalStatus;
            // terminal statuses from deliveries take precedence
            if (deliveryBasedStatus == BO.OrderStatus.Delivered
                || deliveryBasedStatus == BO.OrderStatus.Returned
                || deliveryBasedStatus == BO.OrderStatus.Canceled)
            {
                finalStatus = deliveryBasedStatus;
            }
            else
            {
                // otherwise use the time-based computed status
                finalStatus = timeBasedStatus;
            }

            // 4) No persistence to DO.Order (by design). If you need the DAL to store order status
            //    instead, we must add a property to DO.Order or store a sentinel delivery — you asked not to.
            //    We keep the computed `finalStatus` available for in-memory decisions or eventing if required.
        }
    }
    internal static IEnumerable<int> GetOrderSummary(int requesterId)
    {
        // Validate requester
        try
        {
            var requester = s_dal.Courier.Read(requesterId);
        }
        catch
        {
            throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");
        }
        // Read data once
        var orders = s_dal.Order.ReadAll().ToList();
        var deliveriesAll = s_dal.Delivery.ReadAll().ToList();
        var config = AdminManager.GetConfig();

        int statusCount = Enum.GetValues(typeof(BO.OrderStatus)).Length;
        int scheduleCount = Enum.GetValues(typeof(BO.ScheduleStatus)).Length;
        // Flattened matrix: index = statusIndex * scheduleCount + scheduleIndex
        int[] summary = new int[statusCount * scheduleCount];

        // Project each order to its computed (OrderStatus, ScheduleStatus) then GroupBy as requested
        var projections = orders.Select(o =>
        {
            var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();
            var lastDelivery = orderDeliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();
            DateTime? realArrival = lastDelivery?.ArrivalTime;

            // compute coordinates (best-effort, keep 0,0 on failure)
            double lat = o.Latitude ?? 0;
            double lon = o.Longitude ?? 0;
            if (lat == 0 && lon == 0)
            {
                try
                {
                    var coords = Tools.GetCoordinatesFromAddressAsync(o.CustomerAddress).GetAwaiter().GetResult();
                    lat = coords.Latitude;
                    lon = coords.Longitude;
                }
                catch { }
            }

            double distance = Tools.BirdDistance(
                config.CompanyLatitude,
                config.CompanyLongitude,
                lat,
                lon
            );

            DateTime? estArrival = distance > 0
                ? Tools.CalculateEstimatedArrival(o.OrderDate, distance, config.CarSpeed)
                : null;

            DateTime? maxArrival = estArrival?.Add(config.RiskRange);

            var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);
            var scheduleStatus = Tools.CalculateScheduleStatus(orderStatus, o.OrderDate, estArrival, maxArrival, realArrival);

            return new { Status = orderStatus, Schedule = scheduleStatus };
        });

        var groups = projections.GroupBy(p => new { p.Status, p.Schedule });

        foreach (var g in groups)
        {
            int sIdx = (int)g.Key.Status;
            int schIdx = (int)g.Key.Schedule;
            int idx = sIdx * scheduleCount + schIdx;
            summary[idx] = g.Count();
        }

        return summary;
    }

    internal static IEnumerable<BO.OrderInList> orderInLists(int requesterId,Enum? filter,object? Object,Enum? sorter)
    {
        // 1. Validation of requesterId
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester does not exist.");

        var doOrders = s_dal.Order.ReadAll().ToList();
        var deliveriesDO = s_dal.Delivery.ReadAll().ToList();
        var config = AdminManager.GetConfig();

        // 3. convert DO.Order → BO.OrderInList
        var list = doOrders.Select(order =>
        {
            var orderDeliveries = deliveriesDO.Where(d => d.OrderId == order.Id).ToList();
            var lastDelivery = orderDeliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();
            int? deliveryId = lastDelivery?.Id;

            // Coordinates (geocode if missing)
            double lat = order.Latitude ?? 0;
            double lon = order.Longitude ?? 0;
            if (lat == 0 && lon == 0)
            {
                try
                {
                    var coords = Tools.GetCoordinatesFromAddressAsync(order.CustomerAddress).GetAwaiter().GetResult();
                    lat = coords.Latitude;
                    lon = coords.Longitude;
                }
                catch (Exception ex)
                {
                    throw new BLFailedOperation(ex.Message);
                }
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

        // 4. Filtering (support OrderStatus and OrderType filters)
        if (filter != null)
        {
            if (filter is BO.OrderStatus os)
                list = list.Where(l => l.Status == os).ToList();
            else if (filter is BO.OrderType ot)
                list = list.Where(l => l.Type == ot).ToList();
        }

        // 5. Sorting (support few common keys; Object can be bool for ascending)
        if (sorter != null)
        {
            string key = sorter.ToString() ?? string.Empty;
            bool ascending = Object is bool b ? b : true;

            list = key switch
            {
                "Distance" => ascending ? list.OrderBy(l => l.Distance).ToList() : list.OrderByDescending(l => l.Distance).ToList(),
                "OrderEndTime" => ascending ? list.OrderBy(l => l.OrderEndTime).ToList() : list.OrderByDescending(l => l.OrderEndTime).ToList(),
                "TreatmentEndTime" => ascending ? list.OrderBy(l => l.TreatmentEndTime).ToList() : list.OrderByDescending(l => l.TreatmentEndTime).ToList(),
                "NumberOfCouriers" => ascending ? list.OrderBy(l => l.NumberOfCouriers).ToList() : list.OrderByDescending(l => l.NumberOfCouriers).ToList(),
                "OrderId" => ascending ? list.OrderBy(l => l.OrderId).ToList() : list.OrderByDescending(l => l.OrderId).ToList(),
                "Status" => ascending ? list.OrderBy(l => l.Status).ToList() : list.OrderByDescending(l => l.Status).ToList(),
                "ScheduleStatus" => ascending ? list.OrderBy(l => l.ScheduleStatus).ToList() : list.OrderByDescending(l => l.ScheduleStatus).ToList(),
                _ => list
            };
        }

        return list;
    }

    // a choisir entre les deux methodes dessus dessous
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
        // validate requester
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester does not exist.");

        // read order
        var doOrder = s_dal.Order.Read(orderId) ?? throw new BLNotFoundException($"Order with id {orderId} not found.");

        // get deliveries for this order
        var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
        var lastDelivery = deliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();

        var config = AdminManager.GetConfig();

        // coordinates (use geocoding if missing)
        double lat = doOrder.Latitude ?? 0;
        double lon = doOrder.Longitude ?? 0;
        if (lat == 0 && lon == 0)
        {
            try
            {
                var coords = Tools.GetCoordinatesFromAddressAsync(doOrder.CustomerAddress).GetAwaiter().GetResult();
                lat = coords.Latitude;
                lon = coords.Longitude;
            }
            catch
            {
                // fallback to 0,0 if geocoding fails
            }
        }

        double distance = Tools.BirdDistance(config.CompanyLatitude, config.CompanyLongitude, lat, lon);

        DateTime? estArrival = distance > 0 ? (DateTime?)Tools.CalculateEstimatedArrival(doOrder.OrderDate, distance, config.CarSpeed) : null;
        DateTime? maxArrival = estArrival?.Add(config.RiskRange);
        DateTime? realArrival = lastDelivery?.ArrivalTime;

        var status = Tools.CalculateOrderStatus(deliveries);
        var schedule = Tools.CalculateScheduleStatus(status, doOrder.OrderDate, estArrival, maxArrival, realArrival);

        TimeSpan arrivalEstDuration = estArrival != null ? estArrival.Value - doOrder.OrderDate : TimeSpan.Zero;

        // map deliveries to BO.DeliveryPerOrderInList
        var deliveriesPerOrder = deliveries.Select(d =>
        {
            var courier = s_dal.Courier.Read(d.CourierId);
            return new BO.DeliveryPerOrderInList
            {
                DeliveryId = d.Id,
                CourierId = d.CourierId == 0 ? null : (int?)d.CourierId,
                Name = courier?.Name ?? string.Empty,
                PickupTime = d.PickupTime,
                OrderStatus = d.Status.HasValue ? (BO.OrderStatus?) (BO.OrderStatus)d.Status.Value : null,
                ArrivalTime = d.ArrivalTime
            };
        }).ToList();

        // build BO.Order
        return new BO.Order
        {
            Id = doOrder.Id,
            Type = (BO.OrderType)doOrder.Type,
            OrderDescription = doOrder.Description,
            CustomerAddress = doOrder.CustomerAddress,
            Latitude = lat,
            Longitude = lon,
            Distance = distance,
            CustomerName = doOrder.CustomerName,
            CustomerPhone = doOrder.CustomerPhone,
            Weight = doOrder.weight,
            Fragility = doOrder.Fragility.HasValue ? (BO.FragilityLevel?) (BO.FragilityLevel)doOrder.Fragility.Value : null,
            Volume = doOrder.size,
            OrderDate = doOrder.OrderDate,
            ArrivalDateEstimeted = estArrival,
            ArrivalDateMax = maxArrival,
            Status = status,
            ScheduleStatus = schedule,
            ArrivalTimeEstimeted = arrivalEstDuration,
            DeliveriesPerOrder = deliveriesPerOrder
        };
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
        // validate requester
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester does not exist.");

        // delete associated deliveries first
        var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
        foreach (var d in deliveries)
            s_dal.Delivery.Delete(d.Id);

        // then delete order
        s_dal.Order.Delete(orderId);
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
        // validate requester and courier
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester does not exist.");

        var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");
        var delivery = s_dal.Delivery.Read(deliveryId) ?? throw new BLNotFoundException($"Delivery {deliveryId} not found.");

        var order = s_dal.Order.Read(delivery.OrderId) ?? throw new BLNotFoundException($"Order {delivery.OrderId} not found.");
        var config = AdminManager.GetConfig();

        // compute coordinates and distance
        double lat = order.Latitude ?? 0;
        double lon = order.Longitude ?? 0;
        if (lat == 0 && lon == 0)
        {
            try
            {
                var coords = Tools.GetCoordinatesFromAddressAsync(order.CustomerAddress).GetAwaiter().GetResult();
                lat = coords.Latitude;
                lon = coords.Longitude;
            }
            catch { }
        }

        double distance = Tools.BirdDistance(config.CompanyLatitude, config.CompanyLongitude, lat, lon);

        // update delivery
        var updated = delivery with
        {
            ArrivalTime = DateTime.Now,
            Distance = distance,
            Status = DO.OrderStatus.Delivered
        };

        s_dal.Delivery.Update(updated);

        // optionally update courier activity (best-effort)
        try
        {
            Tools.UpdateCourierActivity(courier, config.InactivityThreshold);
            s_dal.Courier.Update(courier);
        }
        catch { }
    }

    internal static void AssignOrderToCourier(int requesterId,int orderId,int courierId)
    {
        // validate requester, courier and order
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester does not exist.");

        var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");
        var order = s_dal.Order.Read(orderId) ?? throw new BLNotFoundException($"Order {orderId} not found.");

        // create a new delivery assigned to courier
        var delivery = new DO.Delivery
        {
            Id = 0,
            OrderId = orderId,
            Transport = courier.Transport,
            CourierId = courierId,
            PickupTime = DateTime.Now,
            ArrivalTime = null,
            Distance = null,
            Status = DO.OrderStatus.Processing
        };

        s_dal.Delivery.Create(delivery);
    }

    internal static IEnumerable<BO.ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId,int courierId,BO.OrderType? filter,Enum? sorter)
    {
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester does not exist.");

        var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");

        var deliveries = s_dal.Delivery.ReadAll(d => d.CourierId == courierId && d.ArrivalTime != null).ToList();
        var orders = s_dal.Order.ReadAll().ToDictionary(o => o.Id);

        var list = deliveries.Select(d =>
        {
            orders.TryGetValue(d.OrderId, out var o);
            return new BO.ClosedDeliveryInList
            {
                DeliveryId = d.Id,
                OrderId = d.OrderId,
                OrderType = o != null ? (BO.OrderType)o.Type : BO.OrderType.FastFood,
                CustomerAdress = o?.CustomerAddress ?? string.Empty,
                DeliveryTransport = (BO.DeliveryTransport)d.Transport,
                ActualDistance = d.Distance,
                DeliveryTotalTime = d.ArrivalTime!.Value - d.PickupTime,
                DeliveredStatus = d.Status switch
                {
                    DO.OrderStatus.Delivered => BO.DeliveredStatus.Delivered,
                    DO.OrderStatus.Canceled => BO.DeliveredStatus.Canceled,
                    DO.OrderStatus.Returned => BO.DeliveredStatus.Rejected,
                    _ => BO.DeliveredStatus.Failed
                }
            };
        }).ToList();

        if (filter.HasValue)
            list = list.Where(x => x.OrderType == filter.Value).ToList();

        if (sorter != null)
        {
            string key = sorter.ToString() ?? string.Empty;
            bool ascending = true;
            list = key switch
            {
                "DeliveryTotalTime" => ascending ? list.OrderBy(x => x.DeliveryTotalTime).ToList() : list.OrderByDescending(x => x.DeliveryTotalTime).ToList(),
                "ActualDistance" => ascending ? list.OrderBy(x => x.ActualDistance).ToList() : list.OrderByDescending(x => x.ActualDistance).ToList(),
                _ => list
            };
        }

        return list;
    }

    internal static IEnumerable<BO.OpenOrderInList> GetOpenOrdersForCourier(int requesterId,int courierId, BO.OrderType? filter,BO.DeliveredStatus? sorter)
    {
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester does not exist.");

        var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");

        var deliveries = s_dal.Delivery.ReadAll(d => d.CourierId == courierId && d.ArrivalTime == null).ToList();
        var orders = s_dal.Order.ReadAll().ToDictionary(o => o.Id);
        var config = AdminManager.GetConfig();

        var list = deliveries.Select(d =>
        {
            orders.TryGetValue(d.OrderId, out var o);
            // compute bird distance
            double lat = o?.Latitude ?? 0;
            double lon = o?.Longitude ?? 0;
            if (lat == 0 && lon == 0 && o != null)
            {
                try
                {
                    var coords = Tools.GetCoordinatesFromAddressAsync(o.CustomerAddress).GetAwaiter().GetResult();
                    lat = coords.Latitude;
                    lon = coords.Longitude;
                }
                catch { }
            }
            double bird = Tools.BirdDistance(config.CompanyLatitude, config.CompanyLongitude, lat, lon);

            DateTime? estArrival = o != null ? Tools.CalculateEstimatedArrival(o.OrderDate, bird, config.CarSpeed) : (DateTime?)null;
            TimeSpan estTimeSpan = estArrival != null ? estArrival.Value - (o?.OrderDate ?? DateTime.Now) : TimeSpan.Zero;
            DateTime maxDelivered = estArrival?.Add(config.RiskRange) ?? DateTime.Now;

            return new BO.OpenOrderInList
            {
                CourierId = d.CourierId == 0 ? null : (int?)d.CourierId,
                OrderId = d.OrderId,
                OrderType = o != null ? (BO.OrderType)o.Type : BO.OrderType.FastFood,
                Fragility = o?.Fragility != null ? (BO.FragilityLevel?) (BO.FragilityLevel)o.Fragility.Value : null,
                CustomerAddress = o?.CustomerAddress ?? string.Empty,
                BirdDistance = bird,
                Distance = d.Distance,
                AddedTime = (o != null) ? (TimeSpan?) (DateTime.Now - o.OrderDate) : null,
                ScheduleStatus = Tools.CalculateScheduleStatus(Tools.CalculateOrderStatus(new List<DO.Delivery> { d }), o?.OrderDate ?? DateTime.Now, estArrival, estArrival?.Add(config.RiskRange), d.ArrivalTime),
                EstimatedDeliveryTime = estTimeSpan,
                MaxDeliveredTime = maxDelivered
            };
        }).ToList();

        if (filter.HasValue)
            list = list.Where(x => x.OrderType == filter.Value).ToList();

        // sorter param is DeliveredStatus? but for open orders we'll support sorting by AddedTime or BirdDistance via enum name string
        if (sorter != null)
        {
            string key = sorter.ToString() ?? string.Empty;
            bool ascending = true;
            list = key switch
            {
                "BirdDistance" => ascending ? list.OrderBy(x => x.BirdDistance).ToList() : list.OrderByDescending(x => x.BirdDistance).ToList(),
                "AddedTime" => ascending ? list.OrderBy(x => x.AddedTime).ToList() : list.OrderByDescending(x => x.AddedTime).ToList(),
                _ => list
            };
        }

        return list;
    }

}
