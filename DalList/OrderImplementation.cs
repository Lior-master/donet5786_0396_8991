namespace Dal;
using DalApi;
using DO;

/// <summary>
/// Represents the order implementation component in this layer.
/// </summary>
internal class OrderImplementation : IOrder
{
    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void Create(Order item)
    {
        Order clone = item with { Id = Config.NextOrderId };
        DataSource.Orders.Add(clone);
    }

    /// <summary>
    /// Deletes the item.
    /// </summary>
    /// <param name="id">The id value.</param>
    public void Delete(int id)
    {
        foreach (var it in DataSource.Orders) // check all order in orders list
        {
            if (it.Id == id)
            {
                DataSource.Orders.Remove(it);
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Order whit ID {id} doesnt exist"); // if not found
    }

    /// <summary>
    /// Deletes the all.
    /// </summary>
    public void DeleteAll()
    {
        foreach (var it in DataSource.Orders)
        {
            DataSource.Orders.Remove(it);
        }
    }

    /// <summary>
    /// Read.
    /// </summary>
    /// <param name="id">The id value.</param>
    /// <returns>The operation result.</returns>
    public Order? Read(int id)
    {
        return DataSource.Orders.FirstOrDefault(item => item.Id == id);

    }

    /// <summary>
    /// Read All.
    /// </summary>
    /// <param name="Func<Order">The func order value.</param>
    /// <param name="null">The null value.</param>
    /// <returns>The operation result.</returns>
    public IEnumerable<Order> ReadAll(Func<Order, bool>? filter = null)
    {
        foreach (var item in DataSource.Orders)
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
    public void Update(Order item)
    {
        foreach (var it in DataSource.Orders) // check all order in orders list
        {
            if (it.Id == item.Id)
            {
                DataSource.Orders.Remove(it);
                DataSource.Orders.Add(item);
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Order whit ID {item.Id} doesnt exist"); // if not found
    }

}

