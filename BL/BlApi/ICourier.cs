using BO;
using System.Threading.Tasks;
namespace BlApi;

// Specify the type parameter for IObservable<T>.
// Assuming you want to observe Courier changes, use Courier as the type argument.
public interface ICourier : IObservable
{
    Administrator Login(int Id, string password);
    IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive, Enum? status);
    Task<BO.Courier> GetCourierDetailsAsync(int requesterId, int courierId);
    void UpdateCourier(int requesterId, Courier updatedCourier);
    void removeCourier(int requesterId, int courierId);
    void addCourier(int requesterId, Courier newCourier);
    void PromoteToDirector(int requesterId, int courierId);
}
