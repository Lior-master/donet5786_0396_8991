namespace DalApi;

public interface IDal
{
    IOrder Order { get; }
    ICourier Courier { get; }
    IDelivery Delivery { get; }

    void ResetDB();
}
