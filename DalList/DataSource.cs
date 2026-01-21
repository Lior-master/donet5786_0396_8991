
/// <summary>
/// Defines types for this application layer.
/// </summary>
namespace Dal;

/// <summary>
/// Represents the data source component in this layer.
/// </summary>
internal static class DataSource
{
    internal static List<DO.Order> Orders { get; } = new();
    internal static List<DO.Delivery> Deliveries { get; } = new();
    internal static List<DO.Courier> Couriers { get; } = new();

   
}
