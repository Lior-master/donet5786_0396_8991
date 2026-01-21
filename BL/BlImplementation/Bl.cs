using BlApi;
using BLApi;
using BLImplementation;

/// <summary>
/// Implements business-logic interfaces and orchestrates DAL interactions.
/// </summary>
namespace BlImplementation;

/// <summary>
/// Represents the bl component in this layer.
/// </summary>
internal class Bl : IBl
{
    /// <summary>
    /// Admin Implementation.
    /// </summary>
    public IAdmin Admin => new AdminImplementation();

    /// <summary>
    /// Courier Implementation.
    /// </summary>
    public ICourier Courier => new CourierImplementation();

    /// <summary>
    /// Order Implementation.
    /// </summary>
    public IOrder Order => new OrderImplementation();
}
