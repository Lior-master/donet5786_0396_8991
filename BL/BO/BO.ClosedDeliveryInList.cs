namespace BO;

public class ClosedDeliveryInList
{
    public int DeliveryId { get; init; }
    public int OrderId { get; init; }
    public OrderType OrderType { get; init; }
    public string CustomerAdress { get; init; }
    public DeliveryTransport DeliveryTransport { get; init; }
    public double? ActualDistance { get; init; }
    public TimeSpan DeliveryTotalTime  { get; init; }
    public DeliveredStatus DeliveredStatus { get; init; }
}
