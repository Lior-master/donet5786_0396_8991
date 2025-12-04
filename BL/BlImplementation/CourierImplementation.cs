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
    {
        throw new NotImplementedException();
    }

    public IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, DeliveryTransport? status)
    {
        throw new NotImplementedException();
    }

    public Courier Login(string username, string password)
        => CourierManager.Login(username, password);

    public void removeCourier(int requesterId, int courierId)
    {
        throw new NotImplementedException();
    }

    public void UpdateCourier(int requesterId, Courier updatedCourier)
    {
        throw new NotImplementedException();
    }
}