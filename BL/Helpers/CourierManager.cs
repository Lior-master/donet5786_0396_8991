using BO;
using DalApi;
using DO;
using System.Linq;

namespace Helpers;

internal static class CourierManager
{
    private static readonly IDal s_dal = Factory.Get;

    internal static void PeriodicCouriersUpdates(DateTime oldClock, DateTime newClock)
    {
        try
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

    internal static BO.Administrator Login(string username, string password)
    {
        try
        {
            if (!string.IsNullOrEmpty(username))
            {
                var courier = s_dal.Courier.ReadAll()
                    .FirstOrDefault(c => c.Name == username);
                if (courier == null)
                    throw new BO.BLNotFoundException("User with this username not found.");
                if (courier.Password != password)
                    throw new BO.BLInvalidInputException("Wrong password.");
                return (BO.Administrator)courier.Administrator;
            }
            else
            {
                throw new BO.BLInvalidInputException("Username cant be null");
            }
        }
        catch (Exception ex)
        {
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, BO.DeliveryTransport? status)
    {
        try
        {
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("requesterId doesn't exist");

            IEnumerable<DO.Courier> couriers = s_dal.Courier.ReadAll();

            if (isActive != null)
                couriers = couriers.Where(c => c.IsActive == isActive);

            if (status != null)
                couriers = couriers.Where(c => (BO.DeliveryTransport)c.Transport == status);

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
            if (ex is BO.BLNotFoundException || ex is BO.BLInvalidInputException || ex is BO.BLAlreadyExistsException || ex is BO.BLInvalidOperationException) throw;
            if (ex is DO.DalDoesNotExistException) throw new BO.BLNotFoundException(ex.Message, ex);
            if (ex is DO.DalAlreadyExistsException) throw new BO.BLAlreadyExistsException(ex.Message, ex);
            if (ex is DO.DalFormatException) throw new BO.BLInvalidInputException(ex.Message, ex);
            if (ex is DO.DalNullReferenceException || ex is DO.DalXMLFileLoadCreateException) throw new BO.BLFailedOperation(ex.Message, ex);
            throw new BO.BLFailedOperation(ex.Message, ex);
        }
    }

    internal static BO.Courier GetCourierDetails(int requesterId, int courierId)
    {
        try
        {
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester ID does not exist.");

            var courierDO = s_dal.Courier.Read(courierId);
            if (courierDO == null)
                throw new BLNotFoundException("Courier ID does not exist.");

            var courierDeliveries = s_dal.Delivery.ReadAll().Where(d => d.CourierId == courierId);

            int onTime = 0;
            int late = 0;

            var config = AdminManager.GetConfig();

            double GetSpeed(DO.DeliveryTransport transport)
                => transport switch
                {
                    DO.DeliveryTransport.Motorcycle => config.MotorcycleSpeed,
                    DO.DeliveryTransport.Bike => config.BikeSpeed,
                    DO.DeliveryTransport.Foot => config.WalkingSpeed,
                    _ => config.CarSpeed,
                };

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

            var currentDelivery = courierDeliveries.FirstOrDefault(d => d.ArrivalTime == null);

            BO.OrderInProgress? currentOrder = null;

            if (currentDelivery != null)
            {
                var orderDO = s_dal.Order.Read(currentDelivery.OrderId);

                if (orderDO != null)
                {
                    var deliveriesForOrder = s_dal.Delivery.ReadAll(d => d.OrderId == orderDO.Id).ToList();
                    var ordStatus = Tools.CalculateOrderStatus(deliveriesForOrder);

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

    internal static void UpdateCourier(int requesterId, BO.Courier updatedCourier)
    {
        try
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
            s_dal.Courier.Update(existingCourier);
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

    internal static void removeCourier(int requesterId, int courierId)
    {
        try
        {
            var requester = s_dal.Courier.Read(requesterId);
            if (requester == null)
                throw new BLNotFoundException("Requester ID does not exist.");

            var courier = s_dal.Courier.Read(courierId);
            if (courier == null)
                throw new BLNotFoundException($"Courier ID {courierId} does not exist.");

            var deliveries = s_dal.Delivery.ReadAll()
                                           .Where(d => d.CourierId == courierId);

            if (deliveries.Any())
                throw new BLInvalidOperationException("This courier has handled deliveries and cannot be deleted.");

            if (deliveries.Any(d => d.ArrivalTime == null))
                throw new BLInvalidOperationException("This courier is currently handling a delivery and cannot be deleted.");

            s_dal.Courier.Delete(courierId);
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
