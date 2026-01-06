namespace BLImplementation;

using BlApi;
using BO;
using Helpers;
using System.Collections.Generic;

internal class CourierImplementation : ICourier
{
    public void AddObserver(Action listObserver) =>
        CourierManager.Observers.AddListObserver(listObserver); //stage 5
    public void AddObserver(int id, Action observer) =>
        CourierManager.Observers.AddObserver(id, observer); //stage 5
    public void RemoveObserver(Action listObserver) =>
        CourierManager.Observers.RemoveListObserver(listObserver); //stage 5
    public void RemoveObserver(int id, Action observer) =>
        CourierManager.Observers.RemoveObserver(id, observer); //stage 5

    public void addCourier(int requesterId, Courier newCourier)
        => CourierManager.addCourier(requesterId, newCourier);

    public Courier GetCourierDetails(int requesterId, int courierId)
        => CourierManager.GetCourierDetails(requesterId, courierId);

    public IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, DeliveryTransport? status)
        => CourierManager.GetCouriersList(requesterId, isActive, status);

    public BO.Administrator Login(int Id, string password)
        => CourierManager.Login(Id, password);

    public void removeCourier(int requesterId, int courierId)
        => CourierManager.removeCourier(requesterId, courierId);

    public void UpdateCourier(int requesterId, Courier updatedCourier)
        => CourierManager.UpdateCourier(requesterId, updatedCourier);

    public void PromoteToDirector(int requesterId, int courierId)
        => CourierManager.PromoteCourierToDirector(requesterId, courierId);
}