using System;
using System.Linq;
using BlApi;
using BO;

namespace BlTest;

internal class Program
{
    // Assumes a BlApi.Factory similar to DalApi.Factory exists and returns an IBl implementation.
    static readonly IBl s_bl = Factory.Get();

    private static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== BL Integration Smoke Test ===");

            // Initialize BL / DB via Admin
            Console.WriteLine("Initializing DB via BL...");
            s_bl.Admin.InitializeDB();
            Console.WriteLine("DB initialized.");

            // Print current clock and config
            Console.WriteLine("Clock: " + s_bl.Admin.GetClock());
            var cfg = s_bl.Admin.GetConfig();
            Console.WriteLine("Config:");
            Console.WriteLine(cfg == null ? "<null>" : cfg.ToString());

            // List couriers (uses the 'Student' property name in IBl that represents courier manager)
            Console.WriteLine("\nCouriers list:");
            var couriers = s_bl.Courier.GetCouriersList(0, null, null);
            foreach (var c in couriers ?? Enumerable.Empty<BO.CourierInList>())
                Console.WriteLine(c);

            // List orders via Course property
            Console.WriteLine("\nOrders list (orderInLists):");
            var orders = s_bl.Order.orderInLists(0, null, null, null);
            foreach (var o in orders ?? Enumerable.Empty<BO.OrderInList>())
                Console.WriteLine(o);

            // Try read a courier details if any courier exists
            var firstCourier = couriers?.FirstOrDefault();
            if (firstCourier is not null)
            {
                Console.WriteLine($"\nReading details for courier id={firstCourier.Id}:");
                var details = s_bl.Courier.GetCourierDetails(0, firstCourier.Id);
                Console.WriteLine(details);
            }
            else
            {
                Console.WriteLine("\nNo couriers available to show details.");
            }

            Console.WriteLine("\nBL smoke test finished.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred during BL test: " + ex);
        }
    }
}
