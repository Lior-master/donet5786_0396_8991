using BO;
using DalApi;
using DO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Helpers;

/// <summary>
/// Manages courier-related operations in the Business Logic layer.
/// Handles courier creation, updates, deletion, login, and retrieval with authorization checks.
/// Provides observer notifications for UI synchronization when courier data changes.
/// </summary>
internal static class CourierManager
{
    /// <summary>
    /// Static reference to the Data Access Layer providing access to all data repositories.
    /// </summary>
    private static readonly IDal s_dal = Factory.Get;

    /// <summary>
    /// Observer manager for notifying subscribers of courier list and item changes.
    /// Enables real-time UI updates when courier data is modified.
    /// </summary>
    internal static ObserverManager Observers = new();

    /// <summary>
    /// Cache for delivery data to prevent concurrent file access issues.
    /// Thread-safe collection that stores deliveries with timestamp for cache invalidation.
    /// </summary>
    private static readonly ConcurrentDictionary<string, (DateTime timestamp, IEnumerable<Delivery> data)> _deliveryCache = new();

    /// <summary>
    /// Cache expiration time in minutes. Deliveries are cached for 5 minutes to balance performance and data freshness.
    /// </summary>
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Non-blocking mutex for periodic updates to prevent overlapping runs (Stage 7).
    /// </summary>
    private static readonly AsyncMutex s_periodicMutex = new();

    /// <summary>
    /// Thread-safe method to get delivery data, using cache when available and valid.
    /// Falls back to database access when cache is expired or missing.
    /// </summary>
    private static IEnumerable<Delivery> GetDeliveries()
    {
        const string cacheKey = "all_deliveries";
        var now = DateTime.Now;

        // Try to get from cache first
        if (_deliveryCache.TryGetValue(cacheKey, out var cached) &&
            (now - cached.timestamp) < CacheExpiration)
        {
            return cached.data;
        }

        // Cache miss or expired - refresh from database
        try
        {
            List<Delivery> deliveries;
            lock (AdminManager.BlMutex) // stage 7
            {
                deliveries = s_dal.Delivery.ReadAll().ToList(); // materialize under lock
            }

            _deliveryCache.AddOrUpdate(cacheKey,
                (now, deliveries),
                (key, old) => (now, deliveries));

            return deliveries;
        }
        catch (Exception ex) when (ex is DO.DalXMLFileLoadCreateException)
        {
            // If we have stale cache data, return it rather than failing
            if (_deliveryCache.TryGetValue(cacheKey, out var staleCache))
            {
                return staleCache.data;
            }

            throw;
        }
    }

    /// <summary>
    /// Invalidates the delivery cache to ensure fresh data is loaded on next access.
    /// Call this method whenever delivery data is modified.
    /// </summary>
    internal static void InvalidateDeliveryCache() => _deliveryCache.Clear();

    /// <summary>
    /// Adds a new courier to the system after validating the requester's existence.
    /// Automatically generates a unique courier ID if not provided, and sets a valid start date.
    /// Notifies observers of the list update upon successful creation.
    /// </summary>
    internal static void addCourier(int requesterId, BO.Courier newCourier)
    {
        try
        {
            int idToUse = newCourier.Id;

            // Ensure StartDate is valid (avoid DateTime.MinValue)
            DateTime startDate = newCourier.StartDate == default ? AdminManager.Now : newCourier.StartDate;

            lock (AdminManager.BlMutex) // stage 7 - make the DAL sequence atomic
            {
                // Validate requester exists
                var requester = s_dal.Courier.Read(requesterId);
                if (requester is null)
                    throw new BLNotFoundException("requesterId doesn't exist");

                // Generate unique Id if not provided
                if (idToUse == 0)
                {
                    HashSet<int> existing = new(s_dal.Courier.ReadAll().Select(c => c.Id));
                    int candidate;
                    do
                    {
                        candidate = (Math.Abs(Guid.NewGuid().GetHashCode()) % 90000000) + 100000;
                    } while (existing.Contains(candidate));

                    idToUse = candidate;
                }

                DO.Courier courierDO = new()
                {
                    Id = idToUse,
                    Name = newCourier.Name,
                    Phone = newCourier.Phone,
                    Email = newCourier.Email,
                    IsActive = newCourier.IsActive,
                    Transport = (DO.DeliveryTransport)newCourier.Transport,
                    StartDate = startDate,
                    MaxDistance = newCourier.MaxDistance,
                    Administrator = (DO.Administrator)newCourier.Administrator,
                    Password = newCourier.Password
                };

                s_dal.Courier.Create(courierDO);
            }

            // Notify OUTSIDE lock (stage 7)
            Observers.NotifyListUpdated();
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
    /// Periodically updates courier activity status by marking inactive couriers as inactive if they exceed the inactivity threshold.
    /// Stage 7: Uses non-blocking mutex to prevent overlapping runs. All DAL operations are wrapped with lock(AdminManager.BlMutex),
    /// while observer notifications are performed outside locks.
    /// </summary>
    internal static void PeriodicCouriersUpdates(DateTime oldClock, DateTime newClock)
    {
        // Non-blocking mutex: if already running, skip immediately
        if (s_periodicMutex.CheckAndSetInProgress())
            return;

        try
        {
            TimeSpan inactivityThreshold;
            List<DO.Courier> activeCouriers;

            // Snapshot required DAL state under lock (short critical section)
            lock (AdminManager.BlMutex) // stage 7
            {
                inactivityThreshold = s_dal.Config.Inactivity;
                activeCouriers = s_dal.Courier.ReadAll().Where(c => c.IsActive).ToList();
            }

            // Deliveries snapshot (cache is already protected inside GetDeliveries)
            var deliveriesSnapshot = GetDeliveries().ToList();

            var updatedCourierIds = new List<int>();

            foreach (var c in activeCouriers)
            {
                var lastArrival = deliveriesSnapshot
                    .Where(d => d.CourierId == c.Id && d.ArrivalTime != null)
                    .OrderByDescending(d => d.ArrivalTime)
                    .Select(d => d.ArrivalTime!.Value)
                    .FirstOrDefault();

                if (lastArrival == default)
                    continue;

                if ((oldClock - lastArrival) <= inactivityThreshold &&
                    (newClock - lastArrival) > inactivityThreshold)
                {
                    // Update under lock to avoid races with UI/simulator actions
                    lock (AdminManager.BlMutex) // stage 7
                    {
                        var fresh = s_dal.Courier.Read(c.Id);
                        if (fresh is not null && fresh.IsActive)
                            s_dal.Courier.Update(fresh with { IsActive = false });
                    }

                    updatedCourierIds.Add(c.Id);
                }
            }

            // Notify observers OUTSIDE locks (stage 7)
            if (updatedCourierIds.Count > 0)
            {
                foreach (var id in updatedCourierIds)
                    Observers.NotifyItemUpdated(id);

                Observers.NotifyListUpdated();
            }
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
        finally
        {
            s_periodicMutex.UnsetInProgress();
        }
    }

    /// <summary>
    /// Authenticates a courier by verifying their ID and password.
    /// Returns the courier's administrator role upon successful authentication.
    /// </summary>
    internal static BO.Administrator Login(int Id, string password)
    {
        try
        {
            if (Id == 0)
                throw new BO.BLInvalidInputException("ID cant be 0");

            DO.Courier? courier;
            lock (AdminManager.BlMutex) // stage 7
            {
                // Prefer Read(Id) if your DAL supports it; keeping your logic but fully under lock
                courier = s_dal.Courier.ReadAll().FirstOrDefault(c => c.Id == Id);
            }

            if (courier == null)
                throw new BO.BLNotFoundException("User with this Id not found.");

            if (courier.Password != password)
                throw new BO.BLInvalidInputException("Wrong password.");

            return (BO.Administrator)courier.Administrator;
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException)
                throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Retrieves a filtered list of couriers with performance metrics (on-time vs. late deliveries).
    /// </summary>
    internal static IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, Enum? Filter)
    {
        try
        {
            DO.Courier? requester;
            List<DO.Courier> couriersSnapshot;

            lock (AdminManager.BlMutex) // stage 7
            {
                requester = s_dal.Courier.Read(requesterId);
                couriersSnapshot = s_dal.Courier.ReadAll().ToList(); // snapshot for stable enumeration
            }

            if (requester == null)
                throw new BLNotFoundException("requesterId doesn't exist");

            IEnumerable<DO.Courier> couriers = couriersSnapshot;

            if (isActive != null)
                couriers = couriers.Where(c => c.IsActive == isActive);

            if (Filter != null)
            {
                if (Filter is BO.Administrator adminStatus)
                    couriers = couriers.Where(c => (BO.Administrator)c.Administrator == adminStatus);
                else if (Filter is BO.DeliveryTransport transportType)
                    couriers = couriers.Where(c => (BO.DeliveryTransport)c.Transport == transportType);
            }

            var allDeliveries = GetDeliveries().ToList();
            var config = AdminManager.GetConfig();

            double GetSpeed(DO.DeliveryTransport transport)
                => transport switch
                {
                    DO.DeliveryTransport.Motorcycle => config.MotorcycleSpeed,
                    DO.DeliveryTransport.Bike => config.BikeSpeed,
                    DO.DeliveryTransport.Foot => config.WalkingSpeed,
                    _ => config.CarSpeed,
                };

            return couriers.Select(c =>
            {
                var courierDeliveries = allDeliveries.Where(d => d.CourierId == c.Id);

                int onTime = 0;
                int late = 0;

                foreach (var d in courierDeliveries)
                {
                    if (d.ArrivalTime == null || d.Distance == null)
                        continue;

                    double speed = GetSpeed(d.Transport);
                    DateTime expected = d.PickupTime.AddHours(d.Distance.Value / (speed > 0 ? speed : config.CarSpeed));

                    if (Tools.IsDeliveryOnTime(d, expected))
                        onTime++;
                    else
                        late++;
                }

                return new CourierInList
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsActive = c.IsActive,
                    Transport = (BO.DeliveryTransport)c.Transport,
                    StartDate = c.StartDate,
                    NumberOfOnTimeDeliveries = onTime,
                    NumberOfLateDeliveries = late,
                    ActualOrder = courierDeliveries.FirstOrDefault(d => d.ArrivalTime == null)?.OrderId
                };
            });
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
    /// Retrieves detailed information about a specific courier, including their delivery performance statistics
    /// and current order assignment if any.
    /// </summary>
    internal static async Task<BO.Courier> GetCourierDetailsAsync(int requesterId, int courierId)
    {
        try
        {
            DO.Courier? requester;
            DO.Courier? courierDO;

            lock (AdminManager.BlMutex) // stage 7
            {
                requester = s_dal.Courier.Read(requesterId);
                courierDO = s_dal.Courier.Read(courierId);
            }

            if (requester == null)
                throw new BLNotFoundException("Requester ID does not exist.");

            if (courierDO == null)
                throw new BLNotFoundException("Courier ID does not exist.");

            var courierDeliveries = GetDeliveries().Where(d => d.CourierId == courierId).ToList();

            int onTime = 0;
            int late = 0;

            foreach (var d in courierDeliveries)
            {
                if (d.ArrivalTime == null || d.Distance == null)
                    continue;

                DateTime expected = await Tools.EstimateArrivalAsync(d.PickupTime, d.Transport, d.Distance.Value)
                    .ConfigureAwait(false);

                if (await Tools.IsDeliveryOnTimeAsync(d, expected).ConfigureAwait(false))
                    onTime++;
                else
                    late++;
            }

            var currentDelivery = courierDeliveries.FirstOrDefault(d => d.ArrivalTime == null);

            BO.OrderInProgress? currentOrder = null;

            if (currentDelivery != null)
            {
                DO.Order? orderDO;
                lock (AdminManager.BlMutex) // stage 7
                {
                    orderDO = s_dal.Order.Read(currentDelivery.OrderId);
                }

                if (orderDO != null)
                {
                    var config = AdminManager.GetConfig();

                    var deliveriesForOrder = GetDeliveries().Where(d => d.OrderId == orderDO.Id).ToList();
                    var ordStatus = await Tools.CalculateOrderStatusAsync(deliveriesForOrder).ConfigureAwait(false);

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
                            // keep distance = null
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

                    currentOrder = new BO.OrderInProgress
                    {
                        DeliveryId = currentDelivery.Id,
                        OrderId = orderDO.Id,
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
            }

            return new BO.Courier
            {
                Id = courierDO.Id,
                Name = courierDO.Name,
                Password = courierDO.Password,
                Phone = courierDO.Phone,
                Email = courierDO.Email,
                IsActive = courierDO.IsActive,
                Transport = (BO.DeliveryTransport)courierDO.Transport,
                StartDate = courierDO.StartDate,
                MaxDistance = courierDO.MaxDistance,
                Administrator = (BO.Administrator)courierDO.Administrator,
                NumberOfOnTimeDeliveries = onTime,
                NumberOfLateDeliveries = late,
                CurrentOrder = currentOrder
            };
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
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException)
                throw new BO.BLFailedOperation(ex.Message, ex);

            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Updates an existing courier's information with new values.
    /// </summary>
    internal static void UpdateCourier(int requesterId, BO.Courier updatedCourier)
    {
        try
        {
            DO.Courier? requester;
            DO.Courier? existingCourier;

            lock (AdminManager.BlMutex) // stage 7
            {
                requester = s_dal.Courier.Read(requesterId);
                if (requester == null)
                    throw new BLNotFoundException("requesterId doesn't exist");

                existingCourier = s_dal.Courier.Read(updatedCourier.Id);
                if (existingCourier == null)
                    throw new BLNotFoundException($"Courier with ID {updatedCourier.Id} doesn't exist");

                existingCourier = existingCourier with
                {
                    Name = updatedCourier.Name,
                    Phone = updatedCourier.Phone,
                    Email = updatedCourier.Email,
                    IsActive = updatedCourier.IsActive,
                    Transport = (DO.DeliveryTransport)updatedCourier.Transport,
                    Administrator = (DO.Administrator)updatedCourier.Administrator,
                    Password = updatedCourier.Password,
                    MaxDistance = updatedCourier.MaxDistance
                };

                s_dal.Courier.Update(existingCourier);
            }

            // notify outside lock
            Observers.NotifyItemUpdated(updatedCourier.Id);
            Observers.NotifyListUpdated();
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
    /// Promotes a courier to the Director administrator role.
    /// </summary>
    internal static void PromoteCourierToDirector(int requesterId, int courierId)
    {
        try
        {
            lock (AdminManager.BlMutex) // stage 7
            {
                var requester = s_dal.Courier.Read(requesterId);
                if (requester == null)
                    throw new BLNotFoundException("Requester ID does not exist.");

                if (requester.Administrator != DO.Administrator.Director)
                    throw new BO.BLUnauthorizedException("Only a Director can promote another courier.");

                var courier = s_dal.Courier.Read(courierId);
                if (courier == null)
                    throw new BLNotFoundException($"Courier {courierId} not found.");

                var updated = courier with { Administrator = DO.Administrator.Director };
                s_dal.Courier.Update(updated);
            }

            // notify outside lock
            Observers.NotifyItemUpdated(courierId);
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException ||
                ex is BO.BLInvalidInputException ||
                ex is BO.BLAlreadyExistsException ||
                ex is BO.BLInvalidOperationException ||
                ex is BO.BLUnauthorizedException)
                throw;

            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Removes a courier from the system with validation checks.
    /// Stage 7: the decision (no deliveries) + delete must be atomic, so they are performed in one lock.
    /// </summary>
    internal static void removeCourier(int requesterId, int courierId)
    {
        try
        {
            lock (AdminManager.BlMutex) // stage 7 - atomic check + delete
            {
                var requester = s_dal.Courier.Read(requesterId);
                if (requester == null)
                    throw new BLNotFoundException("Requester ID does not exist.");

                var courier = s_dal.Courier.Read(courierId);
                if (courier == null)
                    throw new BLNotFoundException($"Courier ID {courierId} does not exist.");

                var deliveries = s_dal.Delivery.ReadAll().Where(d => d.CourierId == courierId).ToList();

                if (deliveries.Any())
                    throw new BLInvalidOperationException("This courier has handled deliveries and cannot be deleted.");

                if (deliveries.Any(d => d.ArrivalTime == null))
                    throw new BLInvalidOperationException("This courier is currently handling a delivery and cannot be deleted.");

                s_dal.Courier.Delete(courierId);
            }

            // outside lock
            InvalidateDeliveryCache();
            Observers.NotifyItemUpdated(courierId);
            Observers.NotifyListUpdated();
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
    /// Simulates courier activity (Stage 7).
    /// Called asynchronously from the simulator thread once per second.
    /// All DAL access is protected with lock blocks. Observer notifications are outside locks.
    /// </summary>
    internal static async Task SimulateCourierActivityAsync()
    {
        try
        {
            List<DO.Courier> activeCouriers;
            List<DO.Delivery> allDeliveries;

            lock (AdminManager.BlMutex) // stage 7
            {
                activeCouriers = s_dal.Courier.ReadAll()
                    .Where(c => c.IsActive)
                    .ToList();

                allDeliveries = s_dal.Delivery.ReadAll().ToList();
            }

            if (activeCouriers.Count == 0)
                return;

            var config = AdminManager.GetConfig();
            var now = AdminManager.Now;
            Random random = new();

            bool notificationNeeded = false;
            var updatedCourierIds = new HashSet<int>();
            var updatedOrderIds = new HashSet<int>();

            foreach (var courier in activeCouriers)
            {
                var courierInProgress = allDeliveries
                    .Where(d => d.CourierId == courier.Id && d.ArrivalTime == null)
                    .ToList();

                if (courierInProgress.Count > 0)
                {
                    HandleDeliveryCompletion(
                        courier,
                        courierInProgress.First(),
                        config,
                        now,
                        random,
                        ref notificationNeeded,
                        updatedCourierIds,
                        updatedOrderIds);
                }
                else
                {
                    var (orderNotification, courierIds, orderIds) =
                        await HandleCourierOrderSelectionAsync(courier, config, now, random).ConfigureAwait(false);

                    if (orderNotification)
                    {
                        notificationNeeded = true;
                        foreach (var id in courierIds) updatedCourierIds.Add(id);
                        foreach (var id in orderIds) updatedOrderIds.Add(id);
                    }
                }
            }

            if (notificationNeeded)
            {
                InvalidateDeliveryCache();

                foreach (var courierId in updatedCourierIds)
                    Observers.NotifyItemUpdated(courierId);

                Observers.NotifyListUpdated();

                OrderManager.Observers.NotifyListUpdated();
                foreach (var orderId in updatedOrderIds)
                    OrderManager.Observers.NotifyItemUpdated(orderId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SimulateCourierActivity failed: {ex.Message}");
        }
    }

    private static async Task<(bool notificationNeeded, HashSet<int> updatedCourierIds, HashSet<int> updatedOrderIds)>
        HandleCourierOrderSelectionAsync(DO.Courier courier, BO.Config config, DateTime now, Random random)
    {
        const double AVAILABILITY_PROBABILITY = 0.15;
        const double ORDER_SELECTION_PROBABILITY = 0.50;

        bool notificationNeeded = false;
        var updatedCourierIds = new HashSet<int>();
        var updatedOrderIds = new HashSet<int>();

        if (random.NextDouble() > AVAILABILITY_PROBABILITY)
            return (false, updatedCourierIds, updatedOrderIds);

        try
        {
            var availableOrders = (await OrderManager.orderInListsAsync(
                config.BossId, null, courier.Id, null).ConfigureAwait(false)).ToList();

            if (availableOrders.Count == 0)
                return (false, updatedCourierIds, updatedOrderIds);

            if (random.NextDouble() > ORDER_SELECTION_PROBABILITY)
                return (false, updatedCourierIds, updatedOrderIds);

            var selectedOrder = availableOrders[random.Next(availableOrders.Count)];

            bool assigned = false;

            lock (AdminManager.BlMutex) // stage 7 - atomic checks + create delivery
            {
                var orderToAssign = s_dal.Order.Read(selectedOrder.OrderId);
                if (orderToAssign == null)
                    assigned = false;
                else
                {
                    var orderDeliveries = s_dal.Delivery.ReadAll(d => d.OrderId == selectedOrder.OrderId).ToList();
                    var orderStatus = Tools.CalculateOrderStatus(orderDeliveries);

                    if (orderDeliveries.Any(d => d.DeliveredStatus == DO.DeliveredStatus.Delivered) ||
                        orderStatus == BO.OrderStatus.Delivered ||
                        orderStatus == BO.OrderStatus.Returned ||
                        orderStatus == BO.OrderStatus.Canceled)
                    {
                        assigned = false;
                    }
                    else if (orderDeliveries.Any(d => d.ArrivalTime == null && d.DeliveredStatus == null))
                    {
                        assigned = false;
                    }
                    else
                    {
                        var delivery = new DO.Delivery
                        {
                            Id = 0,
                            OrderId = selectedOrder.OrderId,
                            Transport = courier.Transport,
                            CourierId = courier.Id,
                            PickupTime = now,
                            ArrivalTime = null,
                            Distance = null,
                            DeliveredStatus = null
                        };

                        s_dal.Delivery.Create(delivery);
                        assigned = true;
                    }
                }
            }

            if (assigned)
            {
                notificationNeeded = true;
                updatedCourierIds.Add(courier.Id);
                updatedOrderIds.Add(selectedOrder.OrderId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error selecting order for courier {courier.Id}: {ex.Message}");
        }

        return (notificationNeeded, updatedCourierIds, updatedOrderIds);
    }

    private static void HandleDeliveryCompletion(
        DO.Courier courier,
        DO.Delivery delivery,
        BO.Config config,
        DateTime now,
        Random random,
        ref bool notificationNeeded,
        HashSet<int> updatedCourierIds,
        HashSet<int> updatedOrderIds)
    {
        const double CANCELLATION_PROBABILITY = 0.10;
        TimeSpan minDeliveryDuration = TimeSpan.FromMinutes(15);

        try
        {
            DO.Order? order;
            lock (AdminManager.BlMutex) // stage 7
            {
                order = s_dal.Order.Read(delivery.OrderId);
            }

            if (order == null)
                return;

            double distance = 0;
            if (order.Latitude.HasValue && order.Longitude.HasValue &&
                (order.Latitude != 0 || order.Longitude != 0))
            {
                try
                {
                    distance = Tools.CalculateRouteDistanceCachedAsync(
                            config.CompanyLatitude,
                            config.CompanyLongitude,
                            order.Latitude.Value,
                            order.Longitude.Value)
                        .GetAwaiter()
                        .GetResult();
                }
                catch
                {
                    distance = Tools.BirdDistance(
                        config.CompanyLatitude,
                        config.CompanyLongitude,
                        order.Latitude.Value,
                        order.Longitude.Value);
                }
            }

            double speed = Tools.GetSpeed(delivery.Transport, config);
            double travelTimeHours = speed > 0 ? distance / speed : 0.5;
            TimeSpan travelTime = TimeSpan.FromHours(travelTimeHours);

            int serviceTimeMinutes = random.Next(1, 6);
            TimeSpan serviceTime = TimeSpan.FromMinutes(serviceTimeMinutes);

            TimeSpan totalExpectedTime = travelTime + serviceTime;
            if (totalExpectedTime < minDeliveryDuration)
                totalExpectedTime = minDeliveryDuration;
            TimeSpan elapsedTime = now - delivery.PickupTime;

            if (elapsedTime >= totalExpectedTime)
            {
                BO.DeliveredStatus deliveryStatus;
                double statusRoll = random.NextDouble();
                deliveryStatus = statusRoll < 0.85 ? BO.DeliveredStatus.Delivered :
                                 statusRoll < 0.92 ? BO.DeliveredStatus.Rejected :
                                 statusRoll < 0.98 ? BO.DeliveredStatus.Absent :
                                 BO.DeliveredStatus.Failed;

                lock (AdminManager.BlMutex) // stage 7
                {
                    var updatedDelivery = delivery with
                    {
                        ArrivalTime = now,
                        DeliveredStatus = (DO.DeliveredStatus)deliveryStatus
                    };

                    try
                    {
                        s_dal.Delivery.Update(updatedDelivery);
                        notificationNeeded = true;
                        updatedCourierIds.Add(courier.Id);
                        updatedOrderIds.Add(delivery.OrderId);
                    }
                    catch
                    {
                        // silently ignore (might have been updated elsewhere)
                    }
                }
            }
            else if (random.NextDouble() < CANCELLATION_PROBABILITY)
            {
                lock (AdminManager.BlMutex) // stage 7
                {
                    try
                    {
                        var cancelledDelivery = delivery with
                        {
                            ArrivalTime = now,
                            DeliveredStatus = DO.DeliveredStatus.Canceled
                        };

                        s_dal.Delivery.Update(cancelledDelivery);
                        notificationNeeded = true;
                        updatedCourierIds.Add(courier.Id);
                        updatedOrderIds.Add(delivery.OrderId);

                        System.Diagnostics.Debug.WriteLine(
                            $"Delivery {delivery.Id} for order {delivery.OrderId} cancelled by admin (insufficient time elapsed)");
                    }
                    catch
                    {
                        // silently ignore
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error completing delivery {delivery.Id}: {ex.Message}");
        }
    }
}
