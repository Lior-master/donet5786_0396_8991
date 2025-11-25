namespace BLImplementation;

using BLApi;
using BO;
using Helpers;

internal class CourierImplementation : ICourier
{
    public void Create(Courier courier)
        => CourierManager.Create(courier);

    public Courier Read(int id)
        => CourierManager.Read(id);

    public IEnumerable<CourierInList> ReadAll()
        => CourierManager.ReadAll();

    public void Update(Courier courier)
        => CourierManager.Update(courier);

    public void Delete(int id)
        => CourierManager.Delete(id);

    public IEnumerable<OpenOrderInList> GetEligibleOrders(int courierId)
        => CourierManager.GetEligibleOrders(courierId);

    public void TakeOrder(int courierId, int orderId)
        => CourierManager.TakeOrder(courierId, orderId);

    public OrderInProgress? CurrentOrder(int courierId)
        => CourierManager.GetCurrentOrder(courierId);

    public IEnumerable<ClosedDeliveryInList> GetHistory(int courierId)
        => CourierManager.GetHistory(courierId);
}
