namespace BO;

public class OrderInList
{
    public int? DeliveryId { get; init; }
    public int OrderId { get; init; }
    public OrderType Type { get; init; }
    public double Distance { get; init; }
    public OrderStatus Status { get; init; }
    public ScheduleStatus ScheduleStatus { get; init; }
    public TimeSpan OrderEndTime { get; init; }
    public TimeSpan TreatmentEndTime { get; init; }
    public int NumberOfCouriers { get; init; }
}
