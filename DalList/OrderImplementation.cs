namespace Dal;
using DalApi;
using DO;

public class OrderImplementation : IOrder
{
    public void Create(Order item)
    {
        throw new NotImplementedException();
    }

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
        throw new InvalidOperationException($"Object Order whit ID {id} doesnt exist"); // if not found
    }

    public void DeleteAll()
    {
        foreach (var it in DataSource.Orders)
        {
            DataSource.Orders.Remove(it);
        }
    }

    public Order? Read(int id)
    {
        foreach (var it in DataSource.Orders) // check all order in orders list
        {
            if (it.Id == id)
            {
                return it;
            }
        }
        return null; // if not found

    }

    public List<Order> ReadAll()
    {
        return new List<Order>(DataSource.Orders);
    }

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
        throw new InvalidOperationException($"Object Order whit ID {item.Id} doesnt exist"); // if not found
    }

}

