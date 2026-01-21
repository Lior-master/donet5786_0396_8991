/// <summary>
/// Defines public business-logic abstractions and contracts used by the presentation layer.
/// </summary>
namespace BlApi;

/// <summary>
/// Defines the contract for observable operations.
/// for changes in a list of entities and in a speecific entity
/// </summary>
public interface IObservable //stage 5
{
    /// <summary>
    /// Adds the observer.
    /// </summary>
    /// <param name="listObserver">the observer method to be registered</param>
    void AddObserver(Action listObserver);
    /// <summary>
    /// Adds the observer.
    /// </summary>
    /// <param name="id">the identifier of the entity instance to be observed</param>
    /// <param name="observer">the observer method to be registered</param>
    void AddObserver(int id, Action observer);
    /// <summary>
    /// Removes the observer.
    /// </summary>
    /// <param name="listObserver">the observer method to be unregistered</param>
    void RemoveObserver(Action listObserver);
    /// <summary>
    /// Removes the observer.
    /// </summary>
    /// <param name="id">the identifier of the entity instance that was observed</param>
    /// <param name="observer">the observer method to be unregistered</param>
    void RemoveObserver(int id, Action observer);
}