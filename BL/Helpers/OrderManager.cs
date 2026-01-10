using BO;
using DalApi;
using DO;
using System;
using System.Linq;
using System.Collections.Generic;

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
            // Read config values once to minimize DAL calls and improve performance
            TimeSpan maxDeliveryTime = s_dal.Config.MaxTimeDelivery;
            TimeSpan riskRange = s_dal.Config.RiskRange;

            // Read all deliveries once to avoid multiple DAL calls in the loop
            var deliveriesAll = s_dal.Delivery.ReadAll().ToList();
            bool deliveriesUpdated = false;
            var updatedOrders = new HashSet<int>(); // Track which orders were affected by updates

            // Iterate over a snapshot of all orders to evaluate status changes
            foreach (var o in s_dal.Order.ReadAll().ToList())
            {
                // Get all deliveries associated with this order
                var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();

                // 1) Start deliveries whose pickup time was reached during this period (null Status → Processing)
                // Transition: Status == null (pending) AND PickupTime has just passed
                foreach (var d in orderDeliveries.Where(d => d.Status == null && d.PickupTime > oldClock && d.PickupTime <= newClock))
                {
                    var upd = d with { Status = DO.OrderStatus.Processing };
                    s_dal.Delivery.Update(upd);
                    deliveriesUpdated = true;
                    updatedOrders.Add(d.OrderId); // Track the affected order for notification
                }

                // 2) Cancel processing deliveries that exceed maxDeliveryTime (best-effort cancellation)
                // Use order.OrderDate as the reference point for the allowed delivery window
                foreach (var d in orderDeliveries.Where(d => d.Status == DO.OrderStatus.Processing && d.ArrivalTime == null))
                {
                    // Check if the delivery has exceeded the maximum allowed delivery time
                    if (newClock - o.OrderDate > maxDeliveryTime)
                    {
                        var upd = d with
                        {
                            Status = DO.OrderStatus.Canceled,
                            ArrivalTime = newClock // Mark as finished to prevent further processing
                        };
                        s_dal.Delivery.Update(upd);
                        deliveriesUpdated = true;
                        updatedOrders.Add(d.OrderId); // Track the affected order for notification
                    }
                }

                // 3) Compute in-memory order status (BO layer) using delivery records + time-based escalation logic
                // This determines what status the order should have based on both delivery progress and elapsed time
                var deliveryBasedStatus = Tools.CalculateOrderStatus(orderDeliveries); // Calculated from delivery records
                TimeSpan elapsed = newClock - o.OrderDate;
                BO.OrderStatus timeBasedStatus = BO.OrderStatus.Pending;

                // Apply time-based escalation rules
                if (elapsed > maxDeliveryTime)
                    // Delivery time exceeded: mark as canceled regardless of delivery progress
                    timeBasedStatus = BO.OrderStatus.Canceled;
                else if (elapsed >= (maxDeliveryTime - riskRange))
                    // Within risk range (near deadline): escalate to delivery status if pending, otherwise keep delivery status
                    timeBasedStatus = deliveryBasedStatus == BO.OrderStatus.Pending ? BO.OrderStatus.Processing : deliveryBasedStatus;
                else
                    // Still within safe time window: keep as pending
                    timeBasedStatus = BO.OrderStatus.Pending;

                // Determine final status: prioritize terminal delivery states (Delivered, Returned, Canceled)
                BO.OrderStatus finalStatus;
                if (deliveryBasedStatus == BO.OrderStatus.Delivered
                    || deliveryBasedStatus == BO.OrderStatus.Returned
                    || deliveryBasedStatus == BO.OrderStatus.Canceled)
                {
                    // If delivery has reached a terminal state, use that status
                    finalStatus = deliveryBasedStatus;
                }
                else
                {
                    // Otherwise use time-based escalation status
                    finalStatus = timeBasedStatus;
                }

                // 4) No persistence to DO.Order (by design) - only delivery records are updated
                // The order status is computed in-memory and will be recalculated on next retrieval
            }

            // Notify observers if any deliveries were updated during this period
            if (deliveriesUpdated)
            {
                // Notify each affected order individually for granular UI updates
                foreach (var orderId in updatedOrders)
                {
                    Observers.NotifyItemUpdated(orderId);
                }
                // Also notify that the list has been updated for comprehensive refresh
                Observers.NotifyListUpdated();
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
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId) ?? throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            // Read data once to minimize DAL calls
            var orders = s_dal.Order.ReadAll().ToList();
            var deliveriesAll = s_dal.Delivery.ReadAll().ToList();
            var config = AdminManager.GetConfig();

            // Calculate array dimensions based on enum sizes
            int statusCount = Enum.GetValues(typeof(BO.OrderStatus)).Length;
            int scheduleCount = Enum.GetValues(typeof(BO.ScheduleStatus)).Length;
            int[] summary = new int[statusCount * scheduleCount];

            // Project orders to their status combinations
            var projections = orders.Select(o =>
            {
                // Get all deliveries for this order
                var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();
                var lastDelivery = orderDeliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();
                DateTime? realArrival = lastDelivery?.ArrivalTime;

                // Retrieve or geocode coordinates
                double lat = o.Latitude ?? 0;
                double lon = o.Longitude ?? 0;
                if (lat == 0 && lon == 0)
                {
                    try
                    {
                        // Attempt to geocode the customer address if coordinates are missing
                        var coords = Tools.GetCoordinatesFromAddressAsync(o.CustomerAddress).GetAwaiter().GetResult();
                        lat = coords.Latitude;
                        lon = coords.Longitude;
                    }
                    catch { }
                }

                // Calculate straight-line distance from company to customer
                double distance = Tools.BirdDistance(
                    config.CompanyLatitude,
                    config.CompanyLongitude,
                    lat,
                    lon
                );

                // Choose speed based on the last delivery's transport method, or default to car speed
                double speed = config.CarSpeed;
                if (lastDelivery != null)
                    speed = Tools.GetSpeed(lastDelivery.Transport, config);

                // Calculate estimated arrival time based on distance and speed
                DateTime? estArrival = distance > 0
                    ? Tools.CalculateEstimatedArrival(o.OrderDate, distance, speed)
                    : null;

                // Add risk range to estimated arrival to get maximum acceptable arrival time
                DateTime? maxArrival = estArrival?.Add(config.RiskRange);

                // Calculate order status from delivery records
                var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);
                
                // Determine schedule status (OnTime, InRisk, Late, Unknown)
                var scheduleStatus = Tools.CalculateScheduleStatus(orderStatus, o.OrderDate, estArrival, maxArrival, realArrival);

                return new { Status = orderStatus, Schedule = scheduleStatus };
            });

            // Group projections by status combination and count occurrences
            var groups = projections.GroupBy(p => new { p.Status, p.Schedule });

            foreach (var g in groups)
            {
                // Map status combinations to array indices
                int sIdx = (int)g.Key.Status;
                int schIdx = (int)g.Key.Schedule;
                int idx = sIdx * scheduleCount + schIdx;
                summary[idx] = g.Count();
            }

            return summary;
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
            // Validate the requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Read all data once to minimize DAL calls
            var doOrders = s_dal.Order.ReadAll().ToList();
            var deliveriesDO = s_dal.Delivery.ReadAll().ToList();
            var config = AdminManager.GetConfig();

            // Get current time for elapsed time calculations
            var now = AdminManager.Now;

            // Project each order to an OrderInList view model with computed metrics
            var list = doOrders.Select(order =>
            {
                // Get all deliveries for this order
                var orderDeliveries = deliveriesDO.Where(d => d.OrderId == order.Id).ToList();
                var lastDelivery = orderDeliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();
                int? deliveryId = lastDelivery?.Id;

                // Retrieve or geocode coordinates for distance calculation
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
                    catch (Exception ex)
                    {
                        throw new BLFailedOperation(ex.Message);
                    }
                }

                // Calculate straight-line distance from company to customer
                double distance = Tools.BirdDistance(
                    config.CompanyLatitude,
                    config.CompanyLongitude,
                    lat,
                    lon
                );

                // Choose speed based on the last delivery's transport method, or default to car speed
                double speed = config.CarSpeed;
                if (lastDelivery != null)
                    speed = Tools.GetSpeed(lastDelivery.Transport, config);

                // Calculate estimated arrival time based on distance and speed
                DateTime? estArrival = distance > 0
                    ? Tools.CalculateEstimatedArrival(order.OrderDate, distance, speed)
                    : null;

                // Add risk range to determine maximum acceptable arrival time
                DateTime? maxArrival = estArrival?.Add(config.RiskRange);
                DateTime? realArrival = lastDelivery?.ArrivalTime;

                // Calculate order status from delivery records
                var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);

                // Determine schedule status based on arrival estimates
                var schedule = Tools.CalculateScheduleStatus(
                    orderStatus,
                    order.OrderDate,
                    estArrival,
                    maxArrival,
                    realArrival
                );

                // Build the OrderInList view model with all calculated metrics
                return new BO.OrderInList
                {
                    DeliveryId = deliveryId,
                    OrderId = order.Id,
                    Type = (BO.OrderType)order.Type,
                    Distance = distance,
                    Status = orderStatus,
                    ScheduleStatus = schedule,
                    // Total time from order creation to actual arrival (or current time if not yet delivered)
                    OrderEndTime = realArrival != null ? realArrival.Value - order.OrderDate : now - order.OrderDate,
                    // Time from order creation to when treatment/pickup began
                    TreatmentEndTime = lastDelivery != null ? lastDelivery.PickupTime - order.OrderDate : TimeSpan.Zero,
                    // Count distinct couriers who have handled this order
                    NumberOfCouriers = orderDeliveries.Select(d => d.CourierId).Distinct().Count()
                };
            }).ToList();

            // Apply optional status or type filter
            if (filter != null)
            {
                if (filter is BO.OrderStatus os)
                    // Filter by order status (Pending, Processing, Delivered, etc.)
                    list = list.Where(l => l.Status == os).ToList();
                else if (filter is BO.OrderType ot)
                    // Filter by order type (FastFood, Pizza, etc.)
                    list = list.Where(l => l.Type == ot).ToList();
            }

            // Apply optional sorting with ascending/descending direction
            if (sorter != null)
            {
                string key = sorter.ToString() ?? string.Empty;
                bool ascending = Object is bool b ? b : true;

                // Sort by the specified field
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
    /// Retrieves a list of orders with optional filtering by order status and sorting parameters.
    /// Calculates delivery metrics including distance, estimated arrival, and schedule status for each order.
    /// Note: Currently does not apply status filter or sort parameters (parameters ignored).
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this list (must exist in the system).</param>
    /// <param name="statusFilter">Optional filter by order status (currently ignored in implementation).</param>
    /// <param name="sortParameter">Optional sort parameter (currently ignored in implementation).</param>
    /// <returns>An enumerable collection of OrderInList objects representing orders with calculated metrics.</returns>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static IEnumerable<BO.OrderInList> GetOrdersList(int requesterId, BO.OrderStatus? statusFilter, object? sortParameter)
    {
        try
        {
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Read configuration and all data once to minimize DAL calls
            var config = AdminManager.GetConfig();
            var ordersDO = s_dal.Order.ReadAll().ToList();
            var deliveriesDO = s_dal.Delivery.ReadAll().ToList();

            // Get current time for elapsed time calculations
            var now = AdminManager.Now;

            // Project each order to an OrderInList view model with computed metrics
            var list = ordersDO.Select(order =>
            {
                // Get all deliveries associated with this order
                var orderDeliveries = deliveriesDO
                    .Where(d => d.OrderId == order.Id)
                    .ToList();

                // Identify the most recent delivery (last by pickup time)
                var lastDelivery = orderDeliveries
                    .OrderByDescending(d => d.PickupTime)
                    .FirstOrDefault();

                int? deliveryId = lastDelivery?.Id;

                // Retrieve or geocode coordinates for distance calculation
                double lat = order.Latitude ?? 0;
                double lon = order.Longitude ?? 0;

                if (lat == 0 && lon == 0)
                {
                    // Attempt to geocode address if coordinates are missing
                    var coords = Tools.GetCoordinatesFromAddressAsync(order.CustomerAddress).Result;
                    lat = coords.Latitude;
                    lon = coords.Longitude;
                }

                // Calculate straight-line distance from company to customer
                double distance = Tools.BirdDistance(
                    config.CompanyLatitude,
                    config.CompanyLongitude,
                    lat,
                    lon
                );

                // Choose speed based on the last delivery's transport method, or default to car speed
                double speed = config.CarSpeed;
                if (lastDelivery != null)
                    speed = Tools.GetSpeed(lastDelivery.Transport, config);

                // Calculate estimated arrival time based on distance and speed
                DateTime? estArrival = distance > 0
                    ? Tools.CalculateEstimatedArrival(order.OrderDate, distance, speed)
                    : null;

                // Add risk range to determine maximum acceptable arrival time
                DateTime? maxArrival = estArrival?.Add(config.RiskRange);
                DateTime? realArrival = lastDelivery?.ArrivalTime;

                // Calculate order status from delivery records
                var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);

                // Determine schedule status based on arrival estimates
                var schedule = Tools.CalculateScheduleStatus(
                    orderStatus,
                    order.OrderDate,
                    estArrival,
                    maxArrival,
                    realArrival
                );

                // Build the OrderInList view model with all calculated metrics
                return new BO.OrderInList
                {
                    DeliveryId = deliveryId,
                    OrderId = order.Id,
                    Type = (BO.OrderType)order.Type,
                    Distance = distance,
                    Status = orderStatus,
                    ScheduleStatus = schedule,
                    // Total time from order creation to actual arrival (or current time if not yet delivered)
                    OrderEndTime = realArrival != null ? realArrival.Value - order.OrderDate : now - order.OrderDate,
                    // Time from order creation to when treatment/pickup began
                    TreatmentEndTime = lastDelivery != null ? lastDelivery.PickupTime - order.OrderDate : TimeSpan.Zero,
                    // Count distinct couriers who have handled this order
                    NumberOfCouriers = orderDeliveries.Select(d => d.CourierId).Distinct().Count()
                };

            }).ToList();

            return list;
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
                    Name = courier?.Name ?? string.Empty,
                    PickupTime = d.PickupTime,
                    OrderStatus = d.Status.HasValue ? (BO.OrderStatus?)(BO.OrderStatus)d.Status.Value : null,
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
            // Delete the order from the data layer
            s_dal.Order.Delete(orderId);
            
            // Notify subscribers that this order has been deleted and the list has changed
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
                throw new BLNotFoundException("Requester does not exist.");

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
    internal static void FinishOrder(int requesterId, int courierId, int deliveryId)
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
                // Record the calculated distance
                Distance = distance,
                // Mark status as delivered
                Status = DO.OrderStatus.Delivered
            };

            s_dal.Delivery.Update(updated);

            // Optionally update courier activity status (best-effort, failures are silently caught)
            try
            {
                Tools.UpdateCourierActivity(courier, config.InactivityThreshold);
                s_dal.Courier.Update(courier);
            }
            catch { }

            // Notify subscribers that this order has been completed
            Observers.NotifyItemUpdated(delivery.OrderId);
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
                PickupTime = DateTime.Now,
                // Arrival time will be filled when delivery is completed
                ArrivalTime = null,
                // Distance will be calculated when delivery is completed
                Distance = null,
                // Mark delivery as processing (started)
                Status = DO.OrderStatus.Processing
            };

            // Persist the new delivery to the data layer
            s_dal.Delivery.Create(delivery);
            
            // Notify subscribers that the order has been assigned
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
    internal static IEnumerable<BO.ClosedDeliveryInList> GetClosedDeliveriesForCourier(int requesterId, int courierId, BO.OrderType? filter, Enum? sorter)
    {
        try
        {
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Validate courier exists in the system
            var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");

            // Get all completed deliveries for this courier (those with recorded arrival times)
            var deliveries = s_dal.Delivery.ReadAll(d => d.CourierId == courierId && d.ArrivalTime != null).ToList();
            
            // Load all orders and index them by ID for efficient lookup
            var orders = s_dal.Order.ReadAll().ToDictionary(o => o.Id);

            // Project deliveries to ClosedDeliveryInList view models
            var list = deliveries.Select(d =>
            {
                // Retrieve associated order (or use default if not found)
                orders.TryGetValue(d.OrderId, out var o);
                return new BO.ClosedDeliveryInList
                {
                    DeliveryId = d.Id,
                    OrderId = d.OrderId,
                    // Map order type, or default to FastFood if order not found
                    OrderType = o != null ? (BO.OrderType)o.Type : BO.OrderType.FastFood,
                    CustomerAdress = o?.CustomerAddress ?? string.Empty,
                    // Map transport method
                    DeliveryTransport = (BO.DeliveryTransport)d.Transport,
                    ActualDistance = d.Distance,
                    // Calculate total delivery duration from pickup to arrival
                    DeliveryTotalTime = d.ArrivalTime!.Value - d.PickupTime,
                    // Map delivery status to ClosedDeliveryInList status enum
                    DeliveredStatus = d.Status switch
                    {
                        DO.OrderStatus.Delivered => BO.DeliveredStatus.Delivered,
                        DO.OrderStatus.Canceled => BO.DeliveredStatus.Canceled,
                        DO.OrderStatus.Returned => BO.DeliveredStatus.Rejected,
                        _ => BO.DeliveredStatus.Failed
                    }
                };
            }).ToList();

            // Apply optional order type filter
            if (filter.HasValue)
                list = list.Where(x => x.OrderType == filter.Value).ToList();

            // Apply optional sorting
            if (sorter != null)
            {
                string key = sorter.ToString() ?? string.Empty;
                bool ascending = true;
                
                // Sort by the specified field (always ascending for closed deliveries)
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
    /// Retrieves a list of open (not yet completed) deliveries for a specific courier with optional filtering
    /// and sorting capabilities.
    /// Only includes deliveries without recorded arrival times (active/pending deliveries).
    /// Calculates estimated delivery time and schedule status based on current time, distance, and transport speed.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this list (must exist in the system).</param>
    /// <param name="courierId">ID of the courier whose open deliveries are being retrieved.</param>
    /// <param name="filter">Optional filter by order type (FastFood, Pizza, etc.).</param>
    /// <param name="sorter">Optional sort key: supports "BirdDistance" and "AddedTime".</param>
    /// <returns>An enumerable collection of OpenOrderInList objects representing active deliveries.</returns>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester or courier does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static IEnumerable<BO.OpenOrderInList> GetOpenOrdersForCourier(int requesterId, int courierId, BO.OrderType? filter, BO.DeliveredStatus? sorter)
    {
        try
        {
            // Validate requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Validate courier exists in the system
            var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");

            // Get all active deliveries for this courier (those without recorded arrival times)
            var deliveries = s_dal.Delivery.ReadAll(d => d.CourierId == courierId && d.ArrivalTime == null).ToList();
            var orders = s_dal.Order.ReadAll().ToDictionary(o => o.Id);
            var config = AdminManager.GetConfig();

            var list = deliveries.Select(d =>
            {
                // Retrieve associated order (or use default if not found)
                orders.TryGetValue(d.OrderId, out var o);
                double lat = o?.Latitude ?? 0;
                double lon = o?.Longitude ?? 0;
                if (lat == 0 && lon == 0 && o != null)
                {
                    try
                    {
                        // Attempt to geocode the customer address if coordinates are missing
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
}
