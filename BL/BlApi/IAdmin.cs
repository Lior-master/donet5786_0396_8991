namespace BLApi;

using System.Threading.Tasks;

/// <summary>
/// Defines the contract for admin operations.
/// </summary>
public interface IAdmin
{
    /// <summary>
    /// Asynchronously resets the database.
    /// </summary>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task ResetDBAsync();                          // Reset all DB
    /// <summary>
    /// Asynchronously initializes the database.
    /// </summary>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task InitializeDBAsync();                     // Reset with initialize DB
    /// <summary>
    /// Gets the clock value.
    /// </summary>
    /// <returns>The operation result.</returns>
    DateTime GetClock();                          // Read the clock
    /// <summary>
    /// Advances the clock.
    /// </summary>
    /// <param name="unit">The unit value.</param>
    void ForwardClock(BO.TimeUnit unit);          // to forward the clock
    /// <summary>
    /// Gets the config value.
    /// </summary>
    /// <returns>The operation result.</returns>
    BO.Config GetConfig();                        // Read the config
    /// <summary>
    /// Asynchronously sets the config value.
    /// </summary>
    /// <param name="config">The config value.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>
    /// This method is asynchronous to avoid blocking callers and to await required I/O or long-running work. The returned task completes once the awaited operations finish.
    /// </remarks>
    Task SetConfigAsync(BO.Config config);        // to update the config
    /// <summary>
    /// Adds the config observer.
    /// </summary>
    /// <param name="configObserver">The config observer value.</param>
    void AddConfigObserver(Action configObserver);
    /// <summary>
    /// Removes the config observer.
    /// </summary>
    /// <param name="configObserver">The config observer value.</param>
    void RemoveConfigObserver(Action configObserver);
    /// <summary>
    /// Adds the clock observer.
    /// </summary>
    /// <param name="clockObserver">The clock observer value.</param>
    void AddClockObserver(Action clockObserver);
    /// <summary>
    /// Removes the clock observer.
    /// </summary>
    /// <param name="clockObserver">The clock observer value.</param>
    void RemoveClockObserver(Action clockObserver);
    /// <summary>
    /// Starts the simulator.
    /// </summary>
    /// <param name="interval">The interval value.</param>
    void StartSimulator(double interval);             // stage 7
    /// <summary>
    /// Stops the simulator.
    /// </summary>
    void StopSimulator();                          // stage 7
}
