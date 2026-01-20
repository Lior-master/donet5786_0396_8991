using BO;
using DalApi;
using DO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            TimeSpan maxDeliveryTime;
            lock (AdminManager.BlMutex)
            {
                maxDeliveryTime = s_dal.Config.MaxTimeDelivery;
            }

            // Snapshot reads to minimize DAL calls
            List<DO.Order> ordersAll;
            List<DO.Delivery> deliveriesAll;
            lock (AdminManager.BlMutex)
            {
                ordersAll = s_dal.Order.ReadAll().ToList();
                deliveriesAll = s_dal.Delivery.ReadAll().ToList();
            }

            bool deliveriesUpdated = false;
            var updatedOrders = new HashSet<int>();
            var updatedCouriers = new HashSet<int>();

            
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

                    lock (AdminManager.BlMutex)
                    {
                        s_dal.Delivery.Update(upd);
                    }
                    
                    deliveriesUpdated = true;
                    updatedOrders.Add(d.OrderId);
                    if (d.CourierId != 0)
                        updatedCouriers.Add(d.CourierId);
                }
            }

            // Notify observers if any deliveries were updated
            if (deliveriesUpdated)
            {
                CourierManager.InvalidateDeliveryCache();
                foreach (var orderId in updatedOrders)
                    Observers.NotifyItemUpdated(orderId);

                Observers.NotifyListUpdated();
                foreach (var courierId in updatedCouriers)
                    CourierManager.Observers.NotifyItemUpdated(courierId);
                if (updatedCouriers.Count > 0)
                    CourierManager.Observers.NotifyListUpdated();
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
    internal static async Task<IEnumerable<int>> GetOrderSummaryAsync(int requesterId)
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

            var list = (await orderInListsAsync(requesterId, null, null, null).ConfigureAwait(false)).ToList();

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
    private static async Task<BO.OrderInList> BuildOrderInListAsync(
        DO.Order order,
        List<DO.Delivery> deliveriesDO,
        BO.Config config,
        DateTime now)
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
            var coords = await Tools.TryGetCoordinatesFromAddressAsync(order.CustomerAddress).ConfigureAwait(false);
            if (coords.HasValue)
            {
                lat = coords.Value.Latitude;
                lon = coords.Value.Longitude;
            }
        }

        double distance = (lat == 0 && lon == 0)
            ? 0
            : await Tools.BirdDistanceAsync(config.CompanyLatitude, config.CompanyLongitude, lat, lon).ConfigureAwait(false);

        double speed = config.CarSpeed;
        if (lastByPickup != null)
            speed = await Tools.GetSpeedAsync(lastByPickup.Transport, config).ConfigureAwait(false);

        DateTime? estArrival = distance > 0
            ? await Tools.CalculateEstimatedArrivalAsync(order.OrderDate, distance, speed).ConfigureAwait(false)
            : null;

        DateTime maxArrival = order.OrderDate.Add(config.MaxDeliveryTime);

        var orderStatus = await Tools.CalculateOrderStatusAsync(orderDeliveries).ConfigureAwait(false);

        var schedule = await Tools.CalculateScheduleStatusAsync(
            orderStatus,
            order.OrderDate,
            estArrival,
            maxArrival,
            realArrival
        ).ConfigureAwait(false);

        // Ensure TreatmentEndTime is never negative
        TimeSpan treatmentTime = TimeSpan.Zero;
        if (lastByPickup != null)
        {
            var rawTreatmentTime = lastByPickup.PickupTime - order.OrderDate;
            treatmentTime = rawTreatmentTime > TimeSpan.Zero ? rawTreatmentTime : TimeSpan.Zero;
        }

        return new BO.OrderInList
        {
            DeliveryId = deliveryId,
            OrderId = order.Id,
            Type = (BO.OrderType)order.Type,
            Distance = distance,
            Status = orderStatus,
            ScheduleStatus = schedule,
            OrderEndTime = realArrival != null ? realArrival.Value - order.OrderDate : now - order.OrderDate,
            TreatmentEndTime = treatmentTime, // No longer negative
            NumberOfCouriers = orderDeliveries.Select(d => d.CourierId).Distinct().Count()
        };
    }

    private static async Task<List<BO.OrderInList>> BuildOrderInListsAsync(
        List<DO.Order> doOrders,
        List<DO.Delivery> deliveriesDO,
        BO.Config config,
        DateTime now)
    {
        IEnumerable<Task<BO.OrderInList>> tasks = doOrders.Select(order =>
            BuildOrderInListAsync(order, deliveriesDO, config, now));

        var list = new List<BO.OrderInList>();
        foreach (var task in tasks)
            list.Add(await task.ConfigureAwait(false));

        return list;
    }

    internal static async Task<IEnumerable<BO.OrderInList>> orderInListsAsync(int requesterId, Enum? filter, object? Object, Enum? sorter)
    {
        try
        {
            var config = AdminManager.GetConfig();
            if (requesterId != config.BossId)
                throw new BO.BLInvalidOperationException("Requester is not authorized for order management operations.");

            List<DO.Order> doOrders;
            List<DO.Delivery> deliveriesDO;
            lock (AdminManager.BlMutex)
            {
                doOrders = s_dal.Order.ReadAll().ToList();
                deliveriesDO = s_dal.Delivery.ReadAll().ToList();
            }
            
            var now = AdminManager.Now;

            var list = await BuildOrderInListsAsync(doOrders, deliveriesDO, config, now).ConfigureAwait(false);

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
                ex is BO.BLInvalidOperationException)
                throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);

            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static async Task<IEnumerable<BO.OrderInList>> orderInListsDoubleFilterAsync(int requesterId, Enum? filter1, Enum? filter2)
    {
        try
        {
            var config = AdminManager.GetConfig();
            if (requesterId != config.BossId)
                throw new BO.BLInvalidOperationException("Requester is not authorized for order management operations.");

            List<DO.Order> doOrders;
            List<DO.Delivery> deliveriesDO;
            lock (AdminManager.BlMutex)
            {
                doOrders = s_dal.Order.ReadAll().ToList();
                deliveriesDO = s_dal.Delivery.ReadAll().ToList();
            }
            
            var now = AdminManager.Now;

            var list = await BuildOrderInListsAsync(doOrders, deliveriesDO, config, now).ConfigureAwait(false);

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

            list = ApplyOneFilter(list, filter1);
            list = ApplyOneFilter(list, filter2);

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
    internal static async Task<BO.Order> GetOrderDetailsAsync(int requesterId, int orderId)
    {
        try
        {
            // Validate requester exists in the system
            DO.Courier? requester;
            lock (AdminManager.BlMutex)
            {
                requester = s_dal.Courier.Read(requesterId);
            }
            
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Retrieve the order data object
            DO.Order? doOrder;
            lock (AdminManager.BlMutex)
            {
                doOrder = s_dal.Order.Read(orderId);
            }
            
            if (doOrder == null)
                throw new BLNotFoundException($"Order with id {orderId} not found.");

            // Get all deliveries associated with this order
            List<DO.Delivery> deliveries;
            lock (AdminManager.BlMutex)
            {
                deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            }
            
            var impactedCouriers = deliveries
                .Where(d => d.CourierId != 0)
                .Select(d => d.CourierId)
                .Distinct()
                .ToList();
            var lastDelivery = deliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();

            var config = AdminManager.GetConfig();

            // Retrieve or geocode coordinates for distance calculation
            double lat = doOrder.Latitude ?? 0;
            double lon = doOrder.Longitude ?? 0;
            if (lat == 0 && lon == 0)
            {
                var coords = await Tools.TryGetCoordinatesFromAddressAsync(doOrder.CustomerAddress).ConfigureAwait(false);
                if (coords.HasValue)
                {
                    lat = coords.Value.Latitude;
                    lon = coords.Value.Longitude;
                }
            }

            // Calculate straight-line distance from company to customer
            double distance = await Tools.BirdDistanceAsync(config.CompanyLatitude, config.CompanyLongitude, lat, lon)
                .ConfigureAwait(false);

            // Choose speed based on the last delivery's transport method, or default to car speed
            double speed = config.CarSpeed;
            if (lastDelivery != null)
                speed = await Tools.GetSpeedAsync(lastDelivery.Transport, config).ConfigureAwait(false);

            // Calculate estimated arrival time and maximum acceptable arrival time
            DateTime? estArrival = distance > 0
                ? (DateTime?)await Tools.CalculateEstimatedArrivalAsync(doOrder.OrderDate, distance, speed)
                    .ConfigureAwait(false)
                : null;
            DateTime? maxArrival = estArrival?.Add(config.RiskRange);
            DateTime? realArrival = lastDelivery?.ArrivalTime;

            // Calculate order and schedule status from delivery records
            var status = await Tools.CalculateOrderStatusAsync(deliveries).ConfigureAwait(false);
            var schedule = await Tools.CalculateScheduleStatusAsync(status, doOrder.OrderDate, estArrival, maxArrival, realArrival)
                .ConfigureAwait(false);

            // Calculate total estimated delivery duration
            TimeSpan arrivalEstDuration = estArrival != null ? estArrival.Value - doOrder.OrderDate : TimeSpan.Zero;

            // Map delivery records to BO.DeliveryPerOrderInList view models
            var deliveriesPerOrder = deliveries.Select(d =>
            {
                // Retrieve courier information for this delivery
                DO.Courier? courier;
                lock (AdminManager.BlMutex)
                {
                    courier = s_dal.Courier.Read(d.CourierId);
                }
                
                return new BO.DeliveryPerOrderInList
                {
                    DeliveryId = d.Id,
                    CourierId = d.CourierId == 0 ? null : (int?)d.CourierId,
                    CourierName = courier?.Name ?? string.Empty,
                    transport = (BO.DeliveryTransport)d.Transport,
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
    internal static async Task UpdateOrderDetailsAsync(int requesterId, BO.Order order)
    {
        try
        {
            // Validate requester exists in the system
            DO.Courier? requester;
            lock (AdminManager.BlMutex)
            {
                requester = s_dal.Courier.Read(requesterId);
            }
            
            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            // Validate the order to be updated exists
            DO.Order? existingOrder;
            lock (AdminManager.BlMutex)
            {
                existingOrder = s_dal.Order.Read(order.Id);
            }
            
            if (existingOrder == null)
                throw new BLNotFoundException($"Order with id {order.Id} does not exist.");

            // Validate required fields are not empty
            if (string.IsNullOrWhiteSpace(order.CustomerName))
                throw new BLInvalidInputException("Customer name cannot be empty.");
        
            if (string.IsNullOrWhiteSpace(order.CustomerAddress))
                throw new BLInvalidInputException("Customer address cannot be empty.");

            string addressToSave = order.CustomerAddress;
            bool badAddress = false;

            double? latitude = existingOrder.Latitude;
            double? longitude = existingOrder.Longitude;
            bool addressChanged = !string.Equals(existingOrder.CustomerAddress, order.CustomerAddress, StringComparison.Ordinal);
            if (addressChanged)
            {
                latitude = null;
                longitude = null;

                if (!string.IsNullOrWhiteSpace(order.CustomerAddress) &&
                    !string.Equals(order.CustomerAddress.Trim(), Tools.InvalidAddressMarker, StringComparison.OrdinalIgnoreCase))
                {
                    var coords = await Tools.TryGetCoordinatesFromAddressAsync(order.CustomerAddress).ConfigureAwait(false);
                    if (coords.HasValue)
                    {
                        latitude = coords.Value.Latitude;
                        longitude = coords.Value.Longitude;
                    }
                    else
                    {
                        addressToSave = Tools.InvalidAddressMarker;
                        badAddress = true;
                    }
                }
                else
                {
                    addressToSave = Tools.InvalidAddressMarker;
                    badAddress = true;
                }
            }

            // Map BO.Order to DO.Order with proper null handling and fallback to existing values
            var doOrder = new DO.Order
            {
                Id = order.Id,
                Type = (DO.OrderType)order.Type,
                CustomerName = order.CustomerName,
                CustomerAddress = addressToSave,
                // Use provided phone or keep existing
                CustomerPhone = order.CustomerPhone ?? existingOrder.CustomerPhone,
                // Use provided order date or keep existing; avoid DateTime.MinValue
                OrderDate = order.OrderDate != default ? order.OrderDate : existingOrder.OrderDate,
                // Use provided volume or keep existing
                size = order.Volume ?? existingOrder.size,
                // Use provided weight or keep existing
                weight = order.Weight ?? existingOrder.weight,
                // Use geocoded coordinates
                Latitude = latitude,
                Longitude = longitude,
                // Use provided description or keep existing
                Description = order.OrderDescription ?? existingOrder.Description,
                // Use provided fragility level or keep existing
                Fragility = order.Fragility.HasValue ? (DO.FragilityLevel)order.Fragility.Value : existingOrder.Fragility
            };
            
            // Persist the updated order to the data layer
            lock (AdminManager.BlMutex)
            {
                s_dal.Order.Update(doOrder);
            }
            
            // Notify subscribers of both item-specific and list-level changes
            Observers.NotifyItemUpdated(order.Id);
            Observers.NotifyListUpdated();

            if (badAddress)
                throw new BLBadAddressException("Customer address is invalid. Order saved with INVALID_ADDRESS.");
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
            DO.Courier? requester;
            lock (AdminManager.BlMutex)
            {
                requester = s_dal.Courier.Read(requesterId);
            }
            
            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            DO.Order? existingOrder;
            lock (AdminManager.BlMutex)
            {
                existingOrder = s_dal.Order.Read(orderId);
            }
            
            if (existingOrder == null)
                throw new BLNotFoundException($"Order with id {orderId} does not exist.");

            List<DO.Delivery> deliveries;
            lock (AdminManager.BlMutex)
            {
                deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            }
            
            BO.OrderStatus status = Tools.CalculateOrderStatus(deliveries);
            if (status == BO.OrderStatus.Returned ||
                status == BO.OrderStatus.Delivered)
                throw new BO.BLInvalidOperationException($"Order {orderId} has already been delivered or returned.");

            if (status == OrderStatus.Canceled)
                throw new BO.BLInvalidOperationException($"Order {orderId} has already been cancelled.");

            if (status == OrderStatus.Pending)
            {
                lock (AdminManager.BlMutex)
                {
                    s_dal.Delivery.Create(new DO.Delivery
                    {
                        OrderId = orderId,
                        CourierId = 0,
                        PickupTime = AdminManager.Now,
                        DeliveredStatus = (DO.DeliveredStatus)BO.DeliveredStatus.Canceled,
                        ArrivalTime = AdminManager.Now
                    });
                }
                
                CourierManager.InvalidateDeliveryCache();
                Observers.NotifyItemUpdated(orderId);
                Observers.NotifyListUpdated();
                return;
            }

            if (status == OrderStatus.Processing)
            {
                DO.Delivery lastDelivery = deliveries
                    .OrderByDescending(d => d.PickupTime)
                    .First();
                    
                lock (AdminManager.BlMutex)
                {
                    s_dal.Delivery.Update(new DO.Delivery(
                        Id: lastDelivery.Id,
                        OrderId: lastDelivery.OrderId,
                        Transport: lastDelivery.Transport,
                        CourierId: lastDelivery.CourierId,
                        PickupTime: lastDelivery.PickupTime,
                        ArrivalTime: AdminManager.Now,
                        DeliveredStatus: DO.DeliveredStatus.Canceled
                    ));
                }
                
                CourierManager.InvalidateDeliveryCache();
                Observers.NotifyListUpdated();
                Observers.NotifyItemUpdated(orderId);
                if (lastDelivery.CourierId != 0)
                {
                    CourierManager.Observers.NotifyItemUpdated(lastDelivery.CourierId);
                    CourierManager.Observers.NotifyListUpdated();
                }

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
            DO.Courier? requester;
            lock (AdminManager.BlMutex)
            {
                requester = s_dal.Courier.Read(requesterId);
            }
            
            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            DO.Order? orderToDelete;
            lock (AdminManager.BlMutex)
            {
                orderToDelete = s_dal.Order.Read(orderId);
            }
            
            if (orderToDelete == null)
                throw new BLNotFoundException($"Order with id {orderId} does not exist.");

            // Get all deliveries associated with this order
            List<DO.Delivery> deliveries;
            lock (AdminManager.BlMutex)
            {
                deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            }
            
            var impactedCouriers = deliveries
                .Where(d => d.CourierId != 0)
                .Select(d => d.CourierId)
                .Distinct()
                .ToList();
            
            // Delete each delivery first (cascade delete)
            lock (AdminManager.BlMutex)
            {
                foreach (var d in deliveries)
                    s_dal.Delivery.Delete(d.Id);

                // Then delete the order itself
                s_dal.Order.Delete(orderId);
            }
            
            CourierManager.InvalidateDeliveryCache();
            
            // Notify subscribers that this order and its deliveries have been removed
            Observers.NotifyItemUpdated(orderId);
            Observers.NotifyListUpdated();
            foreach (var courierId in impactedCouriers)
                CourierManager.Observers.NotifyItemUpdated(courierId);
            if (impactedCouriers.Count > 0)
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
    /// Adds a new order to the system after validating the requester.
    /// Handles geocoding of the customer address, validates required fields,
    /// and creates the order with a valid start date.
    /// </summary>
    /// <param name="requesterId">ID of the user creating this order (must exist in the system).</param>
    /// <param name="order">The order object containing all details to be added.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester does not exist.</exception>
    /// <exception cref="BO.BLInvalidInputException">Thrown if required fields (customer name or address) are empty.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static async Task AddOrderAsync(int requesterId, BO.Order order)
    {
        try
        {
            // Validate requester exists in the system
            DO.Courier? requester;
            lock (AdminManager.BlMutex)
            {
                requester = s_dal.Courier.Read(requesterId);
            }
            
            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            // Validate required fields are not empty
            if (string.IsNullOrWhiteSpace(order.CustomerName))
                throw new BLInvalidInputException("Customer name cannot be empty.");
    
            if (string.IsNullOrWhiteSpace(order.CustomerAddress))
                throw new BLInvalidInputException("Customer address cannot be empty.");

            string addressToSave = order.CustomerAddress;
            bool badAddress = false;
            double? latitude = null;
            double? longitude = null;

            if (!string.IsNullOrWhiteSpace(order.CustomerAddress) &&
                !string.Equals(order.CustomerAddress.Trim(), Tools.InvalidAddressMarker, StringComparison.OrdinalIgnoreCase))
            {
                var coords = await Tools.TryGetCoordinatesFromAddressAsync(order.CustomerAddress).ConfigureAwait(false);
                if (coords.HasValue)
                {
                    latitude = coords.Value.Latitude;
                    longitude = coords.Value.Longitude;
                }
                else
                {
                    addressToSave = Tools.InvalidAddressMarker;
                    badAddress = true;
                }
            }
            else
            {
                addressToSave = Tools.InvalidAddressMarker;
                badAddress = true;
            }

            // Ensure StartDate is valid (avoid DateTime.MinValue)
            DateTime startDate = order.OrderDate == default ? AdminManager.Now : order.OrderDate;

            // Map BO.Order to DO.Order before persisting to data layer
            var doOrder = new DO.Order
            {
                Id = order.Id,
                Type = (DO.OrderType)order.Type,
                CustomerName = order.CustomerName,
                CustomerAddress = addressToSave,
                CustomerPhone = order.CustomerPhone,
                OrderDate = startDate,
                size = order.Volume,
                weight = order.Weight,
                // Use geocoded coordinates
                Latitude = latitude,
                Longitude = longitude,
                Description = order.OrderDescription,
                // Convert nullable fragility level to DO layer
                Fragility = order.Fragility.HasValue ? (DO.FragilityLevel)order.Fragility.Value : null
            };
            
            // Persist the new order to the data layer
            lock (AdminManager.BlMutex)
            {
                s_dal.Order.Create(doOrder);
            }
            
            // Notify subscribers that the order list has been updated
            Observers.NotifyListUpdated();

            if (badAddress)
                throw new BLBadAddressException("Customer address is invalid. Order saved with INVALID_ADDRESS.");
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
    internal static async Task FinishOrderAsync(int requesterId, int courierId, int deliveryId, BO.DeliveredStatus deliveredStatus)
    {
        try
        {
            // Validate requester exists in the system
            DO.Courier? requester;
            lock (AdminManager.BlMutex)
            {
                requester = s_dal.Courier.Read(requesterId);
            }
            
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Validate courier, delivery, and associated order exist
            DO.Courier? courier;
            DO.Delivery? delivery;
            DO.Order? order;
            lock (AdminManager.BlMutex)
            {
                courier = s_dal.Courier.Read(courierId);
                delivery = s_dal.Delivery.Read(deliveryId);
                if (delivery != null)
                {
                    order = s_dal.Order.Read(delivery.OrderId);
                }
                else
                {
                    order = null;
                }
            }
            
            if (courier == null)
                throw new BLNotFoundException($"Courier {courierId} not found.");
            if (delivery == null)
                throw new BLNotFoundException($"Delivery {deliveryId} not found.");
            if (order == null)
                throw new BLNotFoundException($"Order {delivery.OrderId} not found.");

            var config = AdminManager.GetConfig();

            // Compute coordinates and distance for the delivery
            bool badAddress = false;
            double lat = order.Latitude ?? 0;
            double lon = order.Longitude ?? 0;
            if (lat == 0 && lon == 0)
            {
                var coords = await Tools.TryGetCoordinatesFromAddressAsync(order.CustomerAddress).ConfigureAwait(false);
                if (coords.HasValue)
                {
                    lat = coords.Value.Latitude;
                    lon = coords.Value.Longitude;
                }
                else
                {
                    badAddress = true;
                }
            }

            double? distance = null;
            if (lat != 0 || lon != 0)
            {
                distance = await Tools.CalculateRouteDistanceCachedAsync(
                    config.CompanyLatitude, config.CompanyLongitude, lat, lon).ConfigureAwait(false);
            }

            // Update the delivery record with completion information
            var updated = delivery with
            {
                // Record the current time as the arrival time
                ArrivalTime = AdminManager.Now,
                // Mark status as delivered
                DeliveredStatus = (DO.DeliveredStatus)deliveredStatus,
                Distance = distance,
                PickupTime = delivery.PickupTime,
            };

            lock (AdminManager.BlMutex)
            {
                s_dal.Delivery.Update(updated);
            }
            
            CourierManager.InvalidateDeliveryCache();

            // Notify subscribers that this order has been completed
            Observers.NotifyItemUpdated(delivery.OrderId);
            Observers.NotifyListUpdated();
            Observers.NotifyItemUpdated(courierId);

            CourierManager.Observers.NotifyItemUpdated(courierId);
            CourierManager.Observers.NotifyListUpdated();

            if (badAddress)
                throw new BLBadAddressException("Customer address is invalid. Delivery saved without distance.");
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
    /// Now checks if order already has an active delivery before assignment.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this assignment (must exist in the system).</param>
    /// <param name="orderId">ID of the order to be assigned.</param>
    /// <param name="courierId">ID of the courier to receive the assignment.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester, courier, or order does not exist.</exception>
    /// <exception cref="BO.BLInvalidOperationException">Thrown if the courier is inactive, holds a Director role, or order already has an active delivery.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static async Task AssignOrderToCourierAsync(int requesterId, int orderId, int courierId)
    {
        try
        {
            // Validate requester exists in the system
            DO.Courier? requester;
            lock (AdminManager.BlMutex)
            {
                requester = s_dal.Courier.Read(requesterId);
            }
            
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Validate courier and order exist
            DO.Courier? courier;
            DO.Order? order;
            lock (AdminManager.BlMutex)
            {
                courier = s_dal.Courier.Read(courierId);
                order = s_dal.Order.Read(orderId);
            }
            
            if (courier == null)
                throw new BLNotFoundException($"Courier {courierId} not found.");
            if (order == null)
                throw new BLNotFoundException($"Order {orderId} not found.");

            // Verify courier is active before assignment
            if (!courier.IsActive)
                throw new BO.BLInvalidOperationException($"Cannot assign order {orderId} to courier {courierId}: courier is not active.");

            // Verify courier is not a Director (delivery personnel must be Couriers, not Directors)
            if (courier.Administrator == DO.Administrator.Director)
                throw new BO.BLInvalidOperationException($"Cannot assign order {orderId} to courier {courierId}: courier is a Director.");

            // Check if order already has an active delivery
            List<DO.Delivery> existingActiveDeliveries;
            lock (AdminManager.BlMutex)
            {
                existingActiveDeliveries = s_dal.Delivery.ReadAll(d => 
                    d.OrderId == orderId && 
                    d.ArrivalTime == null && 
                    d.DeliveredStatus == null).ToList();
            }

            if (existingActiveDeliveries.Count > 0)
            {
                var activeDelivery = existingActiveDeliveries.First();
                DO.Courier? assignedCourier;
                lock (AdminManager.BlMutex)
                {
                    assignedCourier = s_dal.Courier.Read(activeDelivery.CourierId);
                }
                
                throw new BO.BLInvalidOperationException(
                    $"Cannot assign order {orderId} to courier {courierId}: " +
                    $"order is already assigned to courier {activeDelivery.CourierId} ({assignedCourier?.Name ?? "Unknown"}) " +
                    $"since {activeDelivery.PickupTime:yyyy-MM-dd HH:mm}.");
            }

            // ADDITIONAL CHECK: Verify order is actually available for assignment
            List<DO.Delivery> orderDeliveries;
            lock (AdminManager.BlMutex)
            {
                orderDeliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            }
            
            var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);

            if (orderStatus == BO.OrderStatus.Delivered || 
                orderStatus == BO.OrderStatus.Returned || 
                orderStatus == BO.OrderStatus.Canceled)
            {
                throw new BO.BLInvalidOperationException(
                    $"Cannot assign order {orderId} to courier {courierId}: " +
                    $"order is already {orderStatus.ToString().ToLower()}.");
            }

            double? distance = null;
            try
            {
                var coord = await Tools.TryGetCoordinatesFromAddressAsync(order.CustomerAddress).ConfigureAwait(false);
                if (coord.HasValue)
                {
                    distance = await Tools.CalculateRouteDistanceCachedAsync(
                        AdminManager.GetConfig().CompanyLatitude,
                        AdminManager.GetConfig().CompanyLongitude,
                        coord.Value.Latitude,
                        coord.Value.Longitude).ConfigureAwait(false);
                }
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
            lock (AdminManager.BlMutex)
            {
                s_dal.Delivery.Create(delivery);
            }
            
            CourierManager.InvalidateDeliveryCache();
            
            // Notify subscribers that the order has been assigned
            Observers.NotifyItemUpdated(orderId);
            Observers.NotifyListUpdated();

            // Notify COURIER subscribers
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
    /// Builds a live OrderInProgress snapshot for a courier/order pair.
    /// Used by UI fallback flows to display accurate timing fields from BL.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this information (must exist in the system).</param>
    /// <param name="courierId">ID of the courier who is assigned to the order.</param>
    /// <param name="orderId">ID of the order to build the snapshot for.</param>
    /// <returns>OrderInProgress snapshot populated with timing and schedule data.</returns>
    /// <exception cref="BO.BLNotFoundException">Thrown if requester, courier, or order does not exist.</exception>
    /// <exception cref="BO.BLInvalidOperationException">Thrown if no active delivery exists for the courier/order.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static async Task<BO.OrderInProgress> GetOrderInProgressSnapshotAsync(int requesterId, int courierId, int orderId)
    {
        try
        {
            DO.Courier? requester;
            lock(AdminManager.BlMutex)
            {
                requester = s_dal.Courier.Read(requesterId);

            }
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            DO.Courier? courier;
            DO.Order? orderDO;
            lock (AdminManager.BlMutex)
            {
                courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");
                orderDO = s_dal.Order.Read(orderId)
                ?? throw new BLNotFoundException($"Order {orderId} not found.");
            }


            List<Delivery>? deliveries;
            lock (AdminManager.BlMutex)
            {
                deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            }

            var currentDelivery = deliveries.FirstOrDefault(d => d.CourierId == courierId && d.ArrivalTime == null);
            if (currentDelivery == null)
                throw new BO.BLInvalidOperationException($"No active delivery for courier {courierId} on order {orderId}.");

            var config = AdminManager.GetConfig();
            var ordStatus = await Tools.CalculateOrderStatusAsync(deliveries).ConfigureAwait(false);

            double? distance = currentDelivery.Distance;
            if (!distance.HasValue)
            {
                try
                {
                    var coord = await Tools.TryGetCoordinatesFromAddressAsync(orderDO.CustomerAddress).ConfigureAwait(false);
                    if (coord.HasValue)
                    {
                        distance = await Tools.CalculateRouteDistanceCachedAsync(
                            config.CompanyLatitude, config.CompanyLongitude,
                            coord.Value.Latitude, coord.Value.Longitude
                        ).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Leave distance null if calculation fails; ETA will use fallback.
                }
            }

            DateTime estimatedArrival = distance.HasValue
                ? await Tools.EstimateArrivalAsync(currentDelivery.PickupTime, currentDelivery.Transport, distance.Value)
                    .ConfigureAwait(false)
                : await Tools.EstimateArrivalFallbackAsync(currentDelivery.PickupTime).ConfigureAwait(false);

            var scheduleStatus = await Tools.CalculateScheduleStatusAsync(
                ordStatus,
                orderDO.OrderDate,
                distance.HasValue
                    ? await Tools.CalculateEstimatedArrivalAsync(
                        orderDO.OrderDate,
                        distance.Value,
                        await Tools.GetSpeedAsync(currentDelivery.Transport, config).ConfigureAwait(false)
                      ).ConfigureAwait(false)
                    : null,
                orderDO.OrderDate.Add(config.MaxDeliveryTime),
                currentDelivery.ArrivalTime).ConfigureAwait(false);

            return new BO.OrderInProgress
            {
                DeliveryId = currentDelivery.Id,
                OrderId = orderDO.Id,
                OrderType = (BO.OrderType)orderDO.Type,
                CustomerName = orderDO.CustomerName,
                CustomerAddress = orderDO.CustomerAddress,
                CustomerPhone = orderDO.CustomerPhone,
                PickupTime = currentDelivery.PickupTime,
                Distance = distance,
                ArrivalTime = currentDelivery.ArrivalTime,
                OrderStatus = ordStatus,
                OrderDate = orderDO.OrderDate,
                RemaningTime = estimatedArrival - AdminManager.Now,
                Description = orderDO.Description,
                ScheduleStatus = scheduleStatus,
                EstimatedArrivalTime = estimatedArrival,
                MaxDeliveryTime = orderDO.OrderDate.Add(config.MaxDeliveryTime),
            };
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
            // Validate requester/courier exists
            DO.Courier? requester;
            DO.Courier? courier;
            lock (AdminManager.BlMutex)
            {
                requester = s_dal.Courier.Read(requesterId) ?? throw new BO.BLNotFoundException("Requester does not exist.");
                courier = s_dal.Courier.Read(courierId) ?? throw new BO.BLNotFoundException($"Courier {courierId} not found.");
            }

            // Authorization (reasonable per "main management" + courier screen):
            // allow the courier himself or the main boss/admin
            var config = AdminManager.GetConfig();
            if (requesterId != courierId && requesterId != config.BossId)
                throw new BO.BLInvalidOperationException("Requester is not authorized to view this courier history.");

            // "Closed deliveries" = deliveries with end-time AND end-type (DeliveredStatus != null)
            List<DO.Delivery>? deliveries;
            Dictionary<int, DO.Order> orders;
            lock (AdminManager.BlMutex)
            {
                deliveries = s_dal.Delivery
                .ReadAll(d => d.CourierId == courierId && d.ArrivalTime != null && d.DeliveredStatus != null)
                .ToList();
                orders = s_dal.Order.ReadAll().ToDictionary(o => o.Id);
            }

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

    private static async Task<BO.OpenOrderInList?> BuildOpenOrderInListAsync(
        DO.Order o,
        List<DO.Delivery> deliveriesAll,
        BO.Courier courier,
        BO.Config config,
        DateTime now,
        double companyLat,
        double companyLon)
    {
        var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();
        var orderStatus = await Tools.CalculateOrderStatusAsync(orderDeliveries).ConfigureAwait(false);

        if (orderStatus == BO.OrderStatus.Delivered ||
            orderStatus == BO.OrderStatus.Returned ||
            orderStatus == BO.OrderStatus.Canceled)
            return null;

        var openDeliveries = orderDeliveries.Where(d => d.ArrivalTime == null).ToList();
        if (openDeliveries.Any(d => d.CourierId != 0))
            return null;

        double custLat = o.Latitude ?? 0;
        double custLon = o.Longitude ?? 0;

        double birdDistance = await Tools.BirdDistanceAsync(
                companyLat, companyLon,
                custLat, custLon)
            .ConfigureAwait(false);

        if (custLat == 0 && custLon == 0 && !string.IsNullOrWhiteSpace(o.CustomerAddress))
        {
            var coords = await Tools.TryGetCoordinatesFromAddressAsync(o.CustomerAddress).ConfigureAwait(false);
            if (!coords.HasValue)
                return null;

            custLat = coords.Value.Latitude;
            custLon = coords.Value.Longitude;
            birdDistance = await Tools.BirdDistanceAsync(companyLat, companyLon, custLat, custLon)
                .ConfigureAwait(false);
        }

        if (custLat == 0 && custLon == 0)
            return null;

        double distance = await Tools.CalculateRouteDistanceCachedAsync(companyLat, companyLon, custLat, custLon)
            .ConfigureAwait(false);

        TimeSpan? addedTime = now - o.OrderDate;

        DateTime? estArrival = await Tools.EstimateArrivalAsync(now, (DO.DeliveryTransport)courier.Transport, distance).ConfigureAwait(false);
        TimeSpan estSpan = estArrival.HasValue ? (estArrival.Value - now) : TimeSpan.Zero;

        DateTime maxDeliveredTime = o.OrderDate + config.MaxDeliveryTime;

        var scheduleStatus = await Tools.CalculateScheduleStatusAsync(
            orderStatus,
            o.OrderDate,
            estArrival,
            maxDeliveredTime,
            null).ConfigureAwait(false);

        return new BO.OpenOrderInList
        {
            CourierId = null,
            OrderId = o.Id,
            OrderType = (BO.OrderType)o.Type,
            Fragility = o.Fragility != null
                ? (BO.FragilityLevel?)(BO.FragilityLevel)o.Fragility.Value
                : null,
            CustomerAddress = o.CustomerAddress ?? string.Empty,
            BirdDistance = birdDistance,
            Distance = distance,
            AddedTime = addedTime,
            ScheduleStatus = scheduleStatus,
            EstimatedDeliveryTime = estSpan,
            MaxDeliveredTime = maxDeliveredTime
        };
    }

    internal static async Task<IEnumerable<BO.OpenOrderInList>> GetOpenOrdersForCourierAsync(
        int requesterId,
        int courierId,
        Enum? filter,
        Enum? sorter)
    {
        try
        {
            // Validate requester exists
            DO.Courier? courier;
            List<DO.Order>? orders;
            List<DO.Delivery>? deliveriesAll;
            lock (AdminManager.BlMutex)
            {
                _ = s_dal.Courier.Read(requesterId)
                ?? throw new BLNotFoundException("Requester does not exist.");
                courier = s_dal.Courier.Read(courierId)
                ?? throw new BLNotFoundException($"Courier {courierId} not found.");
                orders = s_dal.Order.ReadAll().ToList();
                deliveriesAll = s_dal.Delivery.ReadAll().ToList();
            }

            var config = AdminManager.GetConfig();
            DateTime now = config.Clock;

            // Company coordinates come from admin/config (as you said)
            double companyLat = config.CompanyLatitude;
            double companyLon = config.CompanyLongitude;

            IEnumerable<Task<BO.OpenOrderInList?>> tasks = orders.Select(o =>
                BuildOpenOrderInListAsync(o, deliveriesAll, 
                    new BO.Courier
                    {
                        Id = courier.Id,
                        Name = courier.Name,
                        Phone = courier.Phone,
                        Email = courier.Email,
                        Password = courier.Password,
                        IsActive = courier.IsActive,
                        Transport = (BO.DeliveryTransport)courier.Transport,
                        StartDate = courier.StartDate,
                        Administrator = (BO.Administrator)courier.Administrator,
                        MaxDistance = courier.MaxDistance
                    },
                    config, now, companyLat, companyLon));

            var result = new List<BO.OpenOrderInList>();
            foreach (var task in tasks)
            {
                var item = await task.ConfigureAwait(false);
                if (item != null)
                    result.Add(item);
            }

            if (courier.MaxDistance != null)
                result = result.Where(x => x.Distance <= courier.MaxDistance.Value).ToList();

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
