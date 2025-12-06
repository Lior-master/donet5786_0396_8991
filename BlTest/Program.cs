using System;
using BlApi;
using BO;

namespace BlTest;

internal class Program
{
    // Single BL instance
    static readonly IBl s_bl = BlApi.Factory.Get();

    // For tests we use a mutable requester id (e.g. admin / boss)
    private static int TestRequesterId = 347657991;

    static void Main()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("======== BL TEST ========");
            Console.WriteLine("Current Director ID: " + TestRequesterId);
            Console.WriteLine("1. Test Orders");
            Console.WriteLine("2. Test Couriers");
            Console.WriteLine("3. Test Deliveries");
            Console.WriteLine("4. Set Director ID");
            Console.WriteLine("0. Exit");
            Console.Write("Choose: ");

            if (!int.TryParse(Console.ReadLine(), out int mainChoice))
                continue;

            switch (mainChoice)
            {
                case 1:
                    TestOrders();
                    break;
                case 2:
                    TestCouriers();
                    break;
                case 3:
                    TestDeliveries();
                    break;
                case 4:
                    SetDirectorId();
                    break;
                case 0:
                    return;
            }
        }
    }

    /* ============================================
       ORDERS
       ============================================ */

    private static void TestOrders()
    {
        Console.Clear();
        Console.WriteLine("=== TEST ORDERS ===");
        Console.WriteLine("1. Get Order Details");
        Console.WriteLine("2. List All Orders");
        Console.WriteLine("0. Back");
        Console.Write("Choose: ");

        if (!int.TryParse(Console.ReadLine(), out int choice))
            return;

        try
        {
            switch (choice)
            {
                case 1:
                    GetOrderDetails();
                    break;
                case 2:
                    ListOrders();
                    break;
                case 0:
                    return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            Console.WriteLine("Press Enter...");
            Console.ReadLine();
        }
    }

    private static void GetOrderDetails()
    {
        Console.Write("Order ID: ");
        if (!int.TryParse(Console.ReadLine(), out int id))
            return;

        var order = s_bl.Order.GetOrderDetails(TestRequesterId, id);
        Console.WriteLine("\n--- ORDER DETAILS ---");
        Console.WriteLine(order);
        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    private static void ListOrders()
    {
        // Use the BL interface method that returns order list view models
        var orders = s_bl.Order.orderInLists(TestRequesterId, null, null, null);

        Console.WriteLine("\n--- ORDERS LIST ---");
        foreach (var o in orders)
            Console.WriteLine(o);

        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    /* ============================================
       COURIERS
       ============================================ */

    private static void TestCouriers()
    {
        Console.Clear();
        Console.WriteLine("=== TEST COURIERS ===");
        Console.WriteLine("1. Get Courier Details");
        Console.WriteLine("2. List All Couriers");
        Console.WriteLine("0. Back");
        Console.Write("Choose: ");

        if (!int.TryParse(Console.ReadLine(), out int choice))
            return;

        try
        {
            switch (choice)
            {
                case 1:
                    GetCourierDetails();
                    break;
                case 2:
                    ListCouriers();
                    break;
                case 0:
                    return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            Console.WriteLine("Press Enter...");
            Console.ReadLine();
        }
    }

    private static void GetCourierDetails()
    {
        Console.Write("Courier ID: ");
        if (!int.TryParse(Console.ReadLine(), out int id))
            return;

        var courier = s_bl.Courier.GetCourierDetails(TestRequesterId, id);
        Console.WriteLine("\n--- COURIER DETAILS ---");
        Console.WriteLine(courier);
        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    private static void ListCouriers()
    {
        // null / null → no filter, no specific status
        var couriers = s_bl.Courier.GetCouriersList(TestRequesterId, null, null);

        Console.WriteLine("\n--- COURIERS LIST ---");
        foreach (var c in couriers)
            Console.WriteLine(c);

        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    /* ============================================
       DELIVERIES (simple test)
       ============================================ */

    private static void TestDeliveries()
    {
        Console.Clear();
        Console.WriteLine("=== TEST DELIVERIES ===");
        Console.WriteLine("1. Assign Order To Courier");
        Console.WriteLine("2. Finish Delivery");
        Console.WriteLine("0. Back");
        Console.Write("Choose: ");

        if (!int.TryParse(Console.ReadLine(), out int choice))
            return;

        try
        {
            switch (choice)
            {
                case 1:
                    AssignOrderToCourier();
                    break;
                case 2:
                    FinishDelivery();
                    break;
                case 0:
                    return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            Console.WriteLine("Press Enter...");
            Console.ReadLine();
        }
    }

    private static void AssignOrderToCourier()
    {
        Console.Write("Order ID: ");
        if (!int.TryParse(Console.ReadLine(), out int orderId))
            return;

        Console.Write("Courier ID: ");
        if (!int.TryParse(Console.ReadLine(), out int courierId))
            return;

        s_bl.Order.AssignOrderToCourier(TestRequesterId, orderId, courierId);
        Console.WriteLine("Order assigned to courier.");
        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    private static void FinishDelivery()
    {
        Console.Write("Courier ID: ");
        if (!int.TryParse(Console.ReadLine(), out int courierId))
            return;

        Console.Write("Delivery ID: ");
        if (!int.TryParse(Console.ReadLine(), out int deliveryId))
            return;

        s_bl.Order.FinishOrder(TestRequesterId, courierId, deliveryId);
        Console.WriteLine("Delivery finished.");
        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    private static void SetDirectorId()
    {
        Console.Write("Enter Director ID: ");
        if (!int.TryParse(Console.ReadLine(), out int id))
        {
            Console.WriteLine("Invalid ID format. Press Enter...");
            Console.ReadLine();
            return;
        }

        try
        {
            // quick validation: call a BL method that checks requester existence
            var _ = s_bl.Courier.GetCouriersList(id, null, null);
            TestRequesterId = id;
            Console.WriteLine($"Director ID set to {id}. Press Enter...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set Director ID: {ex.Message}");
            Console.WriteLine("If the ID does not exist in DB, initialize data or add courier with that ID.");
        }
        Console.ReadLine();
    }
}
