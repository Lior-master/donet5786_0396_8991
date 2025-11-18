namespace Dal;
using DalApi;
using DO;
using System;
using System.Collections.Generic;

internal class CourierImplementation : ICourier
{
    public void Create(Courier item)
    {
        throw new NotImplementedException();
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

    public IEnumerable<Courier> ReadAll(Func<Courier, bool>? filter = null)
    {
        throw new NotImplementedException();
    }

    public void Update(Courier item)
    {
        throw new NotImplementedException();
    }
}
