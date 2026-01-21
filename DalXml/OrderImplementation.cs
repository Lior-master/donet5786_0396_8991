using System.Runtime.CompilerServices;
using DalApi;
using DO;
using System.Xml.Linq;

/// <summary>
/// Defines types for this application layer.
/// </summary>
namespace Dal;

/// <summary>
/// Represents the order implementation component in this layer.
/// </summary>
internal class OrderImplementation : IOrder
{
    static Order getOrder(XElement o)
    {
        return new DO.Order()
        {
            Id = o.ToIntNullable("Id") ?? throw new DalFormatException("cant convert id"),
            Type = o.ToEnumNullable<OrderType>("Type") ?? OrderType.FastFood,
            CustomerName = (string?)o.Element("CustomerName") ?? "",
            CustomerAddress = (string?)o.Element("CustomerAddress") ?? "",
            CustomerPhone = (string?)o.Element("CustomerPhone") ?? "",
            OrderDate = o.ToDateTimeNullable("OrderDate") ?? DateTime.Now,
            size = o.ToDoubleNullable("size"),
            weight = o.ToDoubleNullable("weight"),
            Latitude = o.ToDoubleNullable("Latitude"),
            Longitude = o.ToDoubleNullable("Longitude"),
            Fragility = o.ToEnumNullable<FragilityLevel>("Fragility"),
            Description = (string?)o.Element("Description")
        };
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void Create(Order item)
    {
        List<Order> orders = XmlTools.LoadListFromXMLSerializer<Order>(Config.s_orders_xml);
        Order clone = item with { Id = Config.NextOrderId };
        orders.Add(clone);
        XmlTools.SaveListToXMLSerializer(orders, Config.s_orders_xml);
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Deletes the item.
    /// </summary>
    /// <param name="id">The id value.</param>
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

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Deletes the all.
    /// </summary>
    public void DeleteAll()
    {
        List<Order> orders = XmlTools.LoadListFromXMLSerializer<Order>(Config.s_orders_xml);
        orders.Clear();
        XmlTools.SaveListToXMLSerializer(orders, Config.s_orders_xml);
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Read.
    /// </summary>
    /// <param name="id">The id value.</param>
    /// <returns>The operation result.</returns>
    public Order? Read(int id)
    {
        XElement? orderElem = XmlTools.LoadListFromXMLElement(Config.s_orders_xml).Elements()
            .FirstOrDefault(o => (int?)o.Element("Id") == id);
        return orderElem == null ? null : getOrder(orderElem);
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Read All.
    /// </summary>
    /// <param name="Func<Order">The func order value.</param>
    /// <param name="null">The null value.</param>
    /// <returns>The operation result.</returns>
    public IEnumerable<Order> ReadAll(Func<Order, bool>? filter = null)
    {
        List<Order> orders = XmlTools.LoadListFromXMLSerializer<Order>(Config.s_orders_xml);
        foreach (var item in orders)
        {
            if (filter == null || filter(item))
                yield return item;
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Updates the item.
    /// </summary>
    /// <param name="item">The item value.</param>
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
