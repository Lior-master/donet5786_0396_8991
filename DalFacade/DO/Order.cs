using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DO;

public record Order
{
    int Id,
    OrderType Type,
    string? Description = null;
    int CustomerId;
    DateTime ? OrderDate;
    OrderStatus ? 
    DeliveryTransport TransportMethod,
    string ?  Address = null;
    string? City = null;
    string? Country = null;
    string? ZipCode = null;
}
