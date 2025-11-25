using DalApi;
using DO;
using System.Linq;

namespace Helpers;

internal static class OrderManager
{
    private static readonly IDal s_dal = Factory.Get;

    internal static void PeriodicOrdersUpdates(DateTime oldClock, DateTime newClock)
    {
        TimeSpan maxDeliveryTime = s_dal.Config.MaxTimeDelivery;
        TimeSpan riskRange = s_dal.Config.RiskRange;

        _ = s_dal.Order.ReadAll()
            .Where(o => o.Status == OrderStatus.Pending ||
                        o.Status == OrderStatus.Processing)
            .Select(o =>
            {
                TimeSpan elapsed = newClock - o.OrderDate;
                var newStatus = o.Status;

                if (elapsed > maxDeliveryTime)
                {
                    newStatus = OrderStatus.Canceled;
                }
                else if (elapsed >= (maxDeliveryTime - riskRange))
                {
                    if (o.Status == OrderStatus.Pending)
                        newStatus = OrderStatus.Processing;
                }
                else
                {
                    newStatus = OrderStatus.Pending;
                }

}
