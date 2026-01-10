namespace BLImplementation;

using BLApi;
using BO;
using Helpers;

/// <summary>
/// Implementation of the <see cref="IAdmin"/> interface that provides administrative operations
/// for managing the database, system clock, and configuration settings.
/// </summary>
/// <remarks>
/// This class acts as a wrapper around the <see cref="AdminManager"/> static class, delegating
/// all operations to it while providing a clean interface for the presentation layer.
/// </remarks>
internal class AdminImplementation : IAdmin
{
    /// <summary>
    /// Resets the database to its initial state, removing all data.
    /// </summary>
    public void ResetDB()
        => AdminManager.ResetDB();

    /// <summary>
    /// Initializes the database with default configuration and sample data.
    /// </summary>
    public void InitializeDB()
        => AdminManager.InitializeDB();

    /// <summary>
    /// Retrieves the current system clock value (which may be simulated time).
    /// </summary>
    /// <returns>The current date and time from the system clock.</returns>
    public DateTime GetClock()
        => AdminManager.Now;

    /// <summary>
    /// Advances the system clock by the specified time unit amount.
    /// </summary>
    /// <param name="unit">The time unit by which to advance the clock (Second, Minute, Hour, Day, Month, or Year).</param>
    /// <remarks>
    /// Each call advances the clock by exactly one unit of the specified type.
    /// If an unknown time unit is provided, the clock remains unchanged.
    /// </remarks>
    public void ForwardClock(TimeUnit unit)
    {
        // Calculate the new time based on the specified time unit
        DateTime newTime = unit switch
        {
            TimeUnit.Second => AdminManager.Now.AddSeconds(1),
            TimeUnit.Minute => AdminManager.Now.AddMinutes(1),
            TimeUnit.Hour => AdminManager.Now.AddHours(1),
            TimeUnit.Day => AdminManager.Now.AddDays(1),
            TimeUnit.Month => AdminManager.Now.AddMonths(1),
            TimeUnit.Year => AdminManager.Now.AddYears(1),
            _ => AdminManager.Now // Default case: no change to clock
        };

        // Update the global clock and notify all registered observers
        AdminManager.UpdateClock(newTime);
    }

    /// <summary>
    /// Retrieves the current system configuration settings.
    /// </summary>
    /// <returns>A <see cref="Config"/> object containing all current configuration values.</returns>
    public Config GetConfig()
        => AdminManager.GetConfig();

    /// <summary>
    /// Updates the system configuration settings and notifies all registered observers.
    /// </summary>
    /// <param name="configuration">The new configuration settings to apply.</param>
    public void SetConfig(Config configuration)
        => AdminManager.SetConfig(configuration);

    /// <summary>
    /// Registers an observer to be notified whenever the system clock is updated.
    /// </summary>
    /// <param name="clockObserver">An action to invoke when the clock is advanced.</param>
    /// <remarks>
    /// Multiple observers can be registered. They will be invoked in the order they were added.
    /// </remarks>
    public void AddClockObserver(Action clockObserver)
        => AdminManager.ClockUpdatedObservers += clockObserver;

    /// <summary>
    /// Removes a previously registered clock observer.
    /// </summary>
    /// <param name="clockObserver">The observer action to remove.</param>
    /// <remarks>
    /// If the observer is not currently registered, this method has no effect.
    /// </remarks>
    public void RemoveClockObserver(Action clockObserver)
        => AdminManager.ClockUpdatedObservers -= clockObserver;

    /// <summary>
    /// Registers an observer to be notified whenever the system configuration is updated.
    /// </summary>
    /// <param name="configObserver">An action to invoke when the configuration changes.</param>
    /// <remarks>
    /// Multiple observers can be registered. They will be invoked in the order they were added.
    /// </remarks>
    public void AddConfigObserver(Action configObserver)
        => AdminManager.ConfigUpdatedObservers += configObserver;

    /// <summary>
    /// Removes a previously registered configuration observer.
    /// </summary>
    /// <param name="configObserver">The observer action to remove.</param>
    /// <remarks>
    /// If the observer is not currently registered, this method has no effect.
    /// </remarks>
    public void RemoveConfigObserver(Action configObserver)
        => AdminManager.ConfigUpdatedObservers -= configObserver;
}
