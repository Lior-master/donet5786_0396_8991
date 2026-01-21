using BLApi;

/// <summary>
/// Defines public business-logic abstractions and contracts used by the presentation layer.
/// </summary>
namespace BlApi;
/// <summary>
/// Defines the contract for bl operations.
/// </summary>
public interface IBl
{
    /// <summary>
    /// Gets or sets the courier value.
    /// </summary>
    ICourier Courier { get; }
    /// <summary>
    /// Gets or sets the order value.
    /// </summary>
    IOrder Order { get; }
    /// <summary>
    /// Gets or sets the admin value.
    /// </summary>
    IAdmin Admin { get; }
}
