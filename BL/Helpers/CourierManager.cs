using BO;
using DalApi;
using DO;
using System.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    /// Thread-safe method to get delivery data, using cache when available and valid.
    /// Falls back to database access when cache is expired or missing.
    /// </summary>
    /// <returns>Current delivery data from cache or database</returns>
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
            var deliveries = s_dal.Delivery.ReadAll().ToList(); // Materialize to avoid multiple enumerations
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
    internal static void InvalidateDeliveryCache()
    {
        _deliveryCache.Clear();
    }

    /// <summary>
    /// Adds a new courier to the system after validating the requester's existence.
    /// Automatically generates a unique courier ID if not provided, and sets a valid start date.
    /// Notifies observers of the list update upon successful creation.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this operation (must exist in the system).</param>
    /// <param name="newCourier">The courier object containing details to be added.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester ID does not exist.</exception>
    /// <exception cref="BO.BLAlreadyExistsException">Thrown if a courier with the same ID already exists.</exception>
    /// <exception cref="BO.BLInvalidInputException">Thrown for invalid input data format.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void addCourier(int requesterId, BO.Courier newCourier)
    {
        try
        {
            // Validate that the requester exists in the system
            try
            {
                var requester = s_dal.Courier.Read(requesterId);
            }
            catch (Exception)
            {
                throw new BLNotFoundException("requesterId doesn't exist");
            }

            // Ensure StartDate is valid (avoid DateTime.MinValue)
            DateTime startDate = newCourier.StartDate == default ? AdminManager.Now : newCourier.StartDate;

            // If caller did not provide an Id (0), generate one on BL side to avoid persisting Id == 0.
            // This avoids UI showing Id = 0 when DAL doesn't auto-generate an id.
            int idToUse = newCourier.Id;
            if (idToUse == 0)
            {
                // Build set of existing ids to avoid collision
                var existing = new HashSet<int>(s_dal.Courier.ReadAll().Select(c => c.Id));
                int candidate;
                do
                {
                    candidate = (Math.Abs(Guid.NewGuid().GetHashCode()) % 90000000) + 100000;
                } while (existing.Contains(candidate));
                idToUse = candidate;
            }

            // Map Business Object to Data Object and set all required properties
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
            
            // Persist the new courier to the data layer
            s_dal.Courier.Create(courierDO);
            
            // Notify subscribers that the courier list has been updated
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
    /// Periodically updates courier activity status by marking inactive couriers as inactive if they exceed the inactivity threshold.
    /// Compares the old and new clock times to determine if any couriers have become inactive since the last check.
    /// Notifies observers when courier statuses are modified.
    /// </summary>
    /// <param name="oldClock">The previous clock time (start of the period being checked).</param>
    /// <param name="newClock">The current clock time (end of the period being checked).</param>
    /// <remarks>
    /// This method identifies couriers who were active during the old period but are now considered inactive
    /// based on the system's inactivity threshold configuration. Only couriers with recorded delivery arrivals are evaluated.
    /// </remarks>
    /// <exception cref="BO.BLNotFoundException">Thrown if required configuration or courier data cannot be retrieved.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void PeriodicCouriersUpdates(DateTime oldClock, DateTime newClock)
    {
        try
        {
            // Retrieve the configured inactivity threshold from system settings
            TimeSpan inactivityThreshold = s_dal.Config.Inactivity;

            // Get all delivery records to analyze courier activity using cached data
            var allDeliveries = GetDeliveries();

            // Process all active couriers to check for inactivity
            var updatedCouriers = s_dal.Courier.ReadAll()
                .Where(c => c.IsActive)
                // Calculate the last arrival time for each courier
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
                // Filter: only process couriers who have delivered something
                .Where(x => x.LastArrival != null)
                // Filter: only process couriers who became inactive between oldClock and newClock
                .Where(x =>
                    (oldClock - x.LastArrival!.Value) <= inactivityThreshold &&
                    (newClock - x.LastArrival!.Value) > inactivityThreshold)
                // Mark inactive couriers and update them
                .Select(x =>
                {
                    var updated = x.Courier with { IsActive = false };
                    s_dal.Courier.Update(updated);
                    
                    // Notify subscribers that this specific courier has been modified
                    Observers.NotifyItemUpdated(updated.Id);
                    
                    return updated;
                })
                .ToList();
                
            // If any couriers were modified, notify list observers for bulk refresh
            if (updatedCouriers.Any())
            {
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
    /// Authenticates a courier by verifying their ID and password.
    /// Returns the courier's administrator role upon successful authentication.
    /// </summary>
    /// <param name="Id">The unique identifier of the courier attempting to log in.</param>
    /// <param name="password">The plain-text password to verify (should be compared against a stored hash in production).</param>
    /// <returns>The administrator role associated with the authenticated courier.</returns>
    /// <exception cref="BO.BLInvalidInputException">Thrown if the ID is 0 or the password is incorrect.</exception>
    /// <exception cref="BO.BLNotFoundException">Thrown if no courier with the specified ID exists.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static BO.Administrator Login(int Id, string password)
    {
        try
        {
            // Validate that a non-zero ID was provided
            if (Id != 0)
            {
                // Search for the courier with the given ID
                var courier = s_dal.Courier.ReadAll()
                    .FirstOrDefault(c => c.Id == Id);
                
                // Verify the courier exists
                if (courier == null)
                    throw new BO.BLNotFoundException("User with this Id not found.");
                
                // Verify the password matches the stored credential
                if (courier.Password != password)
                    throw new BO.BLInvalidInputException("Wrong password.");
                
                // Return the courier's administrator role
                return (BO.Administrator)courier.Administrator;
            }
            else
            {
                // ID cannot be zero
                throw new BO.BLInvalidInputException("ID cant be 0");
            }
        }
        catch (Exception ex)
        {
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException) throw;
            
            // Map Data Access Layer exceptions to Business Logic Layer exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Retrieves a filtered list of couriers with performance metrics (on-time vs. late deliveries).
    /// Applies optional filters by active status and administrator role or transport type.
    /// Calculates on-time and late delivery counts based on expected delivery times derived from transport speeds.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this list (must exist in the system).</param>
    /// <param name="isActive">Optional filter: if provided, only couriers with matching active status are returned.</param>
    /// <param name="Filter">Optional filter: can be an Administrator role or DeliveryTransport type to filter couriers.</param>
    /// <returns>An enumerable collection of CourierInList objects containing summary information and delivery statistics.</returns>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester ID does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, Enum? Filter)
    {
        try
        {
            // Validate that the requester exists in the system
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("requesterId doesn't exist");

            // Start with all couriers
            IEnumerable<DO.Courier> couriers = s_dal.Courier.ReadAll();

            // Apply active status filter if provided
            if (isActive != null)
                couriers = couriers.Where(c => c.IsActive == isActive);

            // Apply administrator role or transport type filter if provided
            if (Filter != null)
            {
                // Filter by administrator role (Director, Courier, Customer)
                if(Filter is BO.Administrator adminStatus)
                    couriers = couriers.Where(c => (BO.Administrator)c.Administrator == adminStatus);

                // Filter by transport method (Motorcycle, Bike, Car, Foot)
                else if(Filter is BO.DeliveryTransport transportType)
                    couriers = couriers.Where(c => (BO.DeliveryTransport)c.Transport == transportType);
            }

            // Retrieve all deliveries and configuration for performance calculations using cached data
            var allDeliveries = GetDeliveries();
            var config = AdminManager.GetConfig();

            /// <summary>
            /// Local helper function to retrieve the speed for a given transport method based on system configuration.
            /// </summary>
            /// <param name="transport">The delivery transport method.</param>
            /// <returns>The speed in km/h for the specified transport method.</returns>
            double GetSpeed(DO.DeliveryTransport transport)
                => transport switch
                {
                    DO.DeliveryTransport.Motorcycle => config.MotorcycleSpeed,
                    DO.DeliveryTransport.Bike => config.BikeSpeed,
                    DO.DeliveryTransport.Foot => config.WalkingSpeed,
                    _ => config.CarSpeed,
                };

            // Project couriers to CourierInList with calculated performance metrics
            return couriers.Select(c =>
            {
                // Get all deliveries handled by this courier
                var courierDeliveries = allDeliveries.Where(d => d.CourierId == c.Id);

                // Initialize counters for on-time and late deliveries
                int onTime = 0;
                int late = 0;

                // Iterate through deliveries to calculate performance
                foreach (var d in courierDeliveries)
                {
                    // Skip deliveries without arrival time or distance data
                    if (d.ArrivalTime == null || d.Distance == null)
                        continue;

                    // Calculate expected arrival time based on distance and transport speed
                    double speed = GetSpeed(d.Transport);
                    DateTime expected = d.PickupTime.AddHours(d.Distance.Value / (speed > 0 ? speed : config.CarSpeed));

                    // Determine if delivery was on time or late
                    if (Tools.IsDeliveryOnTime(d, expected))
                        onTime++;
                    else
                        late++;
                }

                // Build and return the list view model for this courier
                return new CourierInList
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsActive = c.IsActive,
                    Transport = (BO.DeliveryTransport)c.Transport,
                    StartDate = c.StartDate,
                    NumberOfOnTimeDeliveries = onTime,
                    NumberOfLateDeliveries = late,
                    // Get the first undelivered order (where ArrivalTime is still null)
                    ActualOrder = courierDeliveries.FirstOrDefault(d => d.ArrivalTime == null)?.OrderId
                };
            });
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
    /// Retrieves detailed information about a specific courier, including their delivery performance statistics
    /// and current order assignment if any.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this information (must exist in the system).</param>
    /// <param name="courierId">ID of the courier whose details are being requested.</param>
    /// <returns>A Courier object containing full details, performance metrics, and current order information.</returns>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester or courier ID does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static async Task<BO.Courier> GetCourierDetailsAsync(int requesterId, int courierId)
    {
        try
        {
            // -------------------------
            // Basic validation
            // -------------------------
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester ID does not exist.");

            var courierDO = s_dal.Courier.Read(courierId);
            if (courierDO == null)
                throw new BLNotFoundException("Courier ID does not exist.");

            // All deliveries handled by this courier (cached data)
            var courierDeliveries = GetDeliveries().Where(d => d.CourierId == courierId);

            int onTime = 0;
            int late = 0;

            // -------------------------
            // Performance stats (on time / late)
            // -------------------------
            foreach (var d in courierDeliveries)
            {
                // If we don't have the required data, skip this delivery for performance stats
                if (d.ArrivalTime == null || d.Distance == null)
                    continue;

                // Centralized ETA computation (no approximation here)
                DateTime expected = Tools.EstimateArrival(d.PickupTime, d.Transport, d.Distance.Value);

                if (Tools.IsDeliveryOnTime(d, expected))
                    onTime++;
                else
                    late++;
            }

            // -------------------------
            // Current delivery (the one that is not completed yet)
            // -------------------------
            var currentDelivery = courierDeliveries.FirstOrDefault(d => d.ArrivalTime == null);

            BO.OrderInProgress? currentOrder = null;

            if (currentDelivery != null)
            {
                var orderDO = s_dal.Order.Read(currentDelivery.OrderId);

                if (orderDO != null)
                {
                    var config = AdminManager.GetConfig();

                    // Compute order status based on all deliveries of this order (cached data)
                    var deliveriesForOrder = GetDeliveries().Where(d => d.OrderId == orderDO.Id).ToList();
                    var ordStatus = Tools.CalculateOrderStatus(deliveriesForOrder);

                    // Try to use the known distance from the current delivery; if missing, attempt to compute it
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
                            // If distance calculation fails, we keep distance = null and fall back to a default ETA
                        }
                    }

                    // Centralized ETA computation:
                    // - If distance is known, uses distance/speed
                    // - If distance is not known, returns a safe default (pickupTime + 30 minutes)
                    DateTime estimatedArrival = distance.HasValue
                        ? Tools.EstimateArrival(currentDelivery.PickupTime, currentDelivery.Transport, distance.Value)
                        : Tools.EstimateArrivalFallback(currentDelivery.PickupTime);

                    // Schedule status calculation keeps your existing logic
                    var scheduleStatus = Tools.CalculateScheduleStatus(
                        ordStatus,
                        orderDO.OrderDate,
                        distance.HasValue
                            ? Tools.CalculateEstimatedArrival(
                                orderDO.OrderDate,
                                distance.Value,
                                GetSpeed(currentDelivery.Transport, config)
                              )
                            : null,
                        orderDO.OrderDate.Add(config.MaxDeliveryTime),
                        currentDelivery.ArrivalTime);

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

            // -------------------------
            // Build and return the courier view model
            // -------------------------
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
            // Re-throw BL exceptions as-is
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
    /// Returns the travel speed (km/h) for the given transport method, based on system configuration.
    /// </summary>
    private static double GetSpeed(DO.DeliveryTransport transport, BO.Config config)
        => transport switch
        {
            DO.DeliveryTransport.Motorcycle => config.MotorcycleSpeed,
            DO.DeliveryTransport.Bike => config.BikeSpeed,
            DO.DeliveryTransport.Foot => config.WalkingSpeed,
            _ => config.CarSpeed,
        };


    /// <summary>
    /// Updates an existing courier's information with new values.
    /// Validates that both the requester and the courier being updated exist before applying changes.
    /// Notifies observers of the modification.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this update (must exist in the system).</param>
    /// <param name="updatedCourier">The courier object containing the updated information to persist.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester or courier ID does not exist.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void UpdateCourier(int requesterId, BO.Courier updatedCourier)
    {
        try
        {
            // Validate that the requester exists in the system
            try
            {
                var requester = s_dal.Courier.Read(requesterId);
            }
            catch (Exception)
            {
                throw new BLNotFoundException("requesterId doesn't exist");
            }
            
            // Retrieve the existing courier and apply updates
            try
            {
                var existingCourier = s_dal.Courier.Read(updatedCourier.Id);
                if (existingCourier == null)
                    throw new BLNotFoundException($"Courier with ID {updatedCourier.Id} doesn't exist");

                // Create an updated copy using record immutability with the 'with' expression
                existingCourier = existingCourier with
                {
                    Name = updatedCourier.Name,
                    Phone = updatedCourier.Phone,
                    Email = updatedCourier.Email,
                    IsActive = updatedCourier.IsActive,
                    Transport = (DO.DeliveryTransport)updatedCourier.Transport,
                    Administrator = (DO.Administrator)updatedCourier.Administrator,
                    Password = updatedCourier.Password,
                    // MaxDistance may be nullable on both sides
                    MaxDistance = updatedCourier.MaxDistance
                };

                // Persist the updated courier to the data layer
                s_dal.Courier.Update(existingCourier);
                
                // Notify subscribers of both item-specific and list-level changes
                Observers.NotifyItemUpdated(updatedCourier.Id);
                Observers.NotifyListUpdated();
            }
            catch (Exception ex)
            {
                throw new BLNotFoundException($"courierId with id : {updatedCourier.Id} doesn't exist", ex);
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
    /// Promotes a courier to the Director administrator role.
    /// Only authorized personnel (current Directors) can perform this promotion.
    /// Validates requester authorization before applying the promotion.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting this promotion (must be a Director).</param>
    /// <param name="courierId">ID of the courier to be promoted to Director.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester or courier ID does not exist.</exception>
    /// <exception cref="BO.BLUnauthorizedException">Thrown if the requester is not a Director and thus lacks authorization.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void PromoteCourierToDirector(int requesterId, int courierId)
    {
        try
        {
            // Retrieve and validate the requester
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester ID does not exist.");

            // Check authorization: only Directors can promote others
            if (requester.Administrator != DO.Administrator.Director)
                throw new BO.BLUnauthorizedException("Only a Director can promote another courier.");

            // Retrieve the courier to be promoted
            var courier = s_dal.Courier.Read(courierId) ?? throw new BLNotFoundException($"Courier {courierId} not found.");

            // Create an updated copy with the Director role using record immutability
            var updated = courier with { Administrator = DO.Administrator.Director };
            
            // Persist the promotion to the data layer
            s_dal.Courier.Update(updated);
            
            // Notify subscribers of both item-specific and list-level changes
            Observers.NotifyItemUpdated(courierId);
            Observers.NotifyListUpdated();
        }
        catch (Exception ex)
        {
            // Re-throw business logic exceptions without modification
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException || ex is BO.BLUnauthorizedException) throw;
            
            // Map Data Access Layer exceptions to Business Logic Layer exceptions
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    /// <summary>
    /// Removes a courier from the system with validation checks.
    /// Prevents deletion if the courier has any associated deliveries or is currently handling an active delivery.
    /// Notifies observers of the removal and invalidates delivery cache.
    /// </summary>
    /// <param name="requesterId">ID of the user requesting the removal (must exist in the system).</param>
    /// <param name="courierId">ID of the courier to be deleted.</param>
    /// <exception cref="BO.BLNotFoundException">Thrown if the requester or courier ID does not exist.</exception>
    /// <exception cref="BO.BLInvalidOperationException">Thrown if the courier has existing deliveries or is currently handling a delivery.</exception>
    /// <exception cref="BO.BLFailedOperation">Thrown for unexpected data access layer failures.</exception>
    internal static void removeCourier(int requesterId, int courierId)
    {
        try
        {
            // Validate that the requester exists
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester ID does not exist.");

            // Retrieve the courier to be deleted
            var courier = s_dal.Courier.Read(courierId);
            if (courier == null)
                throw new BLNotFoundException($"Courier ID {courierId} does not exist.");

            // Retrieve all deliveries handled by this courier using cached data
            var deliveries = GetDeliveries().Where(d => d.CourierId == courierId);

            // Prevent deletion if the courier has any deliveries in the system
            if (deliveries.Any())
                throw new BLInvalidOperationException("This courier has handled deliveries and cannot be deleted.");

            // Prevent deletion if the courier is currently handling an active delivery (no arrival time yet)
            if (deliveries.Any(d => d.ArrivalTime == null))
                throw new BLInvalidOperationException("This courier is currently handling a delivery and cannot be deleted.");

            // Delete the courier from the data layer
            s_dal.Courier.Delete(courierId);
            
            // Invalidate delivery cache since courier relationships may have changed
            InvalidateDeliveryCache();
            
            // Notify subscribers that this item has been deleted and the list has changed
            Observers.NotifyItemUpdated(courierId);
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
    /// Simulates courier activity by:
    /// 1. For each active courier with no current delivery: probabilistically decide if they "choose" to view available orders,
    ///    then probabilistically select one to start delivery
    /// 2. For each active courier with a current delivery: if sufficient time has elapsed based on distance,
    ///    automatically mark the delivery as completed
    /// 
    /// This method is called asynchronously from the simulator thread once per second.
    /// All DAL access is protected with tight lock blocks to minimize critical section duration.
    /// Observer notifications are performed only outside locks to prevent blocking.
    /// </summary>
    /// <remarks>
    /// Implementation details per Stage 7 requirements:
    /// - Fetches all active couriers and materializes to List to avoid deferred LINQ execution during locks
    /// - Fetches all open and in-progress deliveries
    /// - Wraps each DAL operation with an individual, tight lock block
    /// - Notifications are triggered only after all locks are released
    /// - Courier availability probability: 15% (adjustable based on courier volume)
    /// - Order selection probability: 50% (courier "changes mind" sometimes)
    /// - Delivery completion probability: probabilistic based on distance and random wait time
    /// </remarks>
    internal static async Task SimulateCourierActivityAsync()
    {
        try
        {
            // Step 1: Fetch all active couriers with materialized list
            List<DO.Courier> activeCouriers;
            lock (AdminManager.BlMutex)
            {
                activeCouriers = s_dal.Courier.ReadAll()
                    .Where(c => c.IsActive)
                    .ToList();
            }

            if (activeCouriers.Count == 0)
                return;

            // Step 2: Fetch all deliveries (open and closed)
            List<DO.Delivery> allDeliveries;
            lock (AdminManager.BlMutex)
            {
                allDeliveries = s_dal.Delivery.ReadAll().ToList();
            }

            var config = AdminManager.GetConfig();
            var now = AdminManager.Now;
            Random random = new();
            bool notificationNeeded = false;
            var updatedCourierIds = new HashSet<int>();
            var updatedOrderIds = new HashSet<int>();

            // Step 3: Process each active courier
            foreach (var courier in activeCouriers)
            {
                // Check if this courier has an in-progress delivery (ArrivalTime == null)
                var courierInProgressDeliveries = allDeliveries
                    .Where(d => d.CourierId == courier.Id && d.ArrivalTime == null)
                    .ToList();

                if (courierInProgressDeliveries.Count > 0)
                {
                    HandleDeliveryCompletion(courier, courierInProgressDeliveries.First(), 
                        config, now, random, ref notificationNeeded, updatedCourierIds, updatedOrderIds);
                }
                else
                {
                    var result = await HandleCourierOrderSelectionAsync(courier, config, now, random)
                        .ConfigureAwait(false);
                    if (result.notificationNeeded)
                    {
                        notificationNeeded = true;
                        foreach (var id in result.updatedCourierIds)
                            updatedCourierIds.Add(id);
                        foreach (var id in result.updatedOrderIds)
                            updatedOrderIds.Add(id);
                    }
                }
            }

            // Step 4: Trigger notifications outside of locks
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
            // Log simulation errors but don't crash the simulator thread
            System.Diagnostics.Debug.WriteLine($"SimulateCourierActivity failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the case where a courier has no active delivery.
    /// Decides probabilistically if the courier "chooses" to view available orders,
    /// then probabilistically selects one to start delivery if available.
    /// </summary>
    private static async Task<(bool notificationNeeded, HashSet<int> updatedCourierIds, HashSet<int> updatedOrderIds)> HandleCourierOrderSelectionAsync(
        DO.Courier courier,
        BO.Config config,
        DateTime now,
            Random random)
    {
        const double AVAILABILITY_PROBABILITY = 0.15; // 15% chance courier chooses to view orders
        const double ORDER_SELECTION_PROBABILITY = 0.50; // 50% chance to actually select an order

        bool notificationNeeded = false;
        var updatedCourierIds = new HashSet<int>();
        var updatedOrderIds = new HashSet<int>();

        // Probabilistically decide if this courier wants to view available orders
        if (random.NextDouble() > AVAILABILITY_PROBABILITY)
            return (notificationNeeded, updatedCourierIds, updatedOrderIds);

        try
        {
            var availableOrders = (await OrderManager.GetOpenOrdersForCourierAsync(
                config.BossId, courier.Id, null, null).ConfigureAwait(false)).ToList();

            if (availableOrders.Count == 0)
                return (notificationNeeded, updatedCourierIds, updatedOrderIds);

            if (random.NextDouble() > ORDER_SELECTION_PROBABILITY)
                return (notificationNeeded, updatedCourierIds, updatedOrderIds);

            var selectedOrder = availableOrders[random.Next(availableOrders.Count)];

            lock (AdminManager.BlMutex)
            {
                try
                {
                    var orderToAssign = s_dal.Order.Read(selectedOrder.OrderId);
                    if (orderToAssign == null)
                        return (notificationNeeded, updatedCourierIds, updatedOrderIds);

                    var orderDeliveries = s_dal.Delivery.ReadAll()
                        .Where(d => d.OrderId == selectedOrder.OrderId && d.ArrivalTime == null)
                        .ToList();

                    if (orderDeliveries.Any(d => d.CourierId != 0))
                        return (notificationNeeded, updatedCourierIds, updatedOrderIds);

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
                    notificationNeeded = true;
                    updatedCourierIds.Add(courier.Id);
                    updatedOrderIds.Add(selectedOrder.OrderId);
                }
                catch
                {
                    // Silently fail
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error selecting order for courier {courier.Id}: {ex.Message}");
        }

        return (notificationNeeded, updatedCourierIds, updatedOrderIds);
    }

    /// <summary>
    /// Handles the case where a courier has an active delivery.
    /// Decides if sufficient time has elapsed and marks the delivery as complete if so.
    /// If insufficient time has elapsed, may probabilistically cancel the delivery.
    /// Time calculation considers distance traveled and adds a random waiting period.
    /// </summary>
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
        const double CANCELLATION_PROBABILITY = 0.10; // 10% chance to cancel if insufficient time elapsed

        try
        {
            // Fetch the associated order to get distance info
            DO.Order? order;
            lock (AdminManager.BlMutex)
            {
                order = s_dal.Order.Read(delivery.OrderId);
            }

            if (order == null)
                return;

            // Calculate the distance from company to delivery address
            double distance = 0;
            if (order.Latitude.HasValue && order.Longitude.HasValue && 
                (order.Latitude != 0 || order.Longitude != 0))
            {
                distance = Tools.BirdDistance(
                    config.CompanyLatitude,
                    config.CompanyLongitude,
                    order.Latitude.Value,
                    order.Longitude.Value);
            }

            // Calculate estimated travel time based on courier transport method
            double speed = Tools.GetSpeed(delivery.Transport, config);
            double travelTimeHours = speed > 0 ? distance / speed : 0.5; // Default 30 min if speed is 0
            TimeSpan travelTime = TimeSpan.FromHours(travelTimeHours);

            // Add random "service time" at delivery location (1-5 minutes)
            int serviceTimeMinutes = random.Next(1, 6);
            TimeSpan serviceTime = TimeSpan.FromMinutes(serviceTimeMinutes);

            // Total expected time from pickup to completion
            TimeSpan totalExpectedTime = travelTime + serviceTime;

            // Calculate actual elapsed time since pickup
            TimeSpan elapsedTime = now - delivery.PickupTime;

            // If sufficient time has elapsed, complete the delivery
            if (elapsedTime >= totalExpectedTime)
            {
                // Randomly decide the delivery status (mostly Delivered, occasionally other statuses)
                BO.DeliveredStatus deliveryStatus;
                double statusRoll = random.NextDouble();
                deliveryStatus = statusRoll < 0.85 ? BO.DeliveredStatus.Delivered : // 85% Delivered
                                 statusRoll < 0.92 ? BO.DeliveredStatus.Rejected :  // 7% Rejected
                                 statusRoll < 0.98 ? BO.DeliveredStatus.Absent :    // 6% Absent
                                 BO.DeliveredStatus.Failed;                         // 2% Failed

                lock (AdminManager.BlMutex)
                {
                    try
                    {
                        var updatedDelivery = delivery with
                        {
                            ArrivalTime = now,
                            DeliveredStatus = (DO.DeliveredStatus)deliveryStatus
                        };

                        s_dal.Delivery.Update(updatedDelivery);
                        notificationNeeded = true;
                        updatedCourierIds.Add(courier.Id);
                        updatedOrderIds.Add(delivery.OrderId);
                    }
                    catch
                    {
                        // Silently fail - delivery may have been updated by the courier directly
                    }
                }
            }
            // If insufficient time has elapsed, probabilistically cancel the delivery
            else if (random.NextDouble() < CANCELLATION_PROBABILITY)
            {
                lock (AdminManager.BlMutex)
                {
                    try
                    {
                        // Cancel the delivery with status Canceled (simulating admin cancellation)
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
                        // Silently fail - delivery may have been updated by the courier directly
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
