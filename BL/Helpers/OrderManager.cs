using BO;
using DalApi;
using DO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Mail;
using System.Net;

namespace Helpers;

/// <summary>
/// Manages order-related operations in the Business Logic layer.
/// Handles order creation, updates, deletion, delivery assignments, and status tracking.
/// Provides periodic updates to order and delivery statuses based on time-based escalation rules.
/// Coordinates with couriers for order fulfillment and provides observer notifications for UI synchronization.
/// </summary>
internal static class OrderManager
{
    /// <summary>
    /// Static reference to the Data Access Layer providing access to all data repositories.
    /// </summary>
    private static readonly IDal s_dal = Factory.Get;

    /// <summary>
    /// Observer manager for notifying subscribers of order list and item changes.
    /// Enables real-time UI updates when order data is modified.
    /// </summary>
    internal static ObserverManager Observers = new();

    /// <summary>
    /// Periodically updates order and delivery statuses based on elapsed time and configured thresholds.
    /// Handles three main update scenarios:
    /// 1. Starts processing deliveries when their pickup time is reached
    /// 2. Cancels processing deliveries that exceed maximum delivery time
    /// 3. Escalates order status based on time-based rules (pending → processing → risk-based escalation)
    /// 
    /// Status escalation logic:
    /// - If elapsed time exceeds maxDeliveryTime: mark as Canceled
    /// - If elapsed time is within riskRange (near maxDeliveryTime): escalate based on delivery status
    /// - Otherwise: remain Pending
    /// 
    /// No changes are persisted to DO.Order (by design); only delivery records are updated.
    /// </summary>
    /// <param name="oldClock">The previous clock time marking the start of the evaluation period.</param>
    /// <param name="newClock">The current clock time marking the end of the evaluation period.</param>
    /// <remarks>
    /// This method is typically called periodically (e.g., by a background timer) to update delivery statuses
    /// and escalate orders based on time thresholds. Configuration values are read once per call for efficiency.
    /// </remarks>
    /// <exception cref="BO.BLNotFoundException">Thrown if required configuration data cannot be retrieved.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void PeriodicOrdersUpdates(DateTime oldClock, DateTime newClock)
    {
        try
        {
            // Read config values once
            TimeSpan maxDeliveryTime = s_dal.Config.MaxTimeDelivery;

            // Snapshot reads to minimize DAL calls
            var ordersAll = s_dal.Order.ReadAll().ToList();
            var deliveriesAll = s_dal.Delivery.ReadAll().ToList();

            bool deliveriesUpdated = false;
            var updatedOrders = new HashSet<int>();

            
            // update all OPEN orders whose validity expired after advancing the system clock
            foreach (var o in ordersAll)
            {
                // If the order already exceeded max time at newClock -> it is expired
                if (newClock - o.OrderDate <= maxDeliveryTime)
                    continue;

                // Find all deliveries of this order
                var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();

                // "Open" delivery = a delivery that hasn't ended yet => DeliveredStatus is null
                // (tiour: DeliveredStatus is the delivery end-type; null means still not ended)
                var openDeliveries = orderDeliveries
                    .Where(d => d.DeliveredStatus == null && d.ArrivalTime == null)
                    .ToList();

                if (openDeliveries.Count == 0)
                    continue;

                // Close every open delivery as expired/failed at newClock
                foreach (var d in openDeliveries)
                {
                    var upd = d with
                    {
                        // Delivery ended now
                        ArrivalTime = newClock,

                        // Delivery end type  - must become non-null on closure
                        DeliveredStatus = DO.DeliveredStatus.Failed
                    };

                    s_dal.Delivery.Update(upd);
                    deliveriesUpdated = true;
                    updatedOrders.Add(d.OrderId);
                }
            }

            // Notify observers if any deliveries were updated
            if (deliveriesUpdated)
            {
                foreach (var orderId in updatedOrders)
                    Observers.NotifyItemUpdated(orderId);

                Observers.NotifyListUpdated();
            }
        }
        catch (Exception ex)
        {
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException ||
                ex is BO.BLInvalidInputException ||
                ex is BO.BLAlreadyExistsException ||
                ex is BO.BLInvalidOperationException) throw;

            // Map DAL exceptions to BL exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException)
                throw new BO.BLFailedOperation(ex.Message, ex);

            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Generates a summary array of order counts grouped by order status and schedule status.
    /// Returns a flattened array where each position represents a combination of (OrderStatus, ScheduleStatus).
    /// Array index calculation: (OrderStatus * ScheduleStatusCount) + ScheduleStatus
    /// 
    /// For each order, the method:
    /// - Retrieves associated deliveries and calculates order status from delivery records
    /// - Geocodes the delivery address if coordinates are not available
    /// - Calculates bird-distance (straight-line) and estimated arrival time
    /// - Determines schedule status based on arrival estimates and time thresholds
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this summary (must exist in the system).</param>
    /// <returns>A flattened integer array representing order count summary by status combinations.</returns>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static IEnumerable<int> GetOrderSummary(int requesterId)
    {
        try
        {
            var config = AdminManager.GetConfig();
            if (requesterId != config.BossId)
                throw new BO.BLInvalidOperationException("Requester is not authorized for order management operations.");

            var orderStatuses = Enum.GetValues(typeof(BO.OrderStatus))
                .Cast<BO.OrderStatus>()
                .Where(s => s != BO.OrderStatus.All)
                .ToList();

            var scheduleStatuses = Enum.GetValues(typeof(BO.ScheduleStatus))
                .Cast<BO.ScheduleStatus>()
                .Where(s => s != BO.ScheduleStatus.All)
                .ToList();

            int statusCount = orderStatuses.Count;
            int scheduleCount = scheduleStatuses.Count;

            int[] summary = new int[statusCount * scheduleCount];

            var list = orderInLists(requesterId, null, null, null).ToList();

            var groups = list
                .Where(l => l.Status != BO.OrderStatus.All && l.ScheduleStatus != BO.ScheduleStatus.All)
                .GroupBy(l => new { l.Status, l.ScheduleStatus });

            foreach (var g in groups)
            {
                int sIdx = orderStatuses.IndexOf(g.Key.Status);
                int schIdx = scheduleStatuses.IndexOf(g.Key.ScheduleStatus);

                if (sIdx < 0 || schIdx < 0)
                    continue;

                int idx = sIdx * scheduleCount + schIdx;
                summary[idx] = g.Count();
            }

            return summary;
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException ||
                ex is BO.BLInvalidInputException ||
                ex is BO.BLAlreadyExistsException ||
                ex is BO.BLInvalidOperationException)
                throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);

            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Retrieves a filtered and sorted list of orders with optional filtering by status/type and sorting options.
    /// Supports flexible filtering by OrderStatus or OrderType via enum parameter.
    /// Supports sorting by Distance, OrderEndTime, TreatmentEndTime, NumberOfCouriers, OrderId, Status, or ScheduleStatus.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this list (must exist in the system).</param>
    /// <param name="filter">Optional filter: can be BO.OrderStatus or BO.OrderType to filter results.</param>
    /// <param name="Object">Additional parameter (currently used for sort direction in some cases).</param>
    /// <param name="sorter">Optional sort key: the field name to sort by (e.g., "Distance", "OrderEndTime").</param>
    /// <returns>An enumerable collection of OrderInList objects representing orders with calculated metrics.</returns>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static IEnumerable<BO.OrderInList> orderInLists(int requesterId, Enum? filter, object? Object, Enum? sorter)
    {
        try
        {
            var config = AdminManager.GetConfig();
            if (requesterId != config.BossId)
                throw new BO.BLInvalidOperationException("Requester is not authorized for order management operations.");

            var doOrders = s_dal.Order.ReadAll().ToList();
            var deliveriesDO = s_dal.Delivery.ReadAll().ToList();

            var now = AdminManager.Now;

            var list = doOrders.Select(order =>
            {
                var orderDeliveries = deliveriesDO.Where(d => d.OrderId == order.Id).ToList();

                var lastByPickup = orderDeliveries
                    .OrderByDescending(d => d.PickupTime)
                    .FirstOrDefault();

                int? deliveryId = lastByPickup?.Id;

                var lastFinished = orderDeliveries
                    .Where(d => d.ArrivalTime != null)
                    .OrderByDescending(d => d.ArrivalTime)
                    .FirstOrDefault();

                DateTime? realArrival = lastFinished?.ArrivalTime;

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
                        throw new BO.BLFailedOperation(ex.Message, ex);
                    }
                }

                double distance = Tools.BirdDistance(
                    config.CompanyLatitude,
                    config.CompanyLongitude,
                    lat,
                    lon
                );

                double speed = config.CarSpeed;
                if (lastByPickup != null)
                    speed = Tools.GetSpeed(lastByPickup.Transport, config);

                DateTime? estArrival = distance > 0
                    ? Tools.CalculateEstimatedArrival(order.OrderDate, distance, speed)
                    : null;

                DateTime maxArrival = order.OrderDate.Add(config.MaxDeliveryTime);

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
                    TreatmentEndTime = lastByPickup != null ? lastByPickup.PickupTime - order.OrderDate : TimeSpan.Zero,
                    NumberOfCouriers = orderDeliveries.Select(d => d.CourierId).Distinct().Count()
                };
            }).ToList();

            if (filter != null)
            {
                if (Object == null)
                    throw new BO.BLInvalidInputException("Filter value cannot be null when filter selector is provided.");

                string fKey = filter.ToString() ?? string.Empty;

                list = fKey switch
                {
                    "ByOrderStatus" => Object is BO.OrderStatus os
                        ? list.Where(l => l.Status == os).ToList()
                        : throw new BO.BLInvalidInputException("Invalid filter value type for Status."),

                    "ByOrderType" => Object is BO.OrderType ot
                        ? list.Where(l => l.Type == ot).ToList()
                        : throw new BO.BLInvalidInputException("Invalid filter value type for Type."),

                    "BySheduleStatus" => Object is BO.ScheduleStatus ss
                        ? list.Where(l => l.ScheduleStatus == ss).ToList()
                        : throw new BO.BLInvalidInputException("Invalid filter value type for ScheduleStatus."),

                    _ => throw new BO.BLInvalidInputException($"Unknown filter selector: {fKey}")
                };
            }

            if (sorter == null)
            {
                list = list
                    .OrderBy(l => l.Status)
                    .ThenBy(l => l.OrderId)
                    .ToList();
            }
            else
            {
                string sKey = sorter.ToString() ?? string.Empty;

                list = sKey switch
                {
                    "Distance" => list.OrderBy(l => l.Distance).ThenBy(l => l.OrderId).ToList(),
                    "OrderEndTime" => list.OrderBy(l => l.OrderEndTime).ThenBy(l => l.OrderId).ToList(),
                    "TreatmentEndTime" => list.OrderBy(l => l.TreatmentEndTime).ThenBy(l => l.OrderId).ToList(),
                    "NumberOfCouriers" => list.OrderBy(l => l.NumberOfCouriers).ThenBy(l => l.OrderId).ToList(),
                    "OrderId" => list.OrderBy(l => l.OrderId).ToList(),
                    "Status" => list.OrderBy(l => l.Status).ThenBy(l => l.OrderId).ToList(),
                    "ScheduleStatus" => list.OrderBy(l => l.ScheduleStatus).ThenBy(l => l.OrderId).ToList(),
                    _ => throw new BO.BLInvalidInputException($"Unknown sorter selector: {sKey}")
                };
            }

            return list;
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException ||
                ex is BO.BLInvalidInputException ||
                ex is BO.BLAlreadyExistsException ||
                ex is BO.BLInvalidOperationException) throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);

            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static IEnumerable<BO.OrderInList> orderInListsDoubleFilter(int requesterId, Enum? filter1, Enum? filter2)
    {
        try
        {
            var config = AdminManager.GetConfig();
            if (requesterId != config.BossId)
                throw new BO.BLInvalidOperationException("Requester is not authorized for order management operations.");

            // Base list (même logique que orderInLists)
            var doOrders = s_dal.Order.ReadAll().ToList();
            var deliveriesDO = s_dal.Delivery.ReadAll().ToList();
            var now = AdminManager.Now;

            var list = doOrders.Select(order =>
            {
                var orderDeliveries = deliveriesDO.Where(d => d.OrderId == order.Id).ToList();

                var lastByPickup = orderDeliveries
                    .OrderByDescending(d => d.PickupTime)
                    .FirstOrDefault();

                int? deliveryId = lastByPickup?.Id;

                var lastFinished = orderDeliveries
                    .Where(d => d.ArrivalTime != null)
                    .OrderByDescending(d => d.ArrivalTime)
                    .FirstOrDefault();

                DateTime? realArrival = lastFinished?.ArrivalTime;

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
                        throw new BO.BLFailedOperation(ex.Message, ex);
                    }
                }

                double distance = Tools.BirdDistance(
                    config.CompanyLatitude,
                    config.CompanyLongitude,
                    lat,
                    lon
                );

                double speed = config.CarSpeed;
                if (lastByPickup != null)
                    speed = Tools.GetSpeed(lastByPickup.Transport, config);

                DateTime? estArrival = distance > 0
                    ? Tools.CalculateEstimatedArrival(order.OrderDate, distance, speed)
                    : null;

                DateTime maxArrival = order.OrderDate.Add(config.MaxDeliveryTime);

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
                    TreatmentEndTime = lastByPickup != null ? lastByPickup.PickupTime - order.OrderDate : TimeSpan.Zero,
                    NumberOfCouriers = orderDeliveries.Select(d => d.CourierId).Distinct().Count()
                };
            }).ToList();

            // --- Double filtre ---
            // Applique un filtre si non-null, en déduisant la propriété à filtrer selon le type de l'enum
            static List<BO.OrderInList> ApplyOneFilter(List<BO.OrderInList> src, Enum? f)
            {
                if (f is null) return src;

                return f switch
                {
                    BO.OrderStatus os => src.Where(x => x.Status == os).ToList(),
                    BO.OrderType ot => src.Where(x => x.Type == ot).ToList(),
                    BO.ScheduleStatus ss => src.Where(x => x.ScheduleStatus == ss).ToList(),
                    _ => throw new BO.BLInvalidInputException($"Unsupported filter enum type: {f.GetType().Name}")
                };
            }

            // Important: si un des deux est null, on applique juste l'autre (et si les deux null => aucun filtre)
            list = ApplyOneFilter(list, filter1);
            list = ApplyOneFilter(list, filter2);

            // Tri par défaut (comme ton cas sorter == null dans orderInLists)
            list = list
                .OrderBy(l => l.Status)
                .ThenBy(l => l.OrderId)
                .ToList();

            return list;
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException ||
                ex is BO.BLInvalidInputException ||
                ex is BO.BLAlreadyExistsException ||
                ex is BO.BLInvalidOperationException) throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);

            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Retrieves comprehensive details for a specific order, including all associated deliveries,
    /// performance metrics, and delivery person information.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this information (must exist in the system).</param>
    /// <param name="orderId">ID of the order whose details are being requested.</param>
    /// <returns>A full Order object containing all details, delivery history, and schedule information.</returns>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester or order does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static BO.Order GetOrderDetails(int requesterId, int orderId)
    {
        try
        {
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Retrieve the order data object
            var doOrder = s_dal.Order.Read(orderId) ?? throw new BLNotFoundException($"Order with id {orderId} not found.");

            // Get all deliveries associated with this order
            var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            var lastDelivery = deliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();

            var config = AdminManager.GetConfig();

            // Retrieve or geocode coordinates for distance calculation
            double lat = doOrder.Latitude ?? 0;
            double lon = doOrder.Longitude ?? 0;
            if (lat == 0 && lon == 0)
            {
                try
                {
                    // Attempt to geocode the customer address if coordinates are missing
                    var coords = Tools.GetCoordinatesFromAddressAsync(doOrder.CustomerAddress).GetAwaiter().GetResult();
                    lat = coords.Latitude;
                    lon = coords.Longitude;
                }
                catch
                {
                    // Fallback to 0,0 if geocoding fails
                }
            }

            // Calculate straight-line distance from company to customer
            double distance = Tools.BirdDistance(config.CompanyLatitude, config.CompanyLongitude, lat, lon);

            // Choose speed based on the last delivery's transport method, or default to car speed
            double speed = config.CarSpeed;
            if (lastDelivery != null)
                speed = Tools.GetSpeed(lastDelivery.Transport, config);

            // Calculate estimated arrival time and maximum acceptable arrival time
            DateTime? estArrival = distance > 0 ? (DateTime?)Tools.CalculateEstimatedArrival(doOrder.OrderDate, distance, speed) : null;
            DateTime? maxArrival = estArrival?.Add(config.RiskRange);
            DateTime? realArrival = lastDelivery?.ArrivalTime;

            // Calculate order and schedule status from delivery records
            var status = Tools.CalculateOrderStatus(deliveries);
            var schedule = Tools.CalculateScheduleStatus(status, doOrder.OrderDate, estArrival, maxArrival, realArrival);

            // Calculate total estimated delivery duration
            TimeSpan arrivalEstDuration = estArrival != null ? estArrival.Value - doOrder.OrderDate : TimeSpan.Zero;

            // Map delivery records to BO.DeliveryPerOrderInList view models
            var deliveriesPerOrder = deliveries.Select(d =>
            {
                // Retrieve courier information for this delivery
                var courier = s_dal.Courier.Read(d.CourierId);
                return new BO.DeliveryPerOrderInList
                {
                    DeliveryId = d.Id,
                    CourierId = d.CourierId == 0 ? null : (int?)d.CourierId,
                    CourierName = courier?.Name ?? string.Empty,
                    PickupTime = d.PickupTime,
                    DeliveredStatus = d.DeliveredStatus.HasValue ? (BO.DeliveredStatus?)(BO.DeliveredStatus)d.DeliveredStatus.Value : null,
                    ArrivalTime = d.ArrivalTime
                };
            }).ToList();

            // Build and return the full Order view model
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
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            
            // Map Data Access Layer exceptions to Business Logic Layer exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Updates an existing order's information with new values.
    /// Handles geocoding of the customer address, validates required fields,
    /// and merges provided data with existing values using null-coalescing logic.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this update (must exist in the system).</param>
    /// <param name="order">The order object containing updated information to persist.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester or order does not exist.</exception>
    /// <exception cref="BO.BLInvalidInputException">Thrown if required fields (customer name or address) are empty.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void UpdateOrderDetails(int requesterId, BO.Order order)
    {
        try
        {
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            // Validate the order to be updated exists
            var existingOrder = s_dal.Order.Read(order.Id);
            if (existingOrder == null)
                throw new BLNotFoundException($"Order with id {order.Id} does not exist.");

            // Get coordinates with better error handling
            (double Latitude, double Longitude) coordinates;
            try
            {
                if (!string.IsNullOrWhiteSpace(order.CustomerAddress))
                {
                    // Attempt to geocode the provided customer address
                    coordinates = Tools.GetCoordinatesFromAddressAsync(order.CustomerAddress).GetAwaiter().GetResult();
                }
                else
                {
                    // Use existing coordinates if no address is provided
                    coordinates = (existingOrder.Latitude ?? 0, existingOrder.Longitude ?? 0);
                }
            }
            catch (Exception ex)
            {
                // Log the geocoding error for debugging purposes
                System.Diagnostics.Debug.WriteLine($"Geocoding failed for address '{order.CustomerAddress}': {ex.Message}");
                
                // Fallback to existing coordinates or default to 0,0
                coordinates = (existingOrder.Latitude ?? 0, existingOrder.Longitude ?? 0);
            }

            // Validate required fields are not empty
            if (string.IsNullOrWhiteSpace(order.CustomerName))
                throw new BLInvalidInputException("Customer name cannot be empty.");
        
            if (string.IsNullOrWhiteSpace(order.CustomerAddress))
                throw new BLInvalidInputException("Customer address cannot be empty.");

            // Map BO.Order to DO.Order with proper null handling and fallback to existing values
            var doOrder = new DO.Order
            {
                Id = order.Id,
                Type = (DO.OrderType)order.Type,
                CustomerName = order.CustomerName,
                CustomerAddress = order.CustomerAddress,
                // Use provided phone or keep existing
                CustomerPhone = order.CustomerPhone ?? existingOrder.CustomerPhone,
                // Use provided order date or keep existing; avoid DateTime.MinValue
                OrderDate = order.OrderDate != default ? order.OrderDate : existingOrder.OrderDate,
                // Use provided volume or keep existing
                size = order.Volume ?? existingOrder.size,
                // Use provided weight or keep existing
                weight = order.Weight ?? existingOrder.weight,
                // Use geocoded coordinates
                Latitude = coordinates.Latitude,
                Longitude = coordinates.Longitude,
                // Use provided description or keep existing
                Description = order.OrderDescription ?? existingOrder.Description,
                // Use provided fragility level or keep existing
                Fragility = order.Fragility.HasValue ? (DO.FragilityLevel)order.Fragility.Value : existingOrder.Fragility
            };
            
            // Persist the updated order to the data layer
            s_dal.Order.Update(doOrder);
            
            // Notify subscribers of both item-specific and list-level changes
            Observers.NotifyItemUpdated(order.Id);
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            // Enhanced error logging for debugging
            System.Diagnostics.Debug.WriteLine($"UpdateOrderDetails failed: {ex.GetType().Name}: {ex.Message}");
        
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            
            // Map Data Access Layer exceptions to Business Logic Layer exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation($"Unexpected error in UpdateOrderDetails: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cancels (marks as deleted) an order in the system.
    /// This is a simple deletion without validation checks or cascade deletion of associated deliveries.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this cancellation (not validated in this implementation).</param>
    /// <param name="orderId">ID of the order to be cancelled/deleted.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the order does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void CancelOrder(int requesterId, int orderId)
    {
        try
        {
            if(s_dal.Courier.Read(requesterId) == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            var existingOrder = s_dal.Order.Read(orderId);
            if (existingOrder == null)
                throw new BLNotFoundException($"Order with id {orderId} does not exist.");

            var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            BO.OrderStatus status = Tools.CalculateOrderStatus(deliveries);
            if (status == BO.OrderStatus.Returned ||
                status == BO.OrderStatus.Delivered)
                throw new BO.BLInvalidOperationException($"Order {orderId} has already been delivered or returned.");

            if(status == OrderStatus.Canceled)
                throw new BO.BLInvalidOperationException($"Order {orderId} has already been cancelled.");

            if(status == OrderStatus.Pending)
            {
                s_dal.Delivery.Create(new DO.Delivery
                {
                    OrderId = orderId,
                    CourierId = 0,
                    PickupTime = AdminManager.Now,
                    DeliveredStatus = (DO.DeliveredStatus)BO.DeliveredStatus.Canceled,
                    ArrivalTime = AdminManager.Now
                });
                Observers.NotifyItemUpdated(orderId);
                Observers.NotifyListUpdated();
                return;
            }

            if(status == OrderStatus.Processing)
            {
                DO.Delivery lastDelivery = deliveries
                    .OrderByDescending(d => d.PickupTime)
                    .First();
                s_dal.Delivery.Update(new DO.Delivery(
                    Id: lastDelivery.Id,
                    OrderId: lastDelivery.OrderId,
                    Transport: lastDelivery.Transport,
                    CourierId: lastDelivery.CourierId,
                    PickupTime: lastDelivery.PickupTime,
                    ArrivalTime: AdminManager.Now,
                    DeliveredStatus: DO.DeliveredStatus.Canceled
                    ));
                Observers.NotifyListUpdated();
                Observers.NotifyItemUpdated(orderId);

                // to send email to courier about cancellation - commented out because i don't want to spam my own email

                //var courier = s_dal.Courier.Read(lastDelivery.CourierId);
                //if (courier != null)
                //{
                //    // Notify the courier about the cancellation
                //    string subject = $"Order Cancellation Notification - Order #{orderId}";
                //    string body = $"Dear {courier.CourierName},\n\n" +
                //                  $"We regret to inform you that Order #{orderId} has been cancelled while it was in processing.\n" +
                //                  $"Please stop the delivery and return to the hub.\n\n" +
                //                  $"Best regards,\n" +
                //                  $"Delivery Management Team";

                //    using var message = new MailMessage(
                //        from: "noreply@wolt2.0.com",
                //        to: courier.Email,
                //        subject: subject,
                //        body: body);

                //    using var smtpClient = new SmtpClient("smtp.wolt2.0.com")
                //    {
                //        EnableSsl = true,
                //        Credentials = new NetworkCredential("noreply@wolt2.0", "securepassword")
                //    };
                //    smtpClient.Send(message);
                //}
                return;
            }
        }
        catch (Exception ex)
        {
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            
            // Map Data Access Layer exceptions to Business Logic Layer exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Removes an order and all associated deliveries from the system.
    /// Validates that the requester exists before performing the removal.
    /// Performs cascade deletion: first removes all deliveries, then the order.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this removal (must exist in the system).</param>
    /// <param name="orderId">ID of the order to be removed along with its deliveries.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester or order does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void RemoveOrder(int requesterId, int orderId)
    {
        try
        {
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            if(s_dal.Order.Read(orderId) == null)
                throw new BLNotFoundException($"Order with id {orderId} does not exist.");

            // Get all deliveries associated with this order
            var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            
            // Delete each delivery first (cascade delete)
            foreach (var d in deliveries)
                s_dal.Delivery.Delete(d.Id);

            // Then delete the order itself
            s_dal.Order.Delete(orderId);
            
            // Notify subscribers that this order and its deliveries have been removed
            Observers.NotifyItemUpdated(orderId);
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            
            // Map Data Access Layer exceptions to Business Logic Layer exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Adds a new order to the system after validating the requester.
    /// Handles geocoding of the customer address, validates required fields,
    /// and creates the order with a valid start date.
    /// </summary>
    /// <param name="requesterId">ID of the user creating this order (must exist in the system).</param>
    /// <param name="order">The order object containing all details to be added.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester does not exist.</exception>
    /// <exception cref="BO.BLInvalidInputException">Thrown if required fields (customer name or address) are empty.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void AddOrder(int requesterId, BO.Order order)
    {
        try
        {
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            // Get coordinates with better error handling
            (double Latitude, double Longitude) coordinates;
            try
            {
                if (!string.IsNullOrWhiteSpace(order.CustomerAddress))
                {
                    // Attempt to geocode the provided customer address
                    coordinates = Tools.GetCoordinatesFromAddressAsync(order.CustomerAddress).GetAwaiter().GetResult();
                }
                else
                {
                    // Default coordinates if no address provided
                    coordinates = (0, 0);
                }
            }
            catch (Exception ex)
            {
                // Log the geocoding error for debugging purposes
                System.Diagnostics.Debug.WriteLine($"Geocoding failed for address '{order.CustomerAddress}': {ex.Message}");
                
                // Fallback to default coordinates
                coordinates = (0, 0);
            }

            // Validate required fields are not empty
            if (string.IsNullOrWhiteSpace(order.CustomerName))
                throw new BLInvalidInputException("Customer name cannot be empty.");
    
            if (string.IsNullOrWhiteSpace(order.CustomerAddress))
                throw new BLInvalidInputException("Customer address cannot be empty.");

            // Ensure StartDate is valid (avoid DateTime.MinValue)
            DateTime startDate = order.OrderDate == default ? AdminManager.Now : order.OrderDate;

            // Map BO.Order to DO.Order before persisting to data layer
            var doOrder = new DO.Order
            {
                Id = order.Id,
                Type = (DO.OrderType)order.Type,
                CustomerName = order.CustomerName,
                CustomerAddress = order.CustomerAddress,
                CustomerPhone = order.CustomerPhone,
                OrderDate = startDate,
                size = order.Volume,
                weight = order.Weight,
                // Use geocoded coordinates
                Latitude = coordinates.Latitude,
                Longitude = coordinates.Longitude,
                Description = order.OrderDescription,
                // Convert nullable fragility level to DO layer
                Fragility = order.Fragility.HasValue ? (DO.FragilityLevel)order.Fragility.Value : null
            };
            
            // Persist the new order to the data layer
            s_dal.Order.Create(doOrder);
            
            // Notify subscribers that the order list has been updated
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            // Enhanced error logging for debugging
            System.Diagnostics.Debug.WriteLine($"AddOrder failed: {ex.GetType().Name}: {ex.Message}");
    
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            
            // Map Data Access Layer exceptions to Business Logic Layer exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation($"Unexpected error in AddOrder: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Marks a delivery as completed by recording the actual arrival time and distance traveled.
    /// Updates the delivery status to Delivered and attempts to update the courier's activity status.
    /// Notifies observers of the order completion.
    /// </summary>
    /// <param name="requesterId">ID of the user marking the delivery as complete (must exist in the system).</param>
    /// <param name="courierId">ID of the courier who completed the delivery.</param>
    /// <param name="deliveryId">ID of the delivery being marked as completed.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester, courier, delivery, or associated order does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void FinishOrder(int requesterId, int courierId, int deliveryId,BO.DeliveredStatus deliveredStatus)
    {
        try
        {
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Validate courier, delivery, and associated order exist
            var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");
            var delivery = s_dal.Delivery.Read(deliveryId) ?? throw new BLNotFoundException($"Delivery {deliveryId} not found.");

            var order = s_dal.Order.Read(delivery.OrderId) ?? throw new BLNotFoundException($"Order {delivery.OrderId} not found.");
            var config = AdminManager.GetConfig();

            // Compute coordinates and distance for the delivery
            
            double lat = order.Latitude ?? 0;
            double lon = order.Longitude ?? 0;
            if (lat == 0 && lon == 0)
            {
                try
                {
                    // Attempt to geocode the customer address if coordinates are missing
                    var coords = Tools.GetCoordinatesFromAddressAsync(order.CustomerAddress).GetAwaiter().GetResult();
                    lat = coords.Latitude;
                    lon = coords.Longitude;
                }
                catch { }
            }

            // Calculate the actual distance traveled
            double distance = Tools.BirdDistance(config.CompanyLatitude, config.CompanyLongitude, lat, lon);

            // Update the delivery record with completion information
            var updated = delivery with
            {
                // Record the current time as the arrival time
                ArrivalTime = AdminManager.Now,
                // Mark status as delivered
                DeliveredStatus = (DO.DeliveredStatus)deliveredStatus
            };

            s_dal.Delivery.Update(updated);

            // Notify subscribers that this order has been completed
            Observers.NotifyItemUpdated(delivery.OrderId);
            Observers.NotifyListUpdated();
            Observers.NotifyItemUpdated(courierId);

            CourierManager.Observers.NotifyItemUpdated(courierId);
            CourierManager.Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            
            // Map Data Access Layer exceptions to Business Logic Layer exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Assigns an order to a courier by creating a new delivery record.
    /// Validates that the courier is active and is not a Director before assignment.
    /// Sets the delivery transport based on the courier's preferred transport method.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this assignment (must exist in the system).</param>
    /// <param name="orderId">ID of the order to be assigned.</param>
    /// <param name="courierId">ID of the courier to receive the assignment.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester, courier, or order does not exist.</exception>
    /// <exception cref="BO.BLInvalidOperationException">Thrown if the courier is inactive or holds a Director role.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void AssignOrderToCourier(int requesterId, int orderId, int courierId)
    {
        try
        {
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Validate courier and order exist
            var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");
            var order = s_dal.Order.Read(orderId) ?? throw new BLNotFoundException($"Order {orderId} not found.");

            // Verify courier is active before assignment
            if (!courier.IsActive)
                throw new BO.BLInvalidOperationException($"Cannot assign order {orderId} to courier {courierId}: courier is not active.");

            // Verify courier is not a Director (delivery personnel must be Couriers, not Directors)
            if (courier.Administrator == DO.Administrator.Director)
                throw new BO.BLInvalidOperationException($"Cannot assign order {orderId} to courier {courierId}: courier is a Director.");

            double? distance = null;
            try
            {
                (double Latitude, double Longitude) coord = Tools.GetCoordinatesFromAddressAsync(order.CustomerAddress).GetAwaiter().GetResult();
                distance = Tools.BirdDistance(AdminManager.GetConfig().CompanyLatitude, AdminManager.GetConfig().CompanyLongitude, coord.Latitude, coord.Longitude);
            }
            catch { }

            // Create a new delivery record assigned to the courier
            var delivery = new DO.Delivery
            {
                // Set ID to 0 to let DAL auto-generate it
                Id = 0,
                OrderId = orderId,
                // Use the courier's preferred transport method
                Transport = courier.Transport,
                CourierId = courierId,
                // Set pickup time to now
                PickupTime = AdminManager.Now,
                // Arrival time will be filled when delivery is completed
                ArrivalTime = null,
                // Distance will be calculated when delivery is completed
                Distance = distance,
                // Mark delivery as null (because is not yet delivered)
                DeliveredStatus = null
            };

            // Persist the new delivery to the data layer
            s_dal.Delivery.Create(delivery);
            
            // Notify subscribers that the order has been assigned
            Observers.NotifyItemUpdated(orderId);
            Observers.NotifyListUpdated();

            // Notify COURIER subscribers (THIS is what was missing)
            CourierManager.Observers.NotifyItemUpdated(courierId);
            CourierManager.Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            
            // Map Data Access Layer exceptions to Business Logic Layer exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Retrieves a list of completed deliveries for a specific courier with optional filtering by order type
    /// and sorting options.
    /// Only includes deliveries with recorded arrival times (completed deliveries).
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this list (must exist in the system).</param>
    /// <param name="courierId">ID of the courier whose closed deliveries are being retrieved.</param>
    /// <param name="filter">Optional filter by order type (FastFood, Pizza, etc.).</param>
    /// <param name="sorter">Optional sort key: supports "DeliveryTotalTime" and "ActualDistance".</param>
    /// <returns>An enumerable collection of ClosedDeliveryInList objects representing completed deliveries.</returns>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester or courier does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static IEnumerable<BO.ClosedDeliveryInList> GetClosedDeliveriesForCourier(
        int requesterId,
        int courierId,
        BO.OrderType? filter,
        Enum? sorter)
    {
        try
        {
            // Validate requester exists
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BO.BLNotFoundException("Requester does not exist.");

            // Validate courier exists
            var courier = s_dal.Courier.Read(courierId)
                ?? throw new BO.BLNotFoundException($"Courier {courierId} not found.");

            // Authorization (reasonable per "main management" + courier screen):
            // allow the courier himself or the main boss/admin
            var config = AdminManager.GetConfig();
            if (requesterId != courierId && requesterId != config.BossId)
                throw new BO.BLInvalidOperationException("Requester is not authorized to view this courier history.");

            // "Closed deliveries" = deliveries with end-time AND end-type (DeliveredStatus != null)
            // (tiour: DeliveredStatus is the delivery end type, nullable until closed)
            var deliveries = s_dal.Delivery
                .ReadAll(d => d.CourierId == courierId && d.ArrivalTime != null && d.DeliveredStatus != null)
                .ToList();

            // Load all orders for lookup
            var orders = s_dal.Order.ReadAll().ToDictionary(o => o.Id);

            // Project
            var list = deliveries.Select(d =>
            {
                orders.TryGetValue(d.OrderId, out var o);

                // Map DO.DeliveredStatus -> BO.DeliveredStatus
                // Adjust mapping names if your enums differ.
                BO.DeliveredStatus endType = d.DeliveredStatus switch
                {
                    DO.DeliveredStatus.Delivered => BO.DeliveredStatus.Delivered,
                    DO.DeliveredStatus.Canceled => BO.DeliveredStatus.Canceled,
                    DO.DeliveredStatus.Rejected => BO.DeliveredStatus.Rejected,
                    DO.DeliveredStatus.Failed => BO.DeliveredStatus.Failed,
                    DO.DeliveredStatus.Absent => BO.DeliveredStatus.Absent,
                    _ => BO.DeliveredStatus.Failed
                };

                return new BO.ClosedDeliveryInList
                {
                    DeliveryId = d.Id,
                    OrderId = d.OrderId,
                    OrderType = o != null ? (BO.OrderType)o.Type : BO.OrderType.FastFood,
                    CustomerAdress = o?.CustomerAddress ?? string.Empty,
                    DeliveryTransport = (BO.DeliveryTransport)d.Transport,
                    ActualDistance = d.Distance,
                    DeliveryTotalTime = d.ArrivalTime!.Value - d.PickupTime,
                    DeliveredStatus = endType
                };
            }).ToList();

            // Filter: if filter is null => full list; else by OrderType
            if (filter.HasValue)
                list = list.Where(x => x.OrderType == filter.Value).ToList();

            // Sorting:
            // If sorter is null => default sort by DeliveredStatus (and then stable by DeliveryId)
            if (sorter == null)
            {
                list = list
                    .OrderBy(x => x.DeliveredStatus)
                    .ThenBy(x => x.DeliveryId)
                    .ToList();
            }
            else
            {
                string key = sorter.ToString() ?? string.Empty;

                list = key switch
                {
                    "DeliveryTotalTime" => list.OrderBy(x => x.DeliveryTotalTime).ThenBy(x => x.DeliveryId).ToList(),
                    "ActualDistance" => list.OrderBy(x => x.ActualDistance).ThenBy(x => x.DeliveryId).ToList(),
                    "DeliveredStatus" => list.OrderBy(x => x.DeliveredStatus).ThenBy(x => x.DeliveryId).ToList(),
                    "OrderType" => list.OrderBy(x => x.OrderType).ThenBy(x => x.DeliveryId).ToList(),
                    "OrderId" => list.OrderBy(x => x.OrderId).ToList(),
                    _ => list
                        .OrderBy(x => x.DeliveredStatus)
                        .ThenBy(x => x.DeliveryId)
                        .ToList()
                };
            }

            return list;
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException ||
                ex is BO.BLInvalidInputException ||
                ex is BO.BLAlreadyExistsException ||
                ex is BO.BLInvalidOperationException) throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);

            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static IEnumerable<BO.OpenOrderInList> GetOpenOrdersForCourier(
        int requesterId,
        int courierId,
        Enum? filter,
        Enum? sorter)
    {
        try
        {
            // Validate requester exists
            _ = s_dal.Courier.Read(requesterId)
                ?? throw new BLNotFoundException("Requester does not exist.");

            // Validate courier exists
            var courier = s_dal.Courier.Read(courierId)
                ?? throw new BLNotFoundException($"Courier {courierId} not found.");

            var config = AdminManager.GetConfig();
            DateTime now = config.Clock;

            // Company coordinates come from admin/config (as you said)
            double companyLat = config.CompanyLatitude;
            double companyLon = config.CompanyLongitude;

            // Read all orders and deliveries once
            var orders = s_dal.Order.ReadAll().ToList();
            var deliveriesAll = s_dal.Delivery.ReadAll().ToList();

            var result = new List<BO.OpenOrderInList>();

            foreach (var o in orders)
            {
                // Compute order status from deliveries
                var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();
                var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);

                // Only open orders (not closed)
                if (orderStatus == BO.OrderStatus.Delivered ||
                    orderStatus == BO.OrderStatus.Returned ||
                    orderStatus == BO.OrderStatus.Canceled)
                    continue;

                // If there is any open delivery assigned to a courier -> not available
                var openDeliveries = orderDeliveries.Where(d => d.ArrivalTime == null).ToList();
                if (openDeliveries.Any(d => d.CourierId != 0))
                    continue;

                // Resolve customer coordinates (fallback: geocode from address)
                double custLat = o.Latitude ?? 0;
                double custLon = o.Longitude ?? 0;

                if (custLat == 0 && custLon == 0 && !string.IsNullOrWhiteSpace(o.CustomerAddress))
                {
                    try
                    {
                        var coords = Tools.GetCoordinatesFromAddressAsync(o.CustomerAddress).GetAwaiter().GetResult();
                        custLat = coords.Latitude;
                        custLon = coords.Longitude;
                    }
                    catch
                    {
                        // If we cannot locate the customer, we cannot calculate distance reliably -> skip
                        continue;
                    }
                }

                // Bird distance is measured from the company (per your project design)
                double bird = Tools.BirdDistance(companyLat, companyLon, custLat, custLon);

                // Filter by courier personal max distance (if defined)
                if (courier.MaxDistance != null && bird > courier.MaxDistance.Value)
                    continue;

                // Added time since order creation
                TimeSpan? addedTime = now - o.OrderDate;

                // Estimated delivery time:
                // Use courier transport speed from config; estimate from "now" using bird distance.
                double speed = Tools.GetSpeed(courier.Transport, config);
                DateTime? estArrival = Tools.CalculateEstimatedArrival(now, bird, speed);
                TimeSpan estSpan = estArrival.HasValue ? (estArrival.Value - now) : TimeSpan.Zero;

                // Latest acceptable delivery time (orderDate + MaxDeliveryTime)
                DateTime maxDeliveredTime = o.OrderDate + config.MaxDeliveryTime;

                // Schedule status based on your updated rules (no Unknown)
                var scheduleStatus = Tools.CalculateScheduleStatus(
                    orderStatus,
                    o.OrderDate,
                    estArrival,
                    maxDeliveredTime,
                    null);

                result.Add(new BO.OpenOrderInList
                {
                    CourierId = null,
                    OrderId = o.Id,
                    OrderType = (BO.OrderType)o.Type,
                    Fragility = o.Fragility != null
                        ? (BO.FragilityLevel?)(BO.FragilityLevel)o.Fragility.Value
                        : null,
                    CustomerAddress = o.CustomerAddress ?? string.Empty,
                    BirdDistance = bird,
                    Distance = null, // route distance not calculated here
                    AddedTime = addedTime,
                    ScheduleStatus = scheduleStatus,
                    EstimatedDeliveryTime = estSpan,
                    MaxDeliveredTime = maxDeliveredTime
                });
            }

            // Optional filter by OrderType (nullable enum): null => full list
            if (filter != null)
                result = result.Where(x => x.OrderType.Equals(filter)).ToList();

            // Sorting: if sorter is null => sort by ScheduleStatus
            if (sorter == null)
            {
                static int Rank(BO.ScheduleStatus s) => s switch
                {
                    BO.ScheduleStatus.OnTime => 0,
                    BO.ScheduleStatus.InRisk => 1,
                    BO.ScheduleStatus.Late => 2,
                    _ => 3
                };

                result = result.OrderBy(x => Rank(x.ScheduleStatus)).ToList();
            }
            else
            {
                string key = sorter.ToString() ?? string.Empty;

                result = key switch
                {
                    "BirdDistance" => result.OrderBy(x => x.BirdDistance).ToList(),
                    "AddedTime" => result.OrderBy(x => x.AddedTime).ToList(),
                    "ScheduleStatus" => result.OrderBy(x => x.ScheduleStatus).ToList(),
                    "EstimatedDeliveryTime" => result.OrderBy(x => x.EstimatedDeliveryTime).ToList(),
                    "MaxDeliveredTime" => result.OrderBy(x => x.MaxDeliveredTime).ToList(),
                    _ => result
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException ||
                ex is BO.BLInvalidInputException ||
                ex is BO.BLAlreadyExistsException ||
                ex is BO.BLInvalidOperationException) throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException)
                throw new BO.BLFailedOperation(ex.Message, ex);

            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }
}
