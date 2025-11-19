
namespace Dal;
using DalApi;
sealed internal class DalList : IDal
{
    public static IDal Instance { get;  } = new DalList();

    private DalList() { }
    public IOrder Order { get; } = new OrderImplementation();

    public ICourier Courier { get; } = new CourierImplementation();

    public IDelivery Delivery { get; } = new DeliveryImplementation();
    public IConfig Config { get; } = new ConfigImplementation();

    public void ResetDB()
    {
        Order.DeleteAll();
        Courier.DeleteAll();
        Delivery.DeleteAll();
        Config.Reset();
    }
}
