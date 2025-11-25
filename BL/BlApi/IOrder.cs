namespace BLApi
{
    public interface IOrder
    {
        int Create(BO.Order order);
        BO.Order Read(int id);
        IEnumerable<BO.OrderInList> ReadAll();
        void Update(BO.Order order);
        void Delete(int id);

        IEnumerable<BO.OpenOrderInList> GetOpenOrdersForCourier(int courierId);
        void AssignOrderToCourier(int courierId, int orderId);

        void FinishDelivery(int courierId, int orderId, BO.DeliveredStatus status);

        IEnumerable<BO.ClosedDeliveryInList> GetDeliveryHistory(int courierId);

        BO.OrderInProgress? GetCurrentOrderForCourier(int courierId);
    }
}
