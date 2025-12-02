namespace BLApi;

public interface ICourier
{
    void Create(BO.Courier courier);
    BO.Courier Read(int id);
    IEnumerable<BO.CourierInList> ReadAll();
    void Update(BO.Courier courier);
    void Delete(int id);

    IEnumerable<BO.OpenOrderInList> GetEligibleOrders(int courierId);
    void TakeOrder(int courierId, int orderId);
    BO.OrderInProgress? CurrentOrder(int courierId);
    IEnumerable<BO.ClosedDeliveryInList> GetHistory(int courierId);
}
