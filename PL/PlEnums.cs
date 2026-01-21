using System.Collections;
/// <summary>
/// Implements the presentation layer UI and related view models.
/// </summary>
namespace PL;

/// <summary>
/// Represents the delivered status collection component in this layer.
/// </summary>
internal class DeliveredStatusCollection : IEnumerable
{
    static readonly IEnumerable<BO.DeliveredStatus> s_enums =
        (Enum.GetValues(typeof(BO.DeliveredStatus)) as IEnumerable<BO.DeliveredStatus>)!;
    /// <summary>
    /// Gets the enumerator value.
    /// </summary>
    /// <returns>The operation result.</returns>
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}
/// <summary>
/// Represents the transports collection component in this layer.
/// </summary>
internal class TransportsCollection : IEnumerable
{
    static readonly IEnumerable<BO.DeliveryTransport> s_enums =
        (Enum.GetValues(typeof(BO.DeliveryTransport)) as IEnumerable<BO.DeliveryTransport>)!;

    /// <summary>
    /// Gets the enumerator value.
    /// </summary>
    /// <returns>The operation result.</returns>
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

/// <summary>
/// Represents the filter type order collection component in this layer.
/// </summary>
internal class FilterTypeOrderCollection : IEnumerable
{
    static readonly IEnumerable<FilterTypeOrder> s_enums =
        (Enum.GetValues(typeof(FilterTypeOrder)) as IEnumerable<FilterTypeOrder>)!;
    /// <summary>
    /// Gets the enumerator value.
    /// </summary>
    /// <returns>The operation result.</returns>
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

/// <summary>
/// Represents the filter type courier collection component in this layer.
/// </summary>
internal class FilterTypeCourierCollection : IEnumerable
{
    static readonly IEnumerable<FilterTypeCourier> s_enums =
        (Enum.GetValues(typeof(FilterTypeCourier)) as IEnumerable<FilterTypeCourier>)!;
    /// <summary>
    /// Gets the enumerator value.
    /// </summary>
    /// <returns>The operation result.</returns>
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

/// <summary>
/// Represents the order status collection component in this layer.
/// </summary>
internal class OrderStatusCollection : IEnumerable
{
    static readonly IEnumerable<BO.OrderStatus> s_enums =
        (Enum.GetValues(typeof(BO.OrderStatus)) as IEnumerable<BO.OrderStatus>)!;

    /// <summary>
    /// Gets the enumerator value.
    /// </summary>
    /// <returns>The operation result.</returns>
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

/// <summary>
/// Represents the administrator type collection component in this layer.
/// </summary>
internal class AdministratorTypeCollection : IEnumerable
{
    static readonly IEnumerable<BO.Administrator> s_enums =
        (Enum.GetValues(typeof(BO.Administrator)) as IEnumerable<BO.Administrator>)!;
    /// <summary>
    /// Gets the enumerator value.
    /// </summary>
    /// <returns>The operation result.</returns>
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

/// <summary>
/// Represents the order type collection component in this layer.
/// </summary>
internal class OrderTypeCollection : IEnumerable
{
    static readonly IEnumerable<BO.OrderType> s_enums =
        (Enum.GetValues(typeof(BO.OrderType)) as IEnumerable<BO.OrderType>)!;
    /// <summary>
    /// Gets the enumerator value.
    /// </summary>
    /// <returns>The operation result.</returns>
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

/// <summary>
/// Represents the schedule status collection component in this layer.
/// </summary>
internal class ScheduleStatusCollection : IEnumerable
{
    static readonly IEnumerable<BO.ScheduleStatus> s_enums =
        (Enum.GetValues(typeof(BO.ScheduleStatus)) as IEnumerable<BO.ScheduleStatus>)!;
    /// <summary>
    /// Gets the enumerator value.
    /// </summary>
    /// <returns>The operation result.</returns>
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

/// <summary>
/// Defines supported filter type order values.
/// </summary>
public enum FilterTypeOrder
{
    All,
    ByOrderType,
    ByOrderStatus,
    BySheduleStatus,
    ByOrderAndSchedulStatus
}

/// <summary>
/// Defines supported filter type courier values.
/// </summary>
public enum FilterTypeCourier
{
    All,
    ByTransportType,
    ByAdministratorType,
    ByActiveStatus
}
