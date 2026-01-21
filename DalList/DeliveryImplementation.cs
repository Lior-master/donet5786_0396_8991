namespace Dal;
using DalApi;
using DO;

/// <summary>
/// Represents the delivery implementation component in this layer.
/// </summary>
internal class DeliveryImplementation : IDelivery
{
    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void Create(Delivery item)
    {
        Delivery clone = item with { Id = Config.NextDeliveryIdValue };
        DataSource.Deliveries.Add(clone);
    }


    /// <summary>
    /// Deletes the item.
    /// </summary>
    /// <param name="id">The id value.</param>
    public void Delete(int id)
    {
        foreach (var it in DataSource.Deliveries)
        {
            if (it.Id == id)
            {
                DataSource.Deliveries.Remove(it);
                return;
            }
        }

        // If id not found, act accordingly (consistently with Update): throw an exception.
        throw new DalDoesNotExistException($"Delivery with Id {id} does not exist.");
    }

    /// <summary>
    /// Deletes the all.
    /// </summary>
    public void DeleteAll()
    {
        DataSource.Deliveries.Clear();

    }

    /// <summary>
    /// Read.
    /// </summary>
    /// <param name="id">The id value.</param>
    /// <returns>The operation result.</returns>
    public Delivery? Read(int id)
    {
        return DataSource.Deliveries.FirstOrDefault(item => item.Id == id);
    }

    /// <summary>
    /// Read All.
    /// </summary>
    /// <param name="Func<Delivery">The func delivery value.</param>
    /// <param name="null">The null value.</param>
    /// <returns>The operation result.</returns>
    public IEnumerable<Delivery> ReadAll(Func<Delivery, bool>? filter = null)
    {
        foreach (var item in DataSource.Deliveries)
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
    public void Update(Delivery item)
    {
        foreach (var it in DataSource.Deliveries)
        {
            if (it.Id == item.Id)
            {
                DataSource.Deliveries.Remove(it);
                DataSource.Deliveries.Add(item);
                return;
            }
        }
        throw new DalDoesNotExistException($"Delivery with Id {item.Id} does not exist.");
    }
}
