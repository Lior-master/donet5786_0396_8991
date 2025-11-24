namespace BO;

namespace BO
{
    public class OrderInProgress
    {
        public int DeliveryId { get; init; }
        public int OrderId { get; init; }
        public int OrderStatus { get; init; } 
        public double? Distance { get; init; }
        public DateTime PickupTime { get; init; }
        public DateTime? ArrivalTime { get; init; }
        public ScheduleStatus ScheduleStatus { get; init; }
        public OrderStatus OrderStatusEnum { get; init; }
        public string Description { get; init; }
        public string CustomerName { get; init; }
        public string CustomerPhone { get; init; }
        public string CustomerAddress { get; init; }
        public DateTime OrderDate { get; init; }
        public DateTime EstimatedArrivalTime { get; init; }
        public DateTime MaxDeliveryTime { get; init; }
        public TimeSpan WaitingTime { get; init; }

}      
