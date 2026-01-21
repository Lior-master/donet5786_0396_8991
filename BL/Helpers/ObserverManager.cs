/// <summary>
/// Provides cross-cutting helper utilities for synchronization and tooling.
/// </summary>
namespace Helpers;

/// <summary>
/// Logic.
/// in the Business Logic (BL) layer.
/// It offers infrastructure to support observers as follows:
/// <list type="bullet">
/// <item>an event delegate for list observers - wherever there may be a change in the
/// presentation of the list of entities</item>
/// <item>a hash table of delegates for individual entity observers - indexed by appropriate entity ID</item>
/// </list>
/// </summary>
class ObserverManager //stage 5
{
    /// <summary>
    /// event delegate for list observers - it's called whenever there may be need to update the presentation
    /// of the list of entities
    /// </summary>
    private event Action? _listObservers;
    /// <summary>
    /// Performs the operation.
    /// The index (key) is the ID of an entity.<br/>
    /// If there are no observers for a specific entity instance - there will not be entry in the hash
    /// table for it, thus providing memory effective storage for these observers
    /// </summary>
    private readonly Dictionary<int, Action?> _specificObservers = new();

    /// <summary>
    /// Adds the list observer.
    /// </summary>
    /// <param name="observer">Observer method (usually from Presentation Layer) to be added</param>
    internal void AddListObserver(Action observer) => _listObservers += observer;
    /// <summary>
    /// Removes the list observer.
    /// </summary>
    /// <param name="observer">Observer method (usually from Presentation Layer) to be removed</param>
    internal void RemoveListObserver(Action observer) => _listObservers -= observer;

    /// <summary>
    /// Adds the observer.
    /// </summary>
    /// <param name="id">the ID value for the entity instance to be observed</param>
    /// <param name="observer">Observer method (usually from Presentation Layer) to be added</param>
    internal void AddObserver(int id, Action observer)
    {
        if (_specificObservers.ContainsKey(id)) // if there are already observers for the ID
            _specificObservers[id] += observer; // add the given observer
        else // there is the first observer for the ID
            _specificObservers[id] = observer; // create hash table entry for the ID with the given observer
    }

    /// <summary>
    /// Removes the observer.
    /// </summary>
    /// <param name="id">the ID value for the observed entity instance</param>
    /// <param name="observer">Observer method (usually from Presentation Layer) to be removed</param>
    internal void RemoveObserver(int id, Action observer)
    {
        // First, lets check that there are any observers for the ID
        if (_specificObservers.ContainsKey(id) && _specificObservers[id] is not null)
        {
            Action? specificObserver = _specificObservers[id]; // Reference to the delegate element for the ID
            specificObserver -= observer; // Remove the given observer from the delegate
            if (specificObserver?.GetInvocationList().Length == 0) // if there are no more observers for the ID
                _specificObservers.Remove(id); // then remove the hash table entry for the ID
        }
    }

    /// <summary>
    /// Notify List Updated.
    /// that may affect the whole list presentation
    /// </summary>
    internal void NotifyListUpdated() => _listObservers?.Invoke();

    /// <summary>
    /// Notify Item Updated.
    /// </summary>
    /// <param name="id">a specific entity ID</param>
    internal void NotifyItemUpdated(int id)
    {
        if (_specificObservers.ContainsKey(id))
            _specificObservers[id]?.Invoke();
    }

}