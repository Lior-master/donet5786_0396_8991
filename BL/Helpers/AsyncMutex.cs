/// <summary>
/// Provides cross-cutting helper utilities for synchronization and tooling.
/// </summary>
namespace Helpers;

/// <summary>
/// Provides a lightweight, non-blocking guard for async refresh loops.
/// </summary>
/// <remarks>
/// This helper avoids overlapping periodic or observer-driven async work by using
/// <see cref="Interlocked"/> operations instead of a blocking <c>lock</c>. It is used
/// to skip concurrent runs and reset state once a run completes, keeping UI and timers
/// responsive during stage 7 simulations.
/// </remarks>
internal class AsyncMutex
{
    // Interlocked works with int, not bool. 
    // 0 = false (not in progress), 1 = true (in progress)
    /// <summary>
    /// Stores whether a protected operation is currently running.
    /// </summary>
    private int _inProgress = 0;
    // Atomically sets _inProgress to 1 only if it is currently 0.
    // CompareExchange returns the ORIGINAL value:
    // - If it returns 1: It was already in progress -> return true.
    // - If it returns 0: It was free (we just acquired it) -> return false.
    /// <summary>
    /// Checks whether work is already running and marks the operation as in progress.
    /// </summary>
    /// <returns>
    /// <c>true</c> if an operation was already running and the caller should skip; otherwise
    /// <c>false</c> after successfully claiming the in-progress flag.
    /// </returns>
    internal bool CheckAndSetInProgress() => Interlocked.CompareExchange(ref _inProgress, 1, 0) == 1;
    // Atomically resets the state to 0 (false).
    /// <summary>
    /// Clears the in-progress flag once the protected operation completes.
    /// </summary>
    internal void UnsetInProgress() => Interlocked.Exchange(ref _inProgress, 0);
}
