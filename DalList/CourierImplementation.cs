namespace Dal;
using DalApi;
using DO;
using System.Collections.Generic;

/// <summary>
/// Represents the courier implementation component in this layer.
/// </summary>
internal class CourierImplementation : ICourier
{
    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void Create(Courier item)
    {        
        DataSource.Couriers.Add(item);
    }

    /// <summary>
    /// Deletes the item.
    /// </summary>
    /// <param name="id">The id value.</param>
    public void Delete(int id)
    {
        foreach(var it in DataSource.Couriers) // check all courier in courier list
        {
            if (it.Id == id)
            {
                DataSource.Couriers.Remove(it);
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Courier whit ID {id} doesnt exist"); // if not found
    }

    /// <summary>
    /// Deletes the all.
    /// </summary>
    public void DeleteAll()
    {
        DataSource.Couriers.Clear();
    }

    /// <summary>
    /// Read.
    /// </summary>
    /// <param name="id">The id value.</param>
    /// <returns>The operation result.</returns>
    public Courier? Read(int id)
    {
        return DataSource.Couriers.FirstOrDefault(item => item.Id == id);
    }

    /// <summary>
    /// Read All.
    /// </summary>
    /// <param name="Func<Courier">The func courier value.</param>
    /// <param name="null">The null value.</param>
    /// <returns>The operation result.</returns>
    public IEnumerable<Courier> ReadAll(Func<Courier, bool>? filter = null)
    {
       foreach (var item in DataSource.Couriers)
        {
            if (filter == null || filter(item))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Updates the item.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void Update(Courier item)
    {
        foreach(var it in DataSource.Couriers) // check all courier in courier list
        {
            if (it.Id == item.Id)
            {
                DataSource.Couriers.Remove(it);
                DataSource.Couriers.Add(item);
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Courier whit ID {item.Id} doesnt exist"); // if not found
    }
}
