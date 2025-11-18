namespace Dal;
using DalApi;
using DO;

internal class CourierImplementation : ICourier
{
    public void Create(Courier item)
    {
        // Load existing couriers, add the new one and save.
        List<Courier> couriers = XmlTools.LoadListFromXMLSerializer<Courier>(Config.s_couriers_xml);
        couriers.Add(item);
        XmlTools.SaveListToXMLSerializer(couriers, Config.s_couriers_xml);
    }

    public void Delete(int id)
    {
        List<Courier> Couriers = XmlTools.LoadListFromXMLSerializer<Courier>(Config.s_couriers_xml);
        foreach (var it in Couriers) // check all courier in courier list
        {
            if (it.Id == id)
            {
                Couriers.Remove(it);
                XmlTools.SaveListToXMLSerializer(Couriers, Config.s_couriers_xml);
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Courier whit ID {id} doesnt exist"); // if not found
    }

    public void DeleteAll()
    {
        XmlTools.SaveListToXMLSerializer(new List<Courier>(), Config.s_couriers_xml);
    }

    public Courier? Read(int id)
    {
        List<Courier> couriers = XmlTools.LoadListFromXMLSerializer<Courier>(Config.s_couriers_xml);
        foreach (var item in couriers)
        {
            if (item.Id == id)
                return item;
        }
        return null;
    }

    public IEnumerable<Courier> ReadAll(Func<Courier, bool>? filter = null)
    {
        List<Courier> couriers = XmlTools.LoadListFromXMLSerializer<Courier>(Config.s_couriers_xml);
        foreach (var item in couriers)
        {
            if (filter == null || filter(item))
            {
                yield return item;
            }
        }
    }

    public void Update(Courier item)
    {
        List<Courier> Couriers = XmlTools.LoadListFromXMLSerializer<Courier>(Config.s_couriers_xml);
        foreach (var it in Couriers) // check all courier in courier list
        {
            if (it.Id == item.Id)
            {
                Couriers.Remove(it);
                Couriers.Add(item);
                XmlTools.SaveListToXMLSerializer(Couriers, Config.s_couriers_xml);
            }
        }
        throw new DalDoesNotExistException($"Object Courier whit ID {item.Id} doesnt exist"); // if not found

    }
}
