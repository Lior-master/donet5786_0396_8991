using BLApi;

namespace BlApi;
public interface IBl
{
    ICourier Student { get; }
    IOrder Course { get; }
    IAdmin Admin { get; }
}
