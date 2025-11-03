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
        throw new NotImplementedException();
    }

    public void DeleteAll()
    {
        throw new NotImplementedException();
    }

    public Courier? Read(int id)
    {
        throw new NotImplementedException();
    }

    public List<Courier> ReadAll()
    {
        throw new NotImplementedException();
    }

    public void Update(Courier item)
    {
        throw new NotImplementedException();
    }
}
