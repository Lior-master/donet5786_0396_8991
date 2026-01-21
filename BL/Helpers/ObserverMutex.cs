/// <summary>
/// Provides cross-cutting helper utilities for synchronization and tooling.
/// </summary>
namespace Helpers;

/// <summary>
/// No.
/// It ensures that only one load runs at a time,
/// and tracks whether a restart is required if another request arrives during the load.
/// 
/// Usage Pattern:
/// 1. Call CheckAndSetLoadInProgressOrRestartRequired() at method start
/// 2. If returns true: exit (load already in progress)
/// 3. If returns false: proceed with Dispatcher.BeginInvoke(async () => { ... })
/// 4. Inside async block: after refresh logic, call UnsetLoadInProgressAndCheckRestartRequested()
/// 5. If returns true: delay 100ms then rerun observer method
/// 
/// Thread-Safe: Yes (uses lock for state management)
/// Blocking: No (callers check flag; no wait needed)
/// </summary>
public class ObserverMutex
{
    /// <summary>
    /// Delay added after a load finishes to avoid too many requests in a short time.
    /// Prevents UI flooding from rapid successive notifications.
    /// </summary>
    private const int DELAY_MILLISECONDS = 100;

    /// <summary>
    /// Indicates whether a load operation is currently running.
    /// </summary>
    private bool _isLoadInProgress = false;

    /// <summary>
    /// Indicates whether a new load request was received during an active load.
    /// Used to trigger a restart after the current load completes.
    /// </summary>
    private bool _isRestartRequested = false;

    /// <summary>
    /// Checks the and set load in progress or restart required.
    /// If not in progress: marks load as started and returns false (proceed with load).
    /// If in progress: marks restart as requested and returns true (skip; will restart later).
    /// 
    /// This method is NON-BLOCKING and should be called at the start of observer methods.
    /// </summary>
    /// <returns>
    /// True if a load is already in progress (caller should exit and wait for restart).
    /// False if this is the first load request (caller should proceed with async load).
    /// </returns>
    public bool CheckAndSetLoadInProgressOrRestartRequired()
    {
        lock (this) // Ensure atomic read-modify-write of shared state
        {
            // If a load is already running, mark that a restart is needed later
            if (_isLoadInProgress)
            {
                _isRestartRequested = true;
                return true; // Caller should exit
            }

            // No load in progress, so start one now
            _isLoadInProgress = true;
            _isRestartRequested = false; // Reset flag for this new load cycle
            return false; // Caller should proceed with load
        }
    }

    /// <summary>
    /// Unsets the load in progress and check restart requested.
    /// 
    /// This method should be called inside the async Dispatcher block, after refresh logic completes.
    /// It includes a built-in 100ms delay to throttle rapid consecutive requests.
    /// </summary>
    /// <returns>
    /// True if a restart was requested during the load (caller should delay and rerun observer).
    /// False if no restart is needed (load cycle complete, return to idle state).
    /// </returns>
    public async Task<bool> UnsetLoadInProgressAndCheckRestartRequested()
    {
        // Add delay to throttle rapid consecutive load requests and keep UI responsive
        await Task.Delay(DELAY_MILLISECONDS).ConfigureAwait(false);

        lock (this) // Ensure atomic read-modify-write of shared state
        {
            // Mark the load as completed
            _isLoadInProgress = false;
            // Return whether another load should be started
            // NB. _isRestartRequested will be reset when setting LoadInProgress next time
            return _isRestartRequested;
        }
    }
}