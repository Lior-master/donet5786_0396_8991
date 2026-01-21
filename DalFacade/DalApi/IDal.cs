namespace DalApi;

/// <summary>
/// Defines the contract for dal operations.
/// </summary>
public interface IDal
{
    /// <summary>
    /// Gets or sets the order value.
    /// </summary>
    IOrder Order { get; }
    /// <summary>
    /// Gets or sets the courier value.
    /// </summary>
    ICourier Courier { get; }
    /// <summary>
    /// Gets or sets the delivery value.
    /// </summary>
    IDelivery Delivery { get; }
    /// <summary>
    /// Gets or sets the config value.
    /// </summary>
    IConfig Config { get; }  

    /// <summary>
    /// Resets the database.
    /// </summary>
    void ResetDB();
}
