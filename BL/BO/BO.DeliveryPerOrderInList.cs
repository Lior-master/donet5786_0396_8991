using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BO
{
    public class DeliverPerOrderInList
    {
        public int DeliveryId { get; set; }
        public int CourierId { get; set; }
        public string Name { get; set; }
        public OrderType OrderType { get; set; }
        public DateTime PickupTime { get; set; }

        public OrderStatus OrderStatus { get; set; }
        public DateTime ArrivalTime { get; set; }
    }
}
