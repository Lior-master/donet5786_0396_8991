namespace Dal;
using DalApi;
using DO;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

/// <summary>
/// Represents the delivery implementation component in this layer.
/// </summary>
internal class DeliveryImplementation : IDelivery
{
    static Delivery getDelivery(XElement d)
    {
        return new DO.Delivery()
        {
            Id = d.ToIntNullable("Id") ?? throw new DalFormatException("cant convert id"),
            OrderId = d.ToIntNullable("OrderId") ?? throw new DalFormatException("cant convert order id"),
            Transport = d.ToEnumNullable<DeliveryTransport>("Transport") ?? DeliveryTransport.Car,
            CourierId = d.ToIntNullable("CourierId") ?? 0,
            PickupTime = d.ToDateTimeNullable("PickupTime") ?? DateTime.Now,
            ArrivalTime = d.ToDateTimeNullable("ArrivalTime"),
            Distance = d.ToDoubleNullable("Distance"),
            DeliveredStatus = d.ToEnumNullable<DeliveredStatus>("DeliveredStatus")
        };
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void Create(Delivery item)
    {
        List<Delivery> deliveries = XmlTools.LoadListFromXMLSerializer<Delivery>(Config.s_deliveries_xml);
        Delivery clone = item with { Id = Config.NextDeliveryId };
        deliveries.Add(clone);
        XmlTools.SaveListToXMLSerializer(deliveries, Config.s_deliveries_xml);
        ;
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Deletes the item.
    /// </summary>
    /// <param name="id">The id value.</param>
    public void Delete(int id)
    {
        List<Delivery> deliveries = XmlTools.LoadListFromXMLSerializer<Delivery>(Config.s_deliveries_xml);
        foreach (var it in deliveries)
        {
            if (it.Id == id)
            {
                deliveries.Remove(it);
                XmlTools.SaveListToXMLSerializer(deliveries, Config.s_deliveries_xml);
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Delivery with ID {id} doesnt exist");
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Deletes the all.
    /// </summary>
    public void DeleteAll()
    {
        List<Delivery> deliveries = XmlTools.LoadListFromXMLSerializer<Delivery>(Config.s_deliveries_xml);
        deliveries.Clear();
        XmlTools.SaveListToXMLSerializer(deliveries, Config.s_deliveries_xml);
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Read.
    /// </summary>
    /// <param name="id">The id value.</param>
    /// <returns>The operation result.</returns>
    public Delivery? Read(int id)
    {
        XElement? deliveryElem = XmlTools.LoadListFromXMLElement(Config.s_deliveries_xml).Elements()
            .FirstOrDefault(d => (int?)d.Element("Id") == id);
        return deliveryElem == null ? null : getDelivery(deliveryElem);
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    /// <summary>
    /// Read All.
    /// </summary>
    /// <param name="Func<Delivery">The func delivery value.</param>
    /// <param name="null">The null value.</param>
    /// <returns>The operation result.</returns>
    public IEnumerable<Delivery> ReadAll(Func<Delivery, bool>? filter = null)
    {
        List<Delivery> deliveries = XmlTools.LoadListFromXMLSerializer<Delivery>(Config.s_deliveries_xml);
        foreach (var item in deliveries)
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
    public void Update(Delivery item)
    {
        List<Delivery> deliveries = XmlTools.LoadListFromXMLSerializer<Delivery>(Config.s_deliveries_xml);
        for (int i = 0; i < deliveries.Count; i++)
        {
            if (deliveries[i].Id == item.Id)
            {
                deliveries.RemoveAt(i);
                deliveries.Add(item);
                XmlTools.SaveListToXMLSerializer(deliveries, Config.s_deliveries_xml);
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Delivery with ID {item.Id} doesnt exist");
    }
}
