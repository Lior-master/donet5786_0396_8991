using System.Runtime.CompilerServices;
using DalApi;
using DO;
using System.Xml.Linq;

namespace Dal;

internal class CourierImplementation : ICourier
{
    static Courier getCourier(XElement c)
    {
        return new DO.Courier()
        {
            Id = c.ToIntNullable("Id") ?? throw new DalFormatException("cant convert id"),
            Name = (string?)c.Element("Name") ?? "",
            Phone = (string?)c.Element("Phone") ?? "",
            Email = (string?)c.Element("Email") ?? "",
            Password = (string?)c.Element("Password") ?? "",
            IsActive = (bool?)c.Element("IsActive") ?? false,
            Transport = c.ToEnumNullable<DeliveryTransport>("Transport") ?? DeliveryTransport.Car,
            StartDate = c.ToDateTimeNullable("StartDate") ?? DateTime.Now,
            Administrator = c.ToEnumNullable<Administrator>("Administrator") ?? Administrator.Courier,
            MaxDistance = c.ToDoubleNullable("MaxDistance")

        };
    }
    
    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    public void Create(Courier item)
    {
        // Load existing couriers, add the new one and save.
        List<Courier> couriers = XmlTools.LoadListFromXMLSerializer<Courier>(Config.s_couriers_xml);
        couriers.Add(item);
        XmlTools.SaveListToXMLSerializer(couriers, Config.s_couriers_xml);
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
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

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    public void DeleteAll()
    {
        XmlTools.SaveListToXMLSerializer(new List<Courier>(), Config.s_couriers_xml);
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    public Courier? Read(int id)
    {
        XElement? courierElem = XmlTools.LoadListFromXMLElement(Config.s_couriers_xml).Elements()
            .FirstOrDefault(c => (int?)c.Element("Id") == id);
        return courierElem == null ? null : getCourier(courierElem);
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
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

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
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
                return;
            }
        }
        throw new DalDoesNotExistException($"Object Courier whit ID {item.Id} doesnt exist"); // if not found

    }
}
