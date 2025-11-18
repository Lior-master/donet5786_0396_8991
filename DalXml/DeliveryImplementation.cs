namespace Dal;
using DalApi;
using DO;

internal class DeliveryImplementation : IDelivery
{
    public void Create(Delivery item)
    {
        List<Delivery> deliveries = XmlTools.LoadListFromXMLSerializer<Delivery>(Config.s_deliveries_xml);
        deliveries.Add(item);
        XmlTools.SaveListToXMLSerializer(deliveries, Config.s_deliveries_xml);
    }

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

    public void DeleteAll()
    {
        List<Delivery> deliveries = XmlTools.LoadListFromXMLSerializer<Delivery>(Config.s_deliveries_xml);
        deliveries.Clear();
        XmlTools.SaveListToXMLSerializer(deliveries, Config.s_deliveries_xml);
    }

    public Delivery? Read(int id)
    {
        List<Delivery> deliveries = XmlTools.LoadListFromXMLSerializer<Delivery>(Config.s_deliveries_xml);
        foreach (var item in deliveries)
        {
            if (item.Id == id)
                return item;
        }
        return null;
    }

    public IEnumerable<Delivery> ReadAll(Func<Delivery, bool>? filter = null)
    {
        List<Delivery> deliveries = XmlTools.LoadListFromXMLSerializer<Delivery>(Config.s_deliveries_xml);
        foreach (var item in deliveries)
        {
            if (filter == null || filter(item))
                yield return item;
        }
    }

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
