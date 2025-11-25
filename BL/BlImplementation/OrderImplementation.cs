using BLApi;
using BO;

namespace BlImplementation
{
    internal class OrderImplementation : IOrder
    {
        public void AssignOrderToCourier(int courierId, int orderId)
        {
            throw new NotImplementedException();
        }

        public int Create(Order order)
        {
            throw new NotImplementedException();
        }

        public void Delete(int id)
        {
            throw new NotImplementedException();
        }

        public void FinishDelivery(int courierId, int orderId, DeliveredStatus status)
        {
            throw new NotImplementedException();
        }

        public OrderInProgress? GetCurrentOrderForCourier(int courierId)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ClosedDeliveryInList> GetDeliveryHistory(int courierId)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<OpenOrderInList> GetOpenOrdersForCourier(int courierId)
        {
            throw new NotImplementedException();
        }

        public Order Read(int id)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<OrderInList> ReadAll()
        {
            throw new NotImplementedException();
        }

        public void Update(Order order)
        {
            throw new NotImplementedException();
        }
    }
}
