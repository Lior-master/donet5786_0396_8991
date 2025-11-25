using DalApi;
using DO;
using System.Linq;

namespace Helpers;

internal static class CourierManager
{
    private static readonly IDal s_dal = Factory.Get;

    /// <summary>
    /// Called from AdminManager.UpdateClock after every clock update.
    /// Deactivates couriers whose last delivery is older than Config.Inactivity.
    /// </summary>
    internal static void PeriodicCouriersUpdates(DateTime oldClock, DateTime newClock)
    {
        TimeSpan inactivityThreshold = s_dal.Config.Inactivity;

        var allDeliveries = s_dal.Delivery.ReadAll();

        _ = s_dal.Courier.ReadAll()
            .Where(c => c.IsActive)
            .Select(c => new
            {
                Courier = c,
                LastArrival =
                    allDeliveries
                        .Where(d => d.CourierId == c.Id && d.ArrivalTime != null)
                        .OrderByDescending(d => d.ArrivalTime)
                        .Select(d => d.ArrivalTime)
                        .FirstOrDefault()
            })
            .Where(x => x.LastArrival != null)
            .Where(x =>
                (oldClock - x.LastArrival!.Value) <= inactivityThreshold &&
                (newClock - x.LastArrival!.Value) > inactivityThreshold)
            .Select(x =>
            {
                var updated = x.Courier with { IsActive = false };
                s_dal.Courier.Update(updated);
                return updated;
            })
            .ToList();
    }
}
