namespace BO;

public class Order
{
    public int Id { get; init; }
    public OrderType Type { get; set; }
    public string? OrderDescription { get; set; }
    public string CustomerAdress { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Distance { get; set; }
    public string CustomerName { get; set; }
    public string CustomerPhone { get; set; }
    public double? Weight { get; set; }
    public FragilityLevel? Fragility { get; set; }
    public double? Volume { get; set; }
    public DateTime OrderDate { get; init; }
    public DateTime? ArrivalDateEstimeted { get; init; }
    public DateTime? ArrivalDateMax { get; init; }
    public OrderStatus Status { get; init; }
    public ScheduleStatus ScheduleStatus { get; init; }
    public TimeSpan ArrivalTimeEstimeted { get; init; }
    public List<BO.DeliveryPerOrderInList>? DeliveriesPerOrder { get; init; }
}
