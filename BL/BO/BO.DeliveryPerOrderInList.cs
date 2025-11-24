using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BO
{
    public class DeliverPerOrderInList
    {
        public int DeliveryId { get; init; }
        public int CourierId { get; init; }
        public string Name { get; init; }
        public OrderType OrderType { get; init; }
        public DateTime PickupTime { get; init; }
        public OrderStatus OrderStatus { get; init; }
        public DateTime ArrivalTime { get; init; }
    }
}
