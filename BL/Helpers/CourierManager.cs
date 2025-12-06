using BO;
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

    internal static BO.Administrator Login(string username,string password)
    {
        if (!string.IsNullOrEmpty(username))
        {
            var courier = s_dal.Courier.ReadAll()
                .FirstOrDefault(c => c.Name == username);
            if (courier == null)
            {
                throw new BO.BLNotFoundException("User whith this username not found.");
            }
            if (courier.Password != password)
            {
                throw new BO.BLInvalidInputException("Wrong password.");
            }
            return (BO.Administrator) courier.Administrator;
        }
        else
        {
            throw new BO.BLInvalidInputException("Username cant be null");
        }

    }

    internal static IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, BO.DeliveryTransport? status)
    {
        // Verify that the requester exists
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("requesterId doesn't exist");

        // Read all couriers
        IEnumerable<DO.Courier> couriers = s_dal.Courier.ReadAll();

        // Filter by active/inactive status
        if (isActive != null)
            couriers = couriers.Where(c => c.IsActive == isActive);

        // Filter by transport if requested
        if (status != null)
            couriers = couriers.Where(c => (BO.DeliveryTransport)c.Transport == status);

        // Read all deliveries once
        var allDeliveries = s_dal.Delivery.ReadAll();

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
            // Deliveries associated with this courier
            var courierDeliveries = allDeliveries.Where(d => d.CourierId == c.Id);

            int onTime = 0;
            int late = 0;

            foreach (var d in courierDeliveries)
            {
                // Unfinished delivery or missing distance -> ignore for stats
                if (d.ArrivalTime == null || d.Distance == null)
                    continue;

                // Calculate expected arrival time from pickup using stored distance and transport speed
                double speed = GetSpeed(d.Transport);
                DateTime expected = d.PickupTime.AddHours(d.Distance.Value / (speed > 0 ? speed : config.CarSpeed));

                // Check if the delivery is on time or late
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

                // Ongoing delivery: the one where ArrivalTime == null
                ActualOrder = courierDeliveries
                              .FirstOrDefault(d => d.ArrivalTime == null)?
                              .OrderId
            };
        });
    }

    internal static BO.Courier GetCourierDetails(int requesterId, int courierId)
    {
        // 1. Validate the requester exists
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester ID does not exist.");

        // 2. Read the requested courier
        var courierDO = s_dal.Courier.Read(courierId);
        if (courierDO == null)
            throw new BLNotFoundException("Courier ID does not exist.");

        // 3. Load all deliveries assigned to this courier
        var courierDeliveries = s_dal.Delivery.ReadAll()
                                              .Where(d => d.CourierId == courierId);

        int onTime = 0;
        int late = 0;

        // Load system configuration (replace with your own config access method if needed)
        var config = AdminManager.GetConfig();

        double GetSpeed(DO.DeliveryTransport transport)
            => transport switch
            {
                DO.DeliveryTransport.Motorcycle => config.MotorcycleSpeed,
                DO.DeliveryTransport.Bike => config.BikeSpeed,
                DO.DeliveryTransport.Foot => config.WalkingSpeed,
                _ => config.CarSpeed,
            };

        // 4. Calculate delivery performance statistics
        foreach (var d in courierDeliveries)
        {
            // Only completed deliveries can be evaluated
            if (d.ArrivalTime == null || d.Distance == null)
                continue;

            // Compute expected delivery time from pickup + distance / speed
            double speed = GetSpeed(d.Transport);
            DateTime expected = d.PickupTime.AddHours(d.Distance.Value / (speed > 0 ? speed : config.CarSpeed));

            // Compare expected vs actual arrival time
            if (Tools.IsDeliveryOnTime(d, expected))
                onTime++;
            else
                late++;
        }

        // 5. Identify the active (unfinished) delivery, if any
        var currentDelivery = courierDeliveries
                              .FirstOrDefault(d => d.ArrivalTime == null);

        BO.OrderInProgress? currentOrder = null;

        // 6. If there is a delivery in progress, load its associated order
        if (currentDelivery != null)
        {
            var orderDO = s_dal.Order.Read(currentDelivery.OrderId);

            if (orderDO != null)
            {
                // compute order status from deliveries for this order (correct mapping)
                var deliveriesForOrder = s_dal.Delivery.ReadAll(d => d.OrderId == orderDO.Id).ToList();
                var ordStatus = Tools.CalculateOrderStatus(deliveriesForOrder);

                // Create a BO.OrderInProgress object to represent the active order
                currentOrder = new BO.OrderInProgress
                {
                    OrderId = orderDO.Id,
                    CustomerName = orderDO.CustomerName,
                    CustomerAddress = orderDO.CustomerAddress,
                    CustomerPhone = orderDO.CustomerPhone,
                    PickupTime = currentDelivery.PickupTime,
                    Distance = currentDelivery.Distance,
                    OrderStatusEnum = ordStatus
                };
            }
        }

        // 7. Construct and return the BO.Courier with all required details
        return new BO.Courier
        {
            Id = courierDO.Id,
            Name = courierDO.Name,
            Phone = courierDO.Phone,
            Email = courierDO.Email,
            IsActive = courierDO.IsActive,
            Transport = (BO.DeliveryTransport)courierDO.Transport,
            StartDate = courierDO.StartDate,
            MaxDistance = courierDO.MaxDistance,
            Administator = (BO.Administrator)courierDO.Administrator,

            NumberOfOnTimeDeliveries = onTime,
            NumberOfLateDeliveries = late,
            CurrentOrder = currentOrder
        };
    }

    internal static void UpdateCourier(int requesterId, BO.Courier updatedCourier)
    {
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("requesterId doesn't exist");
        var existingCourier = s_dal.Courier.Read(updatedCourier.Id);
        if (existingCourier == null)
            throw new BLNotFoundException($"courierId with id : {updatedCourier.Id} doesn't exist");
        existingCourier = existingCourier with
        {
            Name = updatedCourier.Name,
            Phone = updatedCourier.Phone,
            Email = updatedCourier.Email,
            IsActive = updatedCourier.IsActive,
            Transport = (DO.DeliveryTransport)updatedCourier.Transport
        };
    }

    internal static void removeCourier(int requesterId, int courierId)
    {
        // Validate requester
        var requester = s_dal.Courier.Read(requesterId);
        if (requester == null)
            throw new BLNotFoundException("Requester ID does not exist.");

        // Validate courier
        var courier = s_dal.Courier.Read(courierId);
        if (courier == null)
            throw new BLNotFoundException($"Courier ID {courierId} does not exist.");

        // Load deliveries of this courier
        var deliveries = s_dal.Delivery.ReadAll()
                                       .Where(d => d.CourierId == courierId);

        // Courier cannot be deleted if he EVER handled deliveries
        if (deliveries.Any())
            throw new BLInvalidOperationException("This courier has handled deliveries and cannot be deleted.");

        // Courier cannot be deleted if he is currently handling a delivery
        if (deliveries.Any(d => d.ArrivalTime == null))
            throw new BLInvalidOperationException("This courier is currently handling a delivery and cannot be deleted.");

        // Delete if all checks pass
        s_dal.Courier.Delete(courierId);
    }
}
