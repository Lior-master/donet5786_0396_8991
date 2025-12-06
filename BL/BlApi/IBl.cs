using BLApi;

namespace BlApi;
public interface IBl
{
    ICourier Courier { get; }
    IOrder Order { get; }
    IAdmin Admin { get; }
}
