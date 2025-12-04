namespace BLImplementation;

using BLApi;
using BO;
using Helpers;
using System.Collections.Generic;

internal class CourierImplementation : ICourier
{
    public void addCourier(int requesterId, Courier newCourier)
    {
        throw new NotImplementedException();
    }

    public Courier GetCourierDetails(int requesterId, int courierId)
        => CourierManager.GetCourierDetails(requesterId, courierId);

    public IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, DeliveryTransport? status)
        => CourierManager.GetCouriersList(requesterId, isActive, status);

    public BO.Administrator Login(string username, string password)
        => CourierManager.Login(username, password);

    public void removeCourier(int requesterId, int courierId)
        => CourierManager.removeCourier(requesterId, courierId);

    public void UpdateCourier(int requesterId, Courier updatedCourier)
        => CourierManager.UpdateCourier(requesterId, updatedCourier);
}