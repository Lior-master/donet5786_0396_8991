using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BO
{
    public class OpenOrderInList
    {
        public int CourierId { get; init; }
        public int OrderId { get; init; }
        public OrderType OrderType { get; init; }
        public FragilityLevel? Fragility { get; init; }
        public string CustomerAddress { get; init; }
        public double BirdDistance { get; init; }
        public double Distance { get; init; }
        public TimeSpan AddedTime { get; init; }
        public ScheduleStatus ScheduleStatus { get; init; }
        public TimeSpan EstimatedDeliveryTime { get; init; }
        public DateTime MaxDeliveredTime { get; init; }
    }
}
