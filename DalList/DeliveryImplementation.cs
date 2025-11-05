namespace Dal;
using DalApi;
using DO;

internal class DeliveryImplementation : IDelivery
{
    public void Create(Delivery item)
    {
        Delivery clone = item with { Id = Config.NextDeliveryIdValue };
        DataSource.Deliveries.Add(clone);
    }


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
        throw new InvalidOperationException($"Delivery with Id {id} does not exist.");
    }

    public void DeleteAll()
    {
        DataSource.Deliveries.Clear();

    }

    public Delivery? Read(int id)
    {
        foreach (var item in DataSource.Deliveries)
        {
            if (item.Id == id)
            {
                return item;
            }
        }
        return null;
    }

    public List<Delivery> ReadAll()
    {
        return new List<Delivery>(DataSource.Deliveries);
    }

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
        throw new InvalidOperationException($"Delivery with Id {item.Id} does not exist.");
    }
}
