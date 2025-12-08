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

    internal static ObserverManager Observers = new();
    
    internal static void PeriodicOrdersUpdates(DateTime oldClock, DateTime newClock)
    {
        try
        {
            // Read config values once
            TimeSpan maxDeliveryTime = s_dal.Config.MaxTimeDelivery;
            TimeSpan riskRange = s_dal.Config.RiskRange;

            // Read all deliveries once to avoid multiple DAL calls
            var deliveriesAll = s_dal.Delivery.ReadAll().ToList();
            bool deliveriesUpdated = false;
            var updatedOrders = new HashSet<int>(); // Track which orders were affected

            // Iterate over a snapshot of orders
            foreach (var o in s_dal.Order.ReadAll().ToList())
            {
                // Deliveries for this order
                var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();

                // 1) Start deliveries whose pickup time was reached (Status == null -> Processing)
                foreach (var d in orderDeliveries.Where(d => d.Status == null && d.PickupTime > oldClock && d.PickupTime <= newClock))
                {
                    var upd = d with { Status = DO.OrderStatus.Processing };
                    s_dal.Delivery.Update(upd);
                    deliveriesUpdated = true;
                    updatedOrders.Add(d.OrderId); // Track the affected order
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
                            ArrivalTime = newClock // Mark finished so it won't be considered open anymore
                        };
                        s_dal.Delivery.Update(upd);
                        deliveriesUpdated = true;
                        updatedOrders.Add(d.OrderId); // Track the affected order
                    }
                }

                // 3) Compute in-memory order status (BO) using deliveries + time-based escalation
                var deliveryBasedStatus = Tools.CalculateOrderStatus(orderDeliveries); // BO.OrderStatus
                TimeSpan elapsed = newClock - o.OrderDate;
                BO.OrderStatus timeBasedStatus = BO.OrderStatus.Pending;

                if (elapsed > maxDeliveryTime)
                    timeBasedStatus = BO.OrderStatus.Canceled;
                else if (elapsed >= (maxDeliveryTime - riskRange))
                    timeBasedStatus = deliveryBasedStatus == BO.OrderStatus.Pending ? BO.OrderStatus.Processing : deliveryBasedStatus;
                else
                    timeBasedStatus = BO.OrderStatus.Pending;

                BO.OrderStatus finalStatus;
                if (deliveryBasedStatus == BO.OrderStatus.Delivered
                    || deliveryBasedStatus == BO.OrderStatus.Returned
                    || deliveryBasedStatus == BO.OrderStatus.Canceled)
                {
                    finalStatus = deliveryBasedStatus;
                }
                else
                {
                    finalStatus = timeBasedStatus;
                }

                // 4) No persistence to DO.Order (by design).
            }

            // Notify observers if any deliveries were updated
            if (deliveriesUpdated)
            {
                // Notify each affected order individually
                foreach (var orderId in updatedOrders)
                {
                    Observers.NotifyItemUpdated(orderId);
                }
                // Notify that the list has been updated
                Observers.NotifyListUpdated();
            }
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static IEnumerable<int> GetOrderSummary(int requesterId)
    {
        try
        {
            // Validate requester
            var requester = s_dal.Courier.Read(requesterId) ?? throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            // Read data once
            var orders = s_dal.Order.ReadAll().ToList();
            var deliveriesAll = s_dal.Delivery.ReadAll().ToList();
            var config = AdminManager.GetConfig();

            int statusCount = Enum.GetValues(typeof(BO.OrderStatus)).Length;
            int scheduleCount = Enum.GetValues(typeof(BO.ScheduleStatus)).Length;
            int[] summary = new int[statusCount * scheduleCount];

            var projections = orders.Select(o =>
            {
                var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();
                var lastDelivery = orderDeliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();
                DateTime? realArrival = lastDelivery?.ArrivalTime;

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

                // Choose speed based on last delivery transport if available, else fallback to car speed
                double speed = config.CarSpeed;
                if (lastDelivery != null)
                    speed = Tools.GetSpeed(lastDelivery.Transport, config);

                DateTime? estArrival = distance > 0
                    ? Tools.CalculateEstimatedArrival(o.OrderDate, distance, speed)
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
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static IEnumerable<BO.OrderInList> orderInLists(int requesterId, Enum? filter, object? Object, Enum? sorter)
    {
        try
        {
            // Validation of requesterId
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            var doOrders = s_dal.Order.ReadAll().ToList();
            var deliveriesDO = s_dal.Delivery.ReadAll().ToList();
            var config = AdminManager.GetConfig();

            var now = AdminManager.Now;

            var list = doOrders.Select(order =>
            {
                var orderDeliveries = deliveriesDO.Where(d => d.OrderId == order.Id).ToList();
                var lastDelivery = orderDeliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();
                int? deliveryId = lastDelivery?.Id;

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

                // Pick speed depending on lastDelivery transport if available, otherwise default
                double speed = config.CarSpeed;
                if (lastDelivery != null)
                    speed = Tools.GetSpeed(lastDelivery.Transport, config);

                DateTime? estArrival = distance > 0
                    ? Tools.CalculateEstimatedArrival(order.OrderDate, distance, speed)
                    : null;

                DateTime? maxArrival = estArrival?.Add(config.RiskRange);
                DateTime? realArrival = lastDelivery?.ArrivalTime;

                var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);

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
                    OrderEndTime = realArrival != null ? realArrival.Value - order.OrderDate : now - order.OrderDate,
                    TreatmentEndTime = lastDelivery != null ? lastDelivery.PickupTime - order.OrderDate : TimeSpan.Zero,
                    NumberOfCouriers = orderDeliveries.Select(d => d.CourierId).Distinct().Count()
                };
            }).ToList();

            if (filter != null)
            {
                if (filter is BO.OrderStatus os)
                    list = list.Where(l => l.Status == os).ToList();
                else if (filter is BO.OrderType ot)
                    list = list.Where(l => l.Type == ot).ToList();
            }

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
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static IEnumerable<BO.OrderInList> GetOrdersList(int requesterId, BO.OrderStatus? statusFilter, object? sortParameter)
    {
        try
        {
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            var config = AdminManager.GetConfig();
            var ordersDO = s_dal.Order.ReadAll().ToList();
            var deliveriesDO = s_dal.Delivery.ReadAll().ToList();

            var now = AdminManager.Now;

            var list = ordersDO.Select(order =>
            {
                var orderDeliveries = deliveriesDO
                    .Where(d => d.OrderId == order.Id)
                    .ToList();

                var lastDelivery = orderDeliveries
                    .OrderByDescending(d => d.PickupTime)
                    .FirstOrDefault();

                int? deliveryId = lastDelivery?.Id;

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

                double speed = config.CarSpeed;
                if (lastDelivery != null)
                    speed = Tools.GetSpeed(lastDelivery.Transport, config);

                DateTime? estArrival = distance > 0
                    ? Tools.CalculateEstimatedArrival(order.OrderDate, distance, speed)
                    : null;

                DateTime? maxArrival = estArrival?.Add(config.RiskRange);
                DateTime? realArrival = lastDelivery?.ArrivalTime;

                var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);

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
                    OrderEndTime = realArrival != null ? realArrival.Value - order.OrderDate : now - order.OrderDate,
                    TreatmentEndTime = lastDelivery != null ? lastDelivery.PickupTime - order.OrderDate : TimeSpan.Zero,
                    NumberOfCouriers = orderDeliveries.Select(d => d.CourierId).Distinct().Count()
                };

            }).ToList();

            return list;
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static BO.Order GetOrderDetails(int requesterId, int orderId)
    {
        try
        {
            // Validate requester
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Read order
            var doOrder = s_dal.Order.Read(orderId) ?? throw new BLNotFoundException($"Order with id {orderId} not found.");

            // Get deliveries for this order
            var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            var lastDelivery = deliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();

            var config = AdminManager.GetConfig();

            // Coordinates (use geocoding if missing)
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
                    // Fallback to 0,0 if geocoding fails
                }
            }

            double distance = Tools.BirdDistance(config.CompanyLatitude, config.CompanyLongitude, lat, lon);

            double speed = config.CarSpeed;
            if (lastDelivery != null)
                speed = Tools.GetSpeed(lastDelivery.Transport, config);

            DateTime? estArrival = distance > 0 ? (DateTime?)Tools.CalculateEstimatedArrival(doOrder.OrderDate, distance, speed) : null;
            DateTime? maxArrival = estArrival?.Add(config.RiskRange);
            DateTime? realArrival = lastDelivery?.ArrivalTime;

            var status = Tools.CalculateOrderStatus(deliveries);
            var schedule = Tools.CalculateScheduleStatus(status, doOrder.OrderDate, estArrival, maxArrival, realArrival);

            TimeSpan arrivalEstDuration = estArrival != null ? estArrival.Value - doOrder.OrderDate : TimeSpan.Zero;

            // Map deliveries to BO.DeliveryPerOrderInList
            var deliveriesPerOrder = deliveries.Select(d =>
            {
                var courier = s_dal.Courier.Read(d.CourierId);
                return new BO.DeliveryPerOrderInList
                {
                    DeliveryId = d.Id,
                    CourierId = d.CourierId == 0 ? null : (int?)d.CourierId,
                    Name = courier?.Name ?? string.Empty,
                    PickupTime = d.PickupTime,
                    OrderStatus = d.Status.HasValue ? (BO.OrderStatus?)(BO.OrderStatus)d.Status.Value : null,
                    ArrivalTime = d.ArrivalTime
                };
            }).ToList();

            // Build BO.Order
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
                Fragility = doOrder.Fragility.HasValue ? (BO.FragilityLevel?)(BO.FragilityLevel)doOrder.Fragility.Value : null,
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
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static void UpdateOrderDetails(int requesterId, BO.Order order)
    {
        try
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
            
            // Notifications for order modification
            Observers.NotifyItemUpdated(order.Id);
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static void CancelOrder(int requesterId, int orderId)
    {
        try
        {
            s_dal.Order.Delete(orderId);
            
            // Notifications for order cancellation/deletion
            Observers.NotifyItemUpdated(orderId);
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static void RemoveOrder(int requesterId, int orderId)
    {
        try
        {
            // Validate requester
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Delete associated deliveries first
            var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            foreach (var d in deliveries)
                s_dal.Delivery.Delete(d.Id);

            // Then delete order
            s_dal.Order.Delete(orderId);
            
            // Notifications for order removal
            Observers.NotifyItemUpdated(orderId);
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static void AddOrder(int requesterId, BO.Order order)
    {
        try
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
            
            // Notification for adding order to list
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static void FinishOrder(int requesterId, int courierId, int deliveryId)
    {
        try
        {
            // Validate requester and courier
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");
            var delivery = s_dal.Delivery.Read(deliveryId) ?? throw new BLNotFoundException($"Delivery {deliveryId} not found.");

            var order = s_dal.Order.Read(delivery.OrderId) ?? throw new BLNotFoundException($"Order {delivery.OrderId} not found.");
            var config = AdminManager.GetConfig();

            // Compute coordinates and distance
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

            // Update delivery
            var updated = delivery with
            {
                ArrivalTime = AdminManager.Now,
                Distance = distance,
                Status = DO.OrderStatus.Delivered
            };

            s_dal.Delivery.Update(updated);

            // Optionally update courier activity (best-effort)
            try
            {
                Tools.UpdateCourierActivity(courier, config.InactivityThreshold);
                s_dal.Courier.Update(courier);
            }
            catch { }

            // Notifications for order/delivery completion
            Observers.NotifyItemUpdated(delivery.OrderId);
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static void AssignOrderToCourier(int requesterId, int orderId, int courierId)
    {
        try
        {
            // Validate requester, courier and order
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");
            var order = s_dal.Order.Read(orderId) ?? throw new BLNotFoundException($"Order {orderId} not found.");

            // Verify courier is active
            if (!courier.IsActive)
                throw new BO.BLInvalidOperationException($"Cannot assign order {orderId} to courier {courierId}: courier is not active.");

            // Verify courier is not Director
            if (courier.Administrator == DO.Administrator.Director)
                throw new BO.BLInvalidOperationException($"Cannot assign order {orderId} to courier {courierId}: courier is a Director.");

            // Create a new delivery assigned to courier
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
            
            // Notifications for order assignment
            Observers.NotifyItemUpdated(orderId);
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static IEnumerable<BO.ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId, int courierId, BO.OrderType? filter, Enum? sorter)
    {
        try
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
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static IEnumerable<BO.OpenOrderInList> GetOpenOrdersForCourier(int requesterId, int courierId, BO.OrderType? filter, BO.DeliveredStatus? sorter)
    {
        try
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

                // Use delivery pickup time + delivery transport speed for ETA when available
                DateTime? estArrival = null;
                TimeSpan estTimeSpan = TimeSpan.Zero;
                if (o != null)
                {
                    double speed = Tools.GetSpeed(d.Transport, config);
                    estArrival = Tools.CalculateEstimatedArrival(d.PickupTime, bird, speed);
                    estTimeSpan = estArrival != null ? estArrival.Value - d.PickupTime : TimeSpan.Zero;
                }

                DateTime maxDelivered = estArrival?.Add(config.RiskRange) ?? AdminManager.Now;

                return new BO.OpenOrderInList
                {
                    CourierId = d.CourierId == 0 ? null : (int?)d.CourierId,
                    OrderId = d.OrderId,
                    OrderType = o != null ? (BO.OrderType)o.Type : BO.OrderType.FastFood,
                    Fragility = o?.Fragility != null ? (BO.FragilityLevel?)(BO.FragilityLevel)o.Fragility.Value : null,
                    CustomerAddress = o?.CustomerAddress ?? string.Empty,
                    BirdDistance = bird,
                    Distance = d.Distance,
                    AddedTime = (o != null) ? (TimeSpan?)(AdminManager.Now - o.OrderDate) : null,
                    ScheduleStatus = Tools.CalculateScheduleStatus(Tools.CalculateOrderStatus(new List<DO.Delivery> { d }), o?.OrderDate ?? AdminManager.Now, estArrival, estArrival?.Add(config.RiskRange), d.ArrivalTime),
                    EstimatedDeliveryTime = estTimeSpan,
                    MaxDeliveredTime = maxDelivered
                };
            }).ToList();

            if (filter.HasValue)
                list = list.Where(x => x.OrderType == filter.Value).ToList();

            // Sorter param is DeliveredStatus? but for open orders we'll support sorting by AddedTime or BirdDistance via enum name string
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
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }
}
