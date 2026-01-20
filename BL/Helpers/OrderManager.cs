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
    /// Non-blocking mutex to prevent concurrent execution of PeriodicOrdersUpdates.
    /// Ensures that only one instance of the periodic update process runs at a time.
    /// </summary>
    private static readonly AsyncMutex s_periodicMutex = new();

    /// <summary>
    /// Non-blocking mutex for simulation updates to prevent overlapping simulation runs (Stage 7).
    /// Ensures that only one instance of the simulation process runs at a time.
    /// </summary>
    private static readonly AsyncMutex s_simulationMutex = new();

    /// <summary>
    /// Random number generator for simulation probabilistic decisions (Stage 7).
    /// Used to generate orders and make stochastic delivery decisions.
    /// </summary>
    private static readonly Random s_rand = new();

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
    /// Stage 7: Uses non-blocking mutex to prevent overlapping runs. All DAL operations are wrapped with
    /// lock(AdminManager.BlMutex), while observer notifications are performed outside locks.
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
        // Non-blocking mutex: if already running, skip immediately
        if (s_periodicMutex.CheckAndSetInProgress())
            return;

        try
        {
            // Read config values under lock
            TimeSpan maxDeliveryTime;
            lock (AdminManager.BlMutex)
            {
                maxDeliveryTime = s_dal.Config.MaxTimeDelivery;
            }

            // Snapshot reads to minimize DAL calls - materialize to avoid deferred LINQ under lock
            List<DO.Order> ordersAll;
            List<DO.Delivery> deliveriesAll;
            lock (AdminManager.BlMutex)
            {
                ordersAll = s_dal.Order.ReadAll().ToList();
                deliveriesAll = s_dal.Delivery.ReadAll().ToList();
            }

            // Local collections to track updated entities for notification outside locks
            var updatedOrders = new HashSet<int>();
            var updatedCouriers = new HashSet<int>();
            bool deliveriesUpdated = false;

            // Update all OPEN orders whose validity expired after advancing the system clock
            foreach (var o in ordersAll)
            {
                // If the order already exceeded max time at newClock -> it is expired
                if (newClock - o.OrderDate <= maxDeliveryTime)
                    continue;

                // Find all deliveries of this order
                var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();

                // "Open" delivery = a delivery that hasn't ended yet => DeliveredStatus is null
                // (DeliveredStatus is the delivery end-type; null means still not ended)
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

                    // Update delivery under lock
                    lock (AdminManager.BlMutex)
                    {
                        try
                        {
                            s_dal.Delivery.Update(upd);
                            deliveriesUpdated = true;
                            updatedOrders.Add(d.OrderId);
                            if (d.CourierId != 0)
                                updatedCouriers.Add(d.CourierId);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error updating delivery {d.Id}: {ex.Message}");
                        }
                    }
                }
            }

            // Notify observers OUTSIDE all locks
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
        finally
        {
            // Always release the mutex, even on exception
            s_periodicMutex.UnsetInProgress();
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

            var doOrders = s_dal.Order.ReadAll().ToList();
            var deliveriesDO = s_dal.Delivery.ReadAll().ToList();
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

            var doOrders = s_dal.Order.ReadAll().ToList();
            var deliveriesDO = s_dal.Delivery.ReadAll().ToList();
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
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Retrieve the order data object
            var doOrder = s_dal.Order.Read(orderId) ?? throw new BLNotFoundException($"Order with id {orderId} not found.");

            // Get all deliveries associated with this order
            var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
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
    internal static async Task UpdateOrderDetailsAsync(int requesterId, BO.Order order)
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
            s_dal.Order.Update(doOrder);
            
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
                CourierManager.InvalidateDeliveryCache();
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
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            if(s_dal.Order.Read(orderId) == null)
                throw new BLNotFoundException($"Order with id {orderId} does not exist.");

            // Get all deliveries associated with this order
            var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            var impactedCouriers = deliveries
                .Where(d => d.CourierId != 0)
                .Select(d => d.CourierId)
                .Distinct()
                .ToList();
            
            // Delete each delivery first (cascade delete)
            foreach (var d in deliveries)
                s_dal.Delivery.Delete(d.Id);

            // Then delete the order itself
            s_dal.Order.Delete(orderId);
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
            var requester = s_dal.Courier.Read(requesterId);
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
            s_dal.Order.Create(doOrder);
            
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
    internal static async Task FinishOrderAsync(int requesterId, int courierId, int deliveryId,BO.DeliveredStatus deliveredStatus)
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

            s_dal.Delivery.Update(updated);
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
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this assignment (must exist in the system).</param>
    /// <param name="orderId">ID of the order to be assigned.</param>
    /// <param name="courierId">ID of the courier to receive the assignment.</param>
    /// <exception cref = "BO.BLNotFoundException" > Thrown if the requester, courier, or order does not exist.</exception>
    /// <exception cref="BO.BLInvalidOperationException">Thrown if the courier is inactive, holds a Director role, or order already has an active delivery.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static async Task AssignOrderToCourierAsync(int requesterId, int orderId, int courierId)
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

            // FIXED: Check if order already has an active delivery
            var existingActiveDeliveries = s_dal.Delivery.ReadAll(d => 
                d.OrderId == orderId && 
                d.ArrivalTime == null && 
                d.DeliveredStatus == null).ToList();

            if (existingActiveDeliveries.Count > 0)
            {
                var activeDelivery = existingActiveDeliveries.First();
                var assignedCourier = s_dal.Courier.Read(activeDelivery.CourierId);
                throw new BO.BLInvalidOperationException(
                    $"Cannot assign order {orderId} to courier {courierId}: " +
                    $"order is already assigned to courier {activeDelivery.CourierId} ({assignedCourier?.Name ?? "Unknown"}) " +
                    $"since {activeDelivery.PickupTime:yyyy-MM-dd HH:mm}.");
            }

            // ADDITIONAL CHECK: Verify order is actually available for assignment
            var orderDeliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
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
                DeliveredStatus = null
            };

            // Persist the new delivery to the data layer
            s_dal.Delivery.Create(delivery);
            CourierManager.InvalidateDeliveryCache();
            
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
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            var courier = s_dal.Courier.Read(courierId)
                ?? throw new BLNotFoundException($"Courier {courierId} not found.");
            var orderDO = s_dal.Order.Read(orderId)
                ?? throw new BLNotFoundException($"Order {orderId} not found.");

            var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
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
    /// Simulates dynamic order activity by generating new orders and auto-assigning them to available couriers.
    /// Executes asynchronously once per second from the simulator thread (clockRunner).
    /// 
    /// Simulation behavior:
    /// 1. Probabilistically creates new orders from a pool of sample customer data (~8% per second)
    /// 2. For each pending order without an active delivery, probabilistically auto-assigns it to an available courier (~30% per second)
    /// 
    /// Stage 7: Uses non-blocking AsyncMutex to prevent overlapping simulation runs.
    /// All DAL access is protected with short lock blocks (lock(AdminManager.BlMutex)).
    /// Observer notifications are performed only outside locks to prevent blocking.
    /// </summary>
    /// <remarks>
    /// Simulation parameters:
    /// - NEW_ORDER_PROBABILITY: 8% chance per second that a new order is created (~30 orders per hour average)
    /// - AUTO_ASSIGN_PROBABILITY: 30% chance per second that a pending order gets auto-assigned to an available courier
    /// 
    /// This creates realistic dynamics where orders appear and get distributed to couriers over time.
    /// All async operations (geocoding, distance calculation) occur outside locks.
    /// </remarks>
    internal static async Task SimulateOrderActivityAsync() // stage 7
    {
        // Non-blocking mutex: if previous simulation is still in progress, exit immediately
        if (s_simulationMutex.CheckAndSetInProgress())
            return;

        try
        {
            const double NEW_ORDER_PROBABILITY = 0.08;      // 8% chance per second
            const double AUTO_ASSIGN_PROBABILITY = 0.30;    // 30% chance to auto-assign

            var config = AdminManager.GetConfig();
            var now = AdminManager.Now;

            bool orderCreated = false;
            bool ordersModified = false;
            var updatedOrderIds = new HashSet<int>();
            var updatedCourierIds = new HashSet<int>();

            // Step 1: Probabilistically create a new order
            if (s_rand.NextDouble() < NEW_ORDER_PROBABILITY)
            {
                // Sample customer data for generated orders
                string[] customerNames = 
                { 
                    "Alice Johnson", "Bob Smith", "Charlie Brown", "Diana Prince", 
                    "Eve Davis", "Frank Miller", "Grace Lee", "Henry Wilson" 
                };
                string[] addresses = 
                { 
                    "123 Main St, Tel Aviv",
                    "456 King David Ave, Jerusalem",
                    "789 Dizengoff St, Tel Aviv",
                    "101 Jaffa Rd, Jerusalem",
                    "202 Ben Yehuda St, Tel Aviv",
                    "303 Rothschild Blvd, Tel Aviv",
                    "404 Herzl St, Jerusalem"
                };
                string[] phones = 
                { 
                    "050-1234567", "051-2345678", "052-3456789", "053-4567890", 
                    "054-5678901", "055-6789012", "056-7890123"
                };

                int nameIdx = s_rand.Next(customerNames.Length);
                int addrIdx = s_rand.Next(addresses.Length);
                int phoneIdx = s_rand.Next(phones.Length);

                var newOrder = new BO.Order
                {
                    Id = 0, // DAL will generate
                    Type = (BO.OrderType)(s_rand.Next(0, Enum.GetValues(typeof(BO.OrderType)).Length - 1)),
                    CustomerName = customerNames[nameIdx],
                    CustomerAddress = addresses[addrIdx],
                    CustomerPhone = phones[phoneIdx],
                    OrderDate = now,
                    Volume = Math.Round(s_rand.NextDouble() * 50 + 1, 2),
                    Weight = Math.Round(s_rand.NextDouble() * 100 + 1, 2),
                    Fragility = (BO.FragilityLevel?)(s_rand.NextDouble() < 0.3 
                        ? (BO.FragilityLevel)s_rand.Next(0, Enum.GetValues(typeof(BO.FragilityLevel)).Length - 1) 
                        : null),
                    OrderDescription = "Simulated order"
                };

                try
                {
                    // Create the order (async geocoding happens outside locks)
                    await OrderManager.AddOrderAsync(config.BossId, newOrder).ConfigureAwait(false);
                    orderCreated = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Simulation: Failed to create order: {ex.Message}");
                }
            }

            // Step 2: Fetch all pending orders and deliveries (materialized under lock)
            List<DO.Order> pendingOrders;
            List<DO.Delivery> allDeliveries;
            lock (AdminManager.BlMutex)
            {
                var allOrders = s_dal.Order.ReadAll().ToList();
                allDeliveries = s_dal.Delivery.ReadAll().ToList();

                // Filter to orders that have no active or completed deliveries
                pendingOrders = allOrders
                    .Where(o =>
                    {
                        var orderDeliveries = allDeliveries.Where(d => d.OrderId == o.Id).ToList();
                        
                        // No deliveries yet = pending
                        if (orderDeliveries.Count == 0)
                            return true;

                        // If any delivery is open (no ArrivalTime), it's already being processed
                        if (orderDeliveries.Any(d => d.ArrivalTime == null))
                            return false;

                        // If all deliveries are closed and all are successful terminal states, skip
                        return !orderDeliveries.All(d =>
                            d.DeliveredStatus == DO.DeliveredStatus.Delivered ||
                            d.DeliveredStatus == DO.DeliveredStatus.Rejected ||
                            d.DeliveredStatus == DO.DeliveredStatus.Canceled);
                    })
                    .ToList();
            }

            // Step 3: For each pending order, probabilistically auto-assign to an available courier
            if (pendingOrders.Count > 0)
            {
                // Get active couriers (non-Director) under lock
                List<DO.Courier> activeCouriers;
                lock (AdminManager.BlMutex)
                {
                    activeCouriers = s_dal.Courier.ReadAll()
                        .Where(c => c.IsActive && c.Administrator != DO.Administrator.Director)
                        .ToList();
                }

                if (activeCouriers.Count > 0)
                {
                    foreach (var order in pendingOrders)
                    {
                        // Probabilistic decision to attempt assignment
                        if (s_rand.NextDouble() >= AUTO_ASSIGN_PROBABILITY)
                            continue;

                        // Check if this order already has an open delivery (under lock)
                        bool hasOpenDelivery;
                        lock (AdminManager.BlMutex)
                        {
                            hasOpenDelivery = allDeliveries
                                .Any(d => d.OrderId == order.Id && d.ArrivalTime == null);
                        }

                        if (hasOpenDelivery)
                            continue; // Already assigned

                        // Select a random courier
                        var selectedCourier = activeCouriers[s_rand.Next(activeCouriers.Count)];

                        try
                        {
                            // Attempt to assign using the existing BL method (async, outside locks)
                            await OrderManager.AssignOrderToCourierAsync(
                                config.BossId,
                                order.Id,
                                selectedCourier.Id
                            ).ConfigureAwait(false);

                            ordersModified = true;
                            updatedOrderIds.Add(order.Id);
                            updatedCourierIds.Add(selectedCourier.Id);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"Simulation: Failed to assign order {order.Id} to courier {selectedCourier.Id}: {ex.Message}");
                        }
                    }
                }
            }

            // Step 4: Trigger notifications OUTSIDE all locks
            if (orderCreated)
            {
                Observers.NotifyListUpdated();
            }

            if (ordersModified)
            {
                foreach (var orderId in updatedOrderIds)
                    Observers.NotifyItemUpdated(orderId);

                Observers.NotifyListUpdated();

                foreach (var courierId in updatedCourierIds)
                    CourierManager.Observers.NotifyItemUpdated(courierId);

                if (updatedCourierIds.Count > 0)
                    CourierManager.Observers.NotifyListUpdated();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SimulateOrderActivity failed: {ex.Message}");
        }
        finally
        {
            // Always release the mutex, even on exception
            s_simulationMutex.UnsetInProgress();
        }
    }
}
