namespace Dal;
using DalApi;
using DO;

internal class OrderImplementation : IOrder
{
    public void Create(Order item)
    {
        List<Order> orders = XmlTools.LoadListFromXMLSerializer<Order>(Config.s_orders_xml);
        orders.Add(item);
        XmlTools.SaveListToXMLSerializer(orders, Config.s_orders_xml);
    }

    public void Delete(int id)
    {
        List<Order> orders = XmlTools.LoadListFromXMLSerializer<Order>(Config.s_orders_xml);
        for (int i = 0; i < orders.Count; i++)
        {
            if (orders[i].Id == id)
            {
                orders.RemoveAt(i);
                XmlTools.SaveListToXMLSerializer(orders, Config.s_orders_xml);
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Order with ID {id} doesnt exist");
    }

    public void DeleteAll()
    {
        List<Order> orders = XmlTools.LoadListFromXMLSerializer<Order>(Config.s_orders_xml);
        orders.Clear();
        XmlTools.SaveListToXMLSerializer(orders, Config.s_orders_xml);
    }

    public Order? Read(int id)
    {
        List<Order> orders = XmlTools.LoadListFromXMLSerializer<Order>(Config.s_orders_xml);
        for (int i = 0; i < orders.Count; i++)
        {
            if (orders[i].Id == id)
                return orders[i];
        }
        return null;
    }

    public IEnumerable<Order> ReadAll(Func<Order, bool>? filter = null)
    {
        List<Order> orders = XmlTools.LoadListFromXMLSerializer<Order>(Config.s_orders_xml);
        foreach (var item in orders)
        {
            if (filter == null || filter(item))
                yield return item;
        }
    }

    public void Update(Order item)
    {
        List<Order> orders = XmlTools.LoadListFromXMLSerializer<Order>(Config.s_orders_xml);
        for (int i = 0; i < orders.Count; i++)
        {
            if (orders[i].Id == item.Id)
            {
                orders[i] = item;
                XmlTools.SaveListToXMLSerializer(orders, Config.s_orders_xml);
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Order with ID {item.Id} doesnt exist");
    }
}
