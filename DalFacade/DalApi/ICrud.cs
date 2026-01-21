using DO;

/// <summary>
/// Defines types for this application layer.
/// </summary>
namespace DalApi;

/// <summary>
/// Defines the contract for crud operations.
/// </summary>
public interface ICrud<T> where T : class
{
    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="item">The item value.</param>
    void Create(T item);
    // Creates new entity object in DAL
    /// <summary>
    /// Read.
    /// </summary>
    /// <param name="id">The id value.</param>
    /// <returns>The operation result.</returns>
    T? Read(int id);
    // Reads entity object by its ID
    /// <summary>
    /// Read All.
    /// </summary>
    /// <param name="Func<T">The func t value.</param>
    /// <param name="null">The null value.</param>
    /// <returns>The operation result.</returns>
    IEnumerable<T> ReadAll(Func<T, bool>? filter = null);
    // stage 1 only, Reads all entity objects
    /// <summary>
    /// Updates the item.
    /// </summary>
    /// <param name="item">The item value.</param>
    void Update(T item);
    // Updates entity object
    /// <summary>
    /// Deletes the item.
    /// </summary>
    /// <param name="id">The id value.</param>
    void Delete(int id);
    // Deletes an object by its Id
    /// <summary>
    /// Deletes the all.
    /// </summary>
    void DeleteAll();
}
