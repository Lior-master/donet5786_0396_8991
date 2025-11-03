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
        foreach(var it in DataSource.Couriers) // check all courier in courier list
        {
            if (it.Id == id)
            {
                DataSource.Couriers.Remove(it);
                return;
            }
        }
        throw new InvalidOperationException($"Object Courier whit ID {id} doesnt exist"); // if not found
    }

    public void DeleteAll()
    {
        foreach(var it in DataSource.Couriers)
        {
            DataSource.Couriers.Remove(it);
        }
    }

    public Courier? Read(int id)
    {
        foreach(var it in DataSource.Couriers) // check all courier in courier list
        {
            if (it.Id == id)
            {
                return it;
            }
        }
        return null; // if not found

    }

    public List<Courier> ReadAll()
    {
        return new List<Courier>(DataSource.Couriers);
    }

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
        throw new InvalidOperationException($"Object Courier whit ID {item.Id} doesnt exist"); // if not found
    }
}
