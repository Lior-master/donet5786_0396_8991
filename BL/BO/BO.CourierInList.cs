namespace BO;

public class CourierInList
{
    public int Id { get; init; }
    public string Name { get; init; }
    public bool IsActive { get; init; }
    public DeliveryTransport Transport { get; init; }
    public DateTime StartDate { get; init; }
    public int NumberOfOnTimeDeliveries { get; init; }
    public int NumberOfLateDeliveries { get; init; }
    public int? ActualOrder { get; init; }
}
