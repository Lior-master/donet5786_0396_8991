namespace Dal;
using DalApi;
using DO;
using System.Collections.Generic;

public class CourierImplementation : ICourier
{
    public void Create(Courier item)
    {
        if (item == null) {
            throw new ArgumentNullException("item cannot be null");
        }
        foreach(var it in DataSource.Couriers)
        {
            if (it.Id == item.Id)
            {
                throw new InvalidOperationException($"Courier with Id {item.Id} already exists.");
            }
        }
        DataSource.Couriers.Add(item);
    }

    public void Delete(int id)
    {
        foreach(var it in DataSource.Couriers)
        {
            if (it.Id == id)
            {
                DataSource.Couriers.Remove(it);
                return;
            }
        }
        throw new InvalidOperationException($"There are not Courier whit this ID: {id}");
    }

    public void DeleteAll()
    {
        foreach(var it in DataSource.Couriers.ToList())
        {
            DataSource.Couriers.Remove(it);
        }
    }

    public Courier? Read(int id)
    {
        foreach(var it in DataSource.Couriers)
        {
            if (it.Id == id)
            {
                return it;
            }
        }
        throw new InvalidOperationException($"There are not Courier whit this ID: {id}");
    }

    public List<Courier> ReadAll()
    {
        return new List<Courier>(DataSource.Couriers);
    }

    public void Update(Courier item)
    {
        throw new NotImplementedException();
    }
}
