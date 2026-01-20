using BO;
using DalApi;
using DO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Helpers;

/// <summary>
/// Order business logic (BL).
/// Stage 7 focus:
/// - Protect every DAL access with lock(AdminManager.BlMutex)
/// - Keep locks short; never hold a lock across await
/// - Run notifications (Observers.Notify...) outside locks
/// - Periodic/simulation methods use AsyncMutex to avoid overlapping runs
/// </summary>
internal static class OrderManager
{
    private static readonly IDal s_dal = Factory.Get;

    internal static ObserverManager Observers = new();

    // Prevent overlapping periodic runs (Stage 7)
    private static readonly AsyncMutex s_periodicMutex = new();

    // Prevent overlapping simulation runs (Stage 7)
    private static readonly AsyncMutex s_simulationMutex = new();

    // RNG for simulation decisions (Stage 7)
    private static readonly Random s_rand = new();

    /// <summary>
    /// Periodically closes expired deliveries (no changes to DO.Order by design).
    /// Stage 7: snapshot DAL under lock; perform updates under lock; notify outside lock.
    /// </summary>
    internal static void PeriodicOrdersUpdates(DateTime oldClock, DateTime newClock)
    {
        if (s_periodicMutex.CheckAndSetInProgress())
            return;

        try
        {
            TimeSpan maxDeliveryTime;
            List<DO.Order> ordersAll;
            List<DO.Delivery> deliveriesAll;

            // Snapshot config + data under one lock (short, stable enumeration)
            lock (AdminManager.BlMutex) // stage 7
            {
                maxDeliveryTime = s_dal.Config.MaxTimeDelivery;
                ordersAll = s_dal.Order.ReadAll().ToList();
                deliveriesAll = s_dal.Delivery.ReadAll().ToList();
            }

            var updatedOrders = new HashSet<int>();
            var updatedCouriers = new HashSet<int>();
            bool deliveriesUpdated = false;

            // Work on snapshots (no DAL here)
            foreach (var o in ordersAll)
            {
                // Not expired at newClock -> skip
                if (newClock - o.OrderDate <= maxDeliveryTime)
                    continue;

                var orderDeliveries = deliveriesAll.Where(d => d.OrderId == o.Id).ToList();

                // Open delivery = no end time AND no end type
                var openDeliveries = orderDeliveries
                    .Where(d => d.DeliveredStatus == null && d.ArrivalTime == null)
                    .ToList();

                if (openDeliveries.Count == 0)
                    continue;

                // Close every open delivery as failed at newClock
                foreach (var d in openDeliveries)
                {
                    var upd = d with
                    {
                        ArrivalTime = newClock,
                        DeliveredStatus = DO.DeliveredStatus.Failed
                    };

                    // Persist per-item update under lock (stage 7)
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

            // Notify outside locks (stage 7)
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
        finally
        {
            s_periodicMutex.UnsetInProgress();
        }
    }

    /// <summary>
    /// Returns a flattened (OrderStatus x ScheduleStatus) count array.
    /// Uses orderInListsAsync which already snapshots DAL under lock.
    /// </summary>
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

    // ---------------------------
    // Order list builders (async)
    // ---------------------------

    /// <summary>
    /// Builds one BO.OrderInList from DO order + snapshot deliveries.
    /// No DAL access here; safe for parallel/async.
    /// </summary>
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

        // Geocode if missing coordinates (outside any locks)
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

        // Treatment time can't be negative
        TimeSpan treatmentTime = TimeSpan.Zero;
        if (lastByPickup != null)
        {
            var raw = lastByPickup.PickupTime - order.OrderDate;
            treatmentTime = raw > TimeSpan.Zero ? raw : TimeSpan.Zero;
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
            TreatmentEndTime = treatmentTime,
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

    /// <summary>
    /// Returns orders list (filter/sort) based on DAL snapshots.
    /// Stage 7: snapshot orders+deliveries under lock, then compute outside lock.
    /// </summary>
    internal static async Task<IEnumerable<BO.OrderInList>> orderInListsAsync(int requesterId, Enum? filter, object? Object, Enum? sorter)
    {
        try
        {
            var config = AdminManager.GetConfig();
            if (requesterId != config.BossId)
                throw new BO.BLInvalidOperationException("Requester is not authorized for order management operations.");

            List<DO.Order> doOrders;
            List<DO.Delivery> deliveriesDO;

            lock (AdminManager.BlMutex) // stage 7
            {
                doOrders = s_dal.Order.ReadAll().ToList();
                deliveriesDO = s_dal.Delivery.ReadAll().ToList();
            }

            var now = AdminManager.Now;

            var list = await BuildOrderInListsAsync(doOrders, deliveriesDO, config, now).ConfigureAwait(false);

            // Filtering
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

            // Sorting (default: Status, then OrderId)
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

    /// <summary>
    /// Double-filter variant (filter1 + filter2), computed on snapshots.
    /// </summary>
    internal static async Task<IEnumerable<BO.OrderInList>> orderInListsDoubleFilterAsync(int requesterId, Enum? filter1, Enum? filter2)
    {
        try
        {
            var config = AdminManager.GetConfig();
            if (requesterId != config.BossId)
                throw new BO.BLInvalidOperationException("Requester is not authorized for order management operations.");

            List<DO.Order> doOrders;
            List<DO.Delivery> deliveriesDO;

            lock (AdminManager.BlMutex) // stage 7
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

            return list
                .OrderBy(l => l.Status)
                .ThenBy(l => l.OrderId)
                .ToList();
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
    /// Returns full order details.
    /// Stage 7: all DAL reads are locked; async work (geocode/distance/status) is outside locks.
    /// </summary>
    internal static async Task<BO.Order> GetOrderDetailsAsync(int requesterId, int orderId)
    {
        try
        {
            DO.Courier? requester;
            DO.Order? doOrder;
            List<DO.Delivery> deliveries;

            // Snapshot required DAL data under lock
            lock (AdminManager.BlMutex) // stage 7
            {
                requester = s_dal.Courier.Read(requesterId);
                doOrder = s_dal.Order.Read(orderId);
                deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            }

            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            if (doOrder == null)
                throw new BLNotFoundException($"Order with id {orderId} not found.");

            var impactedCouriers = deliveries
                .Where(d => d.CourierId != 0)
                .Select(d => d.CourierId)
                .Distinct()
                .ToList();

            var lastDelivery = deliveries.OrderByDescending(d => d.PickupTime).FirstOrDefault();

            var config = AdminManager.GetConfig();

            // Coordinates (geocode outside locks)
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

            double distance = (lat == 0 && lon == 0)
                ? 0
                : await Tools.BirdDistanceAsync(config.CompanyLatitude, config.CompanyLongitude, lat, lon).ConfigureAwait(false);

            double speed = config.CarSpeed;
            if (lastDelivery != null)
                speed = await Tools.GetSpeedAsync(lastDelivery.Transport, config).ConfigureAwait(false);

            DateTime? estArrival = distance > 0
                ? await Tools.CalculateEstimatedArrivalAsync(doOrder.OrderDate, distance, speed).ConfigureAwait(false)
                : (DateTime?)null;

            DateTime? maxArrival = estArrival?.Add(config.RiskRange);
            DateTime? realArrival = lastDelivery?.ArrivalTime;

            var status = await Tools.CalculateOrderStatusAsync(deliveries).ConfigureAwait(false);

            var schedule = await Tools.CalculateScheduleStatusAsync(
                status,
                doOrder.OrderDate,
                estArrival,
                maxArrival,
                realArrival
            ).ConfigureAwait(false);

            TimeSpan arrivalEstDuration = estArrival != null ? estArrival.Value - doOrder.OrderDate : TimeSpan.Zero;

            // Courier names: build a courierId->name map with one DAL call batch under lock
            var courierIds = deliveries.Where(d => d.CourierId != 0).Select(d => d.CourierId).Distinct().ToList();
            var courierNameById = new Dictionary<int, string>();

            if (courierIds.Count > 0)
            {
                lock (AdminManager.BlMutex) // stage 7
                {
                    foreach (var cid in courierIds)
                    {
                        var c = s_dal.Courier.Read(cid);
                        courierNameById[cid] = c?.Name ?? string.Empty;
                    }
                }
            }

            var deliveriesPerOrder = deliveries.Select(d => new BO.DeliveryPerOrderInList
            {
                DeliveryId = d.Id,
                CourierId = d.CourierId == 0 ? null : (int?)d.CourierId,
                CourierName = (d.CourierId != 0 && courierNameById.TryGetValue(d.CourierId, out var nm)) ? nm : string.Empty,
                transport = (BO.DeliveryTransport)d.Transport,
                PickupTime = d.PickupTime,
                DeliveredStatus = d.DeliveredStatus.HasValue
                    ? (BO.DeliveredStatus?)(BO.DeliveredStatus)d.DeliveredStatus.Value
                    : null,
                ArrivalTime = d.ArrivalTime
            }).ToList();

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
    /// Updates DO.Order fields. Geocoding is done outside locks.
    /// Stage 7: the final DAL update is locked; notifications are outside locks.
    /// </summary>
    internal static async Task UpdateOrderDetailsAsync(int requesterId, BO.Order order)
    {
        try
        {
            DO.Courier? requester;
            DO.Order? existingOrder;

            lock (AdminManager.BlMutex) // stage 7
            {
                requester = s_dal.Courier.Read(requesterId);
                existingOrder = s_dal.Order.Read(order.Id);
            }

            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

            if (existingOrder == null)
                throw new BLNotFoundException($"Order with id {order.Id} does not exist.");

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

            var doOrder = new DO.Order
            {
                Id = order.Id,
                Type = (DO.OrderType)order.Type,
                CustomerName = order.CustomerName,
                CustomerAddress = addressToSave,
                CustomerPhone = order.CustomerPhone ?? existingOrder.CustomerPhone,
                OrderDate = order.OrderDate != default ? order.OrderDate : existingOrder.OrderDate,
                size = order.Volume ?? existingOrder.size,
                weight = order.Weight ?? existingOrder.weight,
                Latitude = latitude,
                Longitude = longitude,
                Description = order.OrderDescription ?? existingOrder.Description,
                Fragility = order.Fragility.HasValue ? (DO.FragilityLevel)order.Fragility.Value : existingOrder.Fragility
            };

            lock (AdminManager.BlMutex) // stage 7
            {
                s_dal.Order.Update(doOrder);
            }

            // Notify outside lock
            Observers.NotifyItemUpdated(order.Id);
            Observers.NotifyListUpdated();

            if (badAddress)
                throw new BLBadAddressException("Customer address is invalid. Order saved with INVALID_ADDRESS.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateOrderDetails failed: {ex.GetType().Name}: {ex.Message}");

            if (ex is BO.BLNotFoundException ||
                ex is BO.BLInvalidInputException ||
                ex is BO.BLAlreadyExistsException ||
                ex is BO.BLInvalidOperationException) throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation($"Unexpected error in UpdateOrderDetails: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cancels an order by closing/adding a delivery with Canceled status.
    /// Stage 7: make status decision + DAL writes atomic (single lock), then notify outside lock.
    /// </summary>
    internal static void CancelOrder(int requesterId, int orderId)
    {
        try
        {
            int? courierIdToNotify = null;
            bool changed = false;

            lock (AdminManager.BlMutex) // stage 7 - atomic read/decide/write
            {
                var requester = s_dal.Courier.Read(requesterId);
                if (requester == null)
                    throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

                var existingOrder = s_dal.Order.Read(orderId);
                if (existingOrder == null)
                    throw new BLNotFoundException($"Order with id {orderId} does not exist.");

                var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();

                BO.OrderStatus status = Tools.CalculateOrderStatus(deliveries);

                if (status == BO.OrderStatus.Returned || status == BO.OrderStatus.Delivered)
                    throw new BO.BLInvalidOperationException($"Order {orderId} has already been delivered or returned.");

                if (status == BO.OrderStatus.Canceled)
                    throw new BO.BLInvalidOperationException($"Order {orderId} has already been cancelled.");

                if (status == BO.OrderStatus.Pending)
                {
                    s_dal.Delivery.Create(new DO.Delivery
                    {
                        Id = 0,
                        OrderId = orderId,
                        CourierId = 0,
                        PickupTime = AdminManager.Now,
                        DeliveredStatus = DO.DeliveredStatus.Canceled,
                        ArrivalTime = AdminManager.Now,
                        Distance = null,
                        Transport = DO.DeliveryTransport.Car // safe default if your DO requires it
                    });

                    changed = true;
                }
                else if (status == BO.OrderStatus.Processing)
                {
                    var lastDelivery = deliveries.OrderByDescending(d => d.PickupTime).First();

                    s_dal.Delivery.Update(lastDelivery with
                    {
                        ArrivalTime = AdminManager.Now,
                        DeliveredStatus = DO.DeliveredStatus.Canceled
                    });

                    courierIdToNotify = lastDelivery.CourierId != 0 ? lastDelivery.CourierId : null;
                    changed = true;
                }
            }

            if (changed)
            {
                CourierManager.InvalidateDeliveryCache();

                Observers.NotifyItemUpdated(orderId);
                Observers.NotifyListUpdated();

                if (courierIdToNotify.HasValue)
                {
                    CourierManager.Observers.NotifyItemUpdated(courierIdToNotify.Value);
                    CourierManager.Observers.NotifyListUpdated();
                }
            }
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
    /// Removes an order and all its deliveries.
    /// Stage 7: cascade delete must be atomic.
    /// </summary>
    internal static void RemoveOrder(int requesterId, int orderId)
    {
        try
        {
            List<int> impactedCouriers;

            lock (AdminManager.BlMutex) // stage 7 - atomic read + delete
            {
                var requester = s_dal.Courier.Read(requesterId);
                if (requester == null)
                    throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

                var orderToDelete = s_dal.Order.Read(orderId);
                if (orderToDelete == null)
                    throw new BLNotFoundException($"Order with id {orderId} does not exist.");

                var deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();

                impactedCouriers = deliveries
                    .Where(d => d.CourierId != 0)
                    .Select(d => d.CourierId)
                    .Distinct()
                    .ToList();

                foreach (var d in deliveries)
                    s_dal.Delivery.Delete(d.Id);

                s_dal.Order.Delete(orderId);
            }

            // Notify outside lock
            CourierManager.InvalidateDeliveryCache();

            Observers.NotifyItemUpdated(orderId);
            Observers.NotifyListUpdated();

            foreach (var cid in impactedCouriers)
                CourierManager.Observers.NotifyItemUpdated(cid);

            if (impactedCouriers.Count > 0)
                CourierManager.Observers.NotifyListUpdated();
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
    /// Adds a new order (geocode outside locks).
    /// Stage 7: DAL create is locked; notifications are outside lock.
    /// </summary>
    internal static async Task AddOrderAsync(int requesterId, BO.Order order)
    {
        try
        {
            DO.Courier? requester;
            lock (AdminManager.BlMutex) // stage 7
            {
                requester = s_dal.Courier.Read(requesterId);
            }

            if (requester == null)
                throw new BLNotFoundException($"Requester with id {requesterId} does not exist.");

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

            DateTime startDate = order.OrderDate == default ? AdminManager.Now : order.OrderDate;

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
                Latitude = latitude,
                Longitude = longitude,
                Description = order.OrderDescription,
                Fragility = order.Fragility.HasValue ? (DO.FragilityLevel)order.Fragility.Value : null
            };

            lock (AdminManager.BlMutex) // stage 7
            {
                s_dal.Order.Create(doOrder);
            }

            // Notify outside lock
            Observers.NotifyListUpdated();

            if (badAddress)
                throw new BLBadAddressException("Customer address is invalid. Order saved with INVALID_ADDRESS.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddOrder failed: {ex.GetType().Name}: {ex.Message}");

            if (ex is BO.BLNotFoundException ||
                ex is BO.BLInvalidInputException ||
                ex is BO.BLAlreadyExistsException ||
                ex is BO.BLInvalidOperationException) throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation($"Unexpected error in AddOrder: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Closes a delivery (finish order) for a courier.
    /// Stage 7: DAL update is locked; async distance work is outside locks.
    /// </summary>
    internal static async Task FinishOrderAsync(int requesterId, int courierId, int deliveryId, BO.DeliveredStatus deliveredStatus)
    {
        try
        {
            DO.Courier? requester;
            DO.Courier? courier;
            DO.Delivery? delivery;
            DO.Order? order;

            // Snapshot required DAL objects under lock
            lock (AdminManager.BlMutex) // stage 7
            {
                requester = s_dal.Courier.Read(requesterId);
                courier = s_dal.Courier.Read(courierId);
                delivery = s_dal.Delivery.Read(deliveryId);
                order = delivery != null ? s_dal.Order.Read(delivery.OrderId) : null;
            }

            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            if (courier == null)
                throw new BLNotFoundException($"Courier {courierId} not found.");

            if (delivery == null)
                throw new BLNotFoundException($"Delivery {deliveryId} not found.");

            if (order == null)
                throw new BLNotFoundException($"Order {delivery.OrderId} not found.");

            var config = AdminManager.GetConfig();

            // Geocode + distance outside locks
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

            var updated = delivery with
            {
                ArrivalTime = AdminManager.Now,
                DeliveredStatus = (DO.DeliveredStatus)deliveredStatus,
                Distance = distance
            };

            // Persist under lock
            lock (AdminManager.BlMutex) // stage 7
            {
                s_dal.Delivery.Update(updated);
            }

            // Notify outside lock
            CourierManager.InvalidateDeliveryCache();

            Observers.NotifyItemUpdated(delivery.OrderId);
            Observers.NotifyListUpdated();

            CourierManager.Observers.NotifyItemUpdated(courierId);
            CourierManager.Observers.NotifyListUpdated();

            if (badAddress)
                throw new BLBadAddressException("Customer address is invalid. Delivery saved without distance.");
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
    /// Assigns an order by creating a new open delivery.
    /// Stage 7: decision (availability) + create must be atomic.
    /// Any async distance precompute is best-effort and done outside locks.
    /// </summary>
    internal static async Task AssignOrderToCourierAsync(int requesterId, int orderId, int courierId)
    {
        try
        {
            // Quick requester check under lock
            DO.Courier? requester;
            lock (AdminManager.BlMutex) // stage 7
            {
                requester = s_dal.Courier.Read(requesterId);
            }
            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");

            // Best-effort distance precompute (no DAL writes; outside locks)
            // (You can remove this if you don't use it.)
            _ = await Task.FromResult(0).ConfigureAwait(false);

            int courierToNotify = 0;

            // Atomic read/decide/write under one lock
            lock (AdminManager.BlMutex) // stage 7
            {
                var courier = s_dal.Courier.Read(courierId);
                var order = s_dal.Order.Read(orderId);

                if (courier == null)
                    throw new BLNotFoundException($"Courier {courierId} not found.");
                if (order == null)
                    throw new BLNotFoundException($"Order {orderId} not found.");

                if (!courier.IsActive)
                    throw new BO.BLInvalidOperationException($"Cannot assign order {orderId}: courier {courierId} is not active.");

                if (courier.Administrator == DO.Administrator.Director)
                    throw new BO.BLInvalidOperationException($"Cannot assign order {orderId}: courier {courierId} is a Director.");

                var deliveriesForOrder = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();

                // Any open delivery => already assigned/processing
                if (deliveriesForOrder.Any(d => d.ArrivalTime == null && d.DeliveredStatus == null))
                {
                    var active = deliveriesForOrder
                        .First(d => d.ArrivalTime == null && d.DeliveredStatus == null);

                    string assignedName = string.Empty;
                    if (active.CourierId != 0)
                        assignedName = s_dal.Courier.Read(active.CourierId)?.Name ?? "Unknown";

                    throw new BO.BLInvalidOperationException(
                        $"Cannot assign order {orderId} to courier {courierId}: " +
                        $"order is already assigned to courier {active.CourierId} ({assignedName}) since {active.PickupTime:yyyy-MM-dd HH:mm}.");
                }

                // Check terminal order status from deliveries history
                var orderStatus = Tools.CalculateOrderStatus(deliveriesForOrder);
                if (orderStatus == BO.OrderStatus.Delivered ||
                    orderStatus == BO.OrderStatus.Returned ||
                    orderStatus == BO.OrderStatus.Canceled)
                {
                    throw new BO.BLInvalidOperationException(
                        $"Cannot assign order {orderId} to courier {courierId}: order is already {orderStatus.ToString().ToLower()}.");
                }

                var delivery = new DO.Delivery
                {
                    Id = 0,
                    OrderId = orderId,
                    Transport = courier.Transport,
                    CourierId = courierId,
                    PickupTime = AdminManager.Now,
                    ArrivalTime = null,
                    Distance = null,
                    DeliveredStatus = null
                };

                s_dal.Delivery.Create(delivery);
                courierToNotify = courierId;
            }

            // Notify outside lock
            CourierManager.InvalidateDeliveryCache();

            Observers.NotifyItemUpdated(orderId);
            Observers.NotifyListUpdated();

            if (courierToNotify != 0)
            {
                CourierManager.Observers.NotifyItemUpdated(courierToNotify);
                CourierManager.Observers.NotifyListUpdated();
            }
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
    /// Builds a live OrderInProgress snapshot for a courier/order.
    /// Stage 7: DAL reads are locked; async ETA work is outside locks.
    /// </summary>
    internal static async Task<BO.OrderInProgress> GetOrderInProgressSnapshotAsync(int requesterId, int courierId, int orderId)
    {
        try
        {
            DO.Courier? requester;
            DO.Courier? courier;
            DO.Order? orderDO;
            List<DO.Delivery> deliveries;

            lock (AdminManager.BlMutex) // stage 7
            {
                requester = s_dal.Courier.Read(requesterId);
                courier = s_dal.Courier.Read(courierId);
                orderDO = s_dal.Order.Read(orderId);
                deliveries = s_dal.Delivery.ReadAll(d => d.OrderId == orderId).ToList();
            }

            if (requester == null)
                throw new BLNotFoundException("Requester does not exist.");
            if (courier == null)
                throw new BLNotFoundException($"Courier {courierId} not found.");
            if (orderDO == null)
                throw new BLNotFoundException($"Order {orderId} not found.");

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
                    // Keep distance = null
                }
            }

            DateTime estimatedArrival = distance.HasValue
                ? await Tools.EstimateArrivalAsync(currentDelivery.PickupTime, currentDelivery.Transport, distance.Value).ConfigureAwait(false)
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
    /// Simulation: create random orders and auto-assign to available couriers.
    /// Stage 7: AsyncMutex prevents overlap; DAL snapshots are locked; notifications outside locks.
    /// </summary>
    internal static async Task SimulateOrderActivityAsync() // stage 7
    {
        if (s_simulationMutex.CheckAndSetInProgress())
            return;

        try
        {
            const double NEW_ORDER_PROBABILITY = 0.08;
            const double AUTO_ASSIGN_PROBABILITY = 0.30;

            var config = AdminManager.GetConfig();
            var now = AdminManager.Now;

            bool orderCreated = false;
            bool ordersModified = false;

            var updatedOrderIds = new HashSet<int>();
            var updatedCourierIds = new HashSet<int>();

            // 1) Maybe create a new order
            if (s_rand.NextDouble() < NEW_ORDER_PROBABILITY)
            {
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
                    Id = 0,
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
                    await AddOrderAsync(config.BossId, newOrder).ConfigureAwait(false);
                    orderCreated = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Simulation: Failed to create order: {ex.Message}");
                }
            }

            // 2) Snapshot pending orders + deliveries under lock
            List<DO.Order> pendingOrders;
            List<DO.Delivery> allDeliveries;

            lock (AdminManager.BlMutex) // stage 7
            {
                var allOrders = s_dal.Order.ReadAll().ToList();
                allDeliveries = s_dal.Delivery.ReadAll().ToList();

                pendingOrders = allOrders
                    .Where(o =>
                    {
                        var ods = allDeliveries.Where(d => d.OrderId == o.Id).ToList();

                        if (ods.Count == 0)
                            return true;

                        if (ods.Any(d => d.ArrivalTime == null))
                            return false;

                        return !ods.All(d =>
                            d.DeliveredStatus == DO.DeliveredStatus.Delivered ||
                            d.DeliveredStatus == DO.DeliveredStatus.Rejected ||
                            d.DeliveredStatus == DO.DeliveredStatus.Canceled);
                    })
                    .ToList();
            }

            // 3) Snapshot active couriers under lock (once)
            List<DO.Courier> activeCouriers;
            lock (AdminManager.BlMutex) // stage 7
            {
                activeCouriers = s_dal.Courier.ReadAll()
                    .Where(c => c.IsActive && c.Administrator != DO.Administrator.Director)
                    .ToList();
            }

            if (pendingOrders.Count > 0 && activeCouriers.Count > 0)
            {
                foreach (var order in pendingOrders)
                {
                    if (s_rand.NextDouble() >= AUTO_ASSIGN_PROBABILITY)
                        continue;

                    var selectedCourier = activeCouriers[s_rand.Next(activeCouriers.Count)];

                    try
                    {
                        // Assign method has its own atomic lock for decision+create
                        await AssignOrderToCourierAsync(config.BossId, order.Id, selectedCourier.Id).ConfigureAwait(false);

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

            // 4) Notifications outside locks
            if (orderCreated)
                Observers.NotifyListUpdated();

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
            s_simulationMutex.UnsetInProgress();
        }
    }
}
