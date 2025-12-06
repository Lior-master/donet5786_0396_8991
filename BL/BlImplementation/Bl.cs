using BlApi;
using BLApi;
using BLImplementation;

namespace BlImplementation;

internal class Bl : IBl
{
    public IAdmin Admin => new AdminImplementation();

    public ICourier Courier => new CourierImplementation();

    public IOrder Order => new OrderImplementation();
}
