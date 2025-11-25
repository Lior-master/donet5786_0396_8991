using DalApi;
using DO;
using System.Linq;

namespace Helpers;

internal static class DeliveryManager
{
    private static readonly IDal s_dal = Factory.Get;

    /// <summary>
    /// Called from AdminManager.UpdateClock after every clock update.
    /// Starts deliveries whose pickup time has just been reached.
    /// </summary>
    internal static void PeriodicDeliveriesUpdates(DateTime oldClock, DateTime newClock)
    {
        _ = s_dal.Delivery.ReadAll()
            .Where(d => d.Status == null)
            .Where(d => d.PickupTime > oldClock && d.PickupTime <= newClock)
            .Select(d =>
            {
                var updated = d with { Status = OrderStatus.Processing };

                s_dal.Delivery.Update(updated);
                return updated;
            })
            .ToList();
    }
}
