namespace Dal;
using DalApi;
using DO;

internal class OrderImplementation : IOrder
{
    public void Create(Order item)
    {
        Order clone = item with { Id = Config.NextOrderId };
        DataSource.Orders.Add(clone);
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
        throw new DalDoesNotExistException($"Object Order whit ID {id} doesnt exist"); // if not found
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
        return DataSource.Orders.FirstOrDefault(item => item.Id == id);

    }

    public IEnumerable<Order> ReadAll(Func<Order, bool>? filter = null)
    {
        foreach (var item in DataSource.Orders)
        {
            if (filter == null || filter(item))
            {
                yield return item;
            }
        }
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
        throw new DalDoesNotExistException($"Object Order whit ID {item.Id} doesnt exist"); // if not found
    }

}

