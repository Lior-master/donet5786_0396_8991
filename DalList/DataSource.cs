
namespace Dal;

internal static class DataSource
{
    internal static List<DO.Order> Orders { get; } = new();
    internal static List<DO.Delivery> Deliveries { get; } = new();
    internal static List<DO.Courier> Couriers { get; } = new();

   
}
