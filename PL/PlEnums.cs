using System.Collections;
namespace PL;

internal class TransportsCollection : IEnumerable
{
    static readonly IEnumerable<BO.DeliveryTransport> s_enums =
        (Enum.GetValues(typeof(BO.DeliveryTransport)) as IEnumerable<BO.DeliveryTransport>)!;

    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

internal class FilterTypeOrderCollection : IEnumerable
{
    static readonly IEnumerable<FilterTypeOrder> s_enums =
        (Enum.GetValues(typeof(FilterTypeOrder)) as IEnumerable<FilterTypeOrder>)!;
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

internal class FilterTypeCourierCollection : IEnumerable
{
    static readonly IEnumerable<FilterTypeCourier> s_enums =
        (Enum.GetValues(typeof(FilterTypeCourier)) as IEnumerable<FilterTypeCourier>)!;
    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

internal class OrderStatusCollection : IEnumerable
{
    static readonly IEnumerable<BO.OrderStatus> s_enums =
        (Enum.GetValues(typeof(BO.OrderStatus)) as IEnumerable<BO.OrderStatus>)!;

    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}

public enum FilterTypeOrder
{
    All,
    ByOrderType,
    ByOrderStatus,
    ByDeliveryStatus,
    ByFragilityLevel,
    BySheduleStatus
}

public enum FilterTypeCourier
{
    All,
    ByTransportType,
    ByAdministratorType,
    ByActiveStatus
}
