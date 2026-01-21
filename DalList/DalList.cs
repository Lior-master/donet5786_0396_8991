
/// <summary>
/// Defines types for this application layer.
/// </summary>
namespace Dal;
using DalApi;
sealed internal class DalList : IDal
{
    public static IDal Instance { get;  } = new DalList();

    private DalList() { }
    /// <summary>
    /// Order Implementation.
    /// </summary>
    public IOrder Order { get; } = new OrderImplementation();

    /// <summary>
    /// Courier Implementation.
    /// </summary>
    public ICourier Courier { get; } = new CourierImplementation();

    /// <summary>
    /// Delivery Implementation.
    /// </summary>
    public IDelivery Delivery { get; } = new DeliveryImplementation();
    /// <summary>
    /// Config Implementation.
    /// </summary>
    public IConfig Config { get; } = new ConfigImplementation();

    /// <summary>
    /// Resets the database.
    /// </summary>
    public void ResetDB()
    {
        Order.DeleteAll();
        Courier.DeleteAll();
        Delivery.DeleteAll();
        Config.Reset();
    }
}
