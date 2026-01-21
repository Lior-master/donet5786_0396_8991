namespace BLImplementation;

using BLApi;
using BO;
using Helpers;
using System.Threading.Tasks;

/// <summary>
/// Represents the admin implementation component in this layer.
/// for managing the database, system clock, and configuration settings.
/// </summary>
/// <remarks>
/// This class acts as a wrapper around the <see cref="AdminManager"/> static class, delegating
/// all operations to it while providing a clean interface for the presentation layer.
/// </remarks>
internal class AdminImplementation : IAdmin
{
    /// <summary>
    /// Asynchronously resets the database.
    /// </summary>
    public Task ResetDBAsync()
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        return AdminManager.ResetDBAsync();
    }

    /// <summary>
    /// Asynchronously initializes the database.
    /// </summary>
    public Task InitializeDBAsync()
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        return AdminManager.InitializeDBAsync();
    }

    /// <summary>
    /// Gets the clock value.
    /// </summary>
    /// <returns>The current date and time from the system clock.</returns>
    public DateTime GetClock()
        => AdminManager.Now;

    /// <summary>
    /// Advances the clock.
    /// </summary>
    /// <param name="unit">The time unit by which to advance the clock (Second, Minute, Hour, Day, Month, or Year).</param>
    /// <remarks>
    /// Each call advances the clock by exactly one unit of the specified type.
    /// If an unknown time unit is provided, the clock remains unchanged.
    /// </remarks>
    public void ForwardClock(TimeUnit unit)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        
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
    /// Gets the config value.
    /// </summary>
    /// <returns>A <see cref="Config"/> object containing all current configuration values.</returns>
    public Config GetConfig()
        => AdminManager.GetConfig();

    /// <summary>
    /// Asynchronously sets the config value.
    /// </summary>
    /// <param name="configuration">The new configuration settings to apply.</param>
    public Task SetConfigAsync(Config configuration)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        return AdminManager.SetConfigAsync(configuration);
    }

    /// <summary>
    /// Adds the clock observer.
    /// </summary>
    /// <param name="clockObserver">An action to invoke when the clock is advanced.</param>
    /// <remarks>
    /// Multiple observers can be registered. They will be invoked in the order they were added.
    /// </remarks>
    public void AddClockObserver(Action clockObserver)
        => AdminManager.ClockUpdatedObservers += clockObserver;

    /// <summary>
    /// Removes the clock observer.
    /// </summary>
    /// <param name="clockObserver">The observer action to remove.</param>
    /// <remarks>
    /// If the observer is not currently registered, this method has no effect.
    /// </remarks>
    public void RemoveClockObserver(Action clockObserver)
        => AdminManager.ClockUpdatedObservers -= clockObserver;

    /// <summary>
    /// Adds the config observer.
    /// </summary>
    /// <param name="configObserver">An action to invoke when the configuration changes.</param>
    /// <remarks>
    /// Multiple observers can be registered. They will be invoked in the order they were added.
    /// </remarks>
    public void AddConfigObserver(Action configObserver)
        => AdminManager.ConfigUpdatedObservers += configObserver;

    /// <summary>
    /// Removes the config observer.
    /// </summary>
    /// <param name="configObserver">The observer action to remove.</param>
    /// <remarks>
    /// If the observer is not currently registered, this method has no effect.
    /// </remarks>
    public void RemoveConfigObserver(Action configObserver)
        => AdminManager.ConfigUpdatedObservers -= configObserver;

    
   

    /// <summary>
    /// Starts the simulator.
    /// </summary>
    /// <param name="interval">The interval in minutes by which to advance the clock per second.</param>
    /// <remarks>
    /// If the simulator is already running, throws <see cref="BO.BLTemporaryNotAvailableException"/>.
    /// </remarks>
    public void StartSimulator(double interval)  // stage 7
    {
        AdminManager.ThrowOnSimulatorIsRunning();  // stage 7
        AdminManager.Start(interval);              // stage 7
    }

    /// <summary>
    /// Stops the simulator.
    /// </summary>
    /// <remarks>
    /// Has no effect if the simulator is already stopped.
    /// </remarks>
    public void StopSimulator()                    // stage 7
        => AdminManager.Stop();                    // stage 7
}
