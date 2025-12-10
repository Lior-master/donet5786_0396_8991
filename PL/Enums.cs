using System.Collections;
namespace PL;

internal class TransportsCollection : IEnumerable
{
    static readonly IEnumerable<BO.DeliveryTransport> s_enums =
        (Enum.GetValues(typeof(BO.DeliveryTransport)) as IEnumerable<BO.DeliveryTransport>)!;

    public IEnumerator GetEnumerator() => s_enums.GetEnumerator();
}
