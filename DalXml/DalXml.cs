using DalApi;

namespace Dal;

sealed internal class DalXml : IDal
{
    public static IDal Instance { get;  } = new DalXml();

    private DalXml() { }
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
