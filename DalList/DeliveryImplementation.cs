namespace Dal;
using DalApi;
using DO;

public class DeliveryImplementation : IDelivery
{
    public void Create(Delivery item)
    {
        // Vérifie si l'ID existe déjà
        if (item == null)
        {
            throw new ArgumentNullException("item cannot be null");
        }
        foreach (var it in DataSource.Deliveries)
        {
            if (it.Id == item.Id)
            {
                throw new InvalidOperationException($"Courier with Id {item.Id} already exists.");
            }
        }
        // Ajouter la livraison à la liste
        DataSource.Deliveries.Add(item);
    }


    public void Delete(int id)
    {
        for (int i = 0; i < DataSource.Deliveries.Count; i++)
        {
            if (DataSource.Deliveries[i].Id == id)
            {
                DataSource.Deliveries.RemoveAt(i);
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
