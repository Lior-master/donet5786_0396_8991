using BO;

namespace BLApi;

public interface ICourier
{
    BO.Courier Login(string username,string password);
    IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, DeliveryTransport? status);
    BO.Courier GetCourierDetails(int requesterId, int courierId);
    void UpdateCourier(int requesterId, Courier updatedCourier);
    void removeCourier(int requesterId, int courierId);
    void addCourier(int requesterId, Courier newCourier);
}
