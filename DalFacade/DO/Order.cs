using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DO;

public record Order
{
    string CustomerName;
    string CustomerAddress;
    string CustomerPhone,
    DeliveryTransport;
    FragilityLevel? Fragility = null;
    string? Description = null;
    OrderStatus? Status = null;


public Order() : this(0, OrderType.Standard, 0.0, 0.0, string.Empty, string.Empty, string.Empty, DateTime.Now) { }
}