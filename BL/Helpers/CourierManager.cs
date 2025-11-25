using DalApi;

namespace Helpers;

internal static class CourierManager
{
    private static IDal s_dal = Factory.Get;

    internal static void PeriodicCouriersUpdates(DateTime oldClock, DateTime newClock)
    {
        TimeSpan threshold = s_dal.Config.Inactivity;
        var allDeliveries = s_dal.Delivery.ReadAll();

        _ = s_dal.Courier.ReadAll()
            .Where(c => c.IsActive)
            .Select(c => new
            {
                Courier = c,
                LastEnd =
                    allDeliveries
                        .Where(d => d.CourierId == c.Id && d.ArrivalTime != null)
                        .OrderByDescending(d => d.ArrivalTime)
                        .Select(d => d.ArrivalTime)
                        .FirstOrDefault()
            })
            .Where(x => x.LastEnd != null)
            .Where(x =>
                (oldClock - x.LastEnd!.Value) <= threshold &&
                (newClock - x.LastEnd!.Value) > threshold)
            .Select(x =>
            {
                s_dal.Courier.Update(x.Courier with { IsActive = false });
                return x;
            })
            .ToList();
    }
}
