namespace Dal;
using DalApi;
using DO;
using System.Collections.Generic;

public class CourierImplementation : ICourier
{
    public void Create(Courier item)
    {        
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
        throw new Exception($"Object Courier whit ID {id} doesnt exist"); // if not found
    }

    public void DeleteAll()
    {
        DataSource.Couriers.Clear();
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
        throw new Exception($"Object Courier whit ID {item.Id} doesnt exist"); // if not found
    }
}
