namespace BO;

public class OrderInProgress
{
    public int DeliveryId { get; set; }
    public int OrderId { get; set; }
    public int OrderStatus { get; set; } 
    public double? Distance { get; set; }
    public DateTime PickupTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public ScheduleStatus ScheduleStatus { get; set; }
    public OrderStatus OrderStatusEnum { get; set; }
    public string Description { get; set; }
    public string CustomerName { get; set; }
    public string CustomerPhone { get; set; }
    public string CustomerAddress { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime EstimatedArrivalTime { get; set; }
    public DateTime MaxDeliveryTime { get; set; }
    public TimeSpan WaitingTime { get; set; }

}      
