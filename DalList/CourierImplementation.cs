namespace Dal;
using DalApi;
using DO;
using System.Collections.Generic;

internal class CourierImplementation : ICourier
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
        throw new DalDoesNotExistException($"Object Courier whit ID {id} doesnt exist"); // if not found
    }

    public void DeleteAll()
    {
        DataSource.Couriers.Clear();
    }

    public Courier? Read(int id)
    {
        return DataSource.Couriers.FirstOrDefault(item => item.Id == id);
    }

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
