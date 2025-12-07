using System;
using System.Collections.Generic;
using System.Linq;
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
            Console.WriteLine("4. Test Admin / Config");
            Console.WriteLine("5. Other Order/Courier helpers");
            Console.WriteLine("6. Set Director ID");
            Console.WriteLine("7. Reinitialize data (quick)"); // NEW: quick init option
            Console.WriteLine("0. Exit");
            Console.Write("Choose: ");

            if (!int.TryParse(Console.ReadLine(), out int mainChoice))
                continue;

            try
            {
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
                        TestAdmin();
                        break;
                    case 5:
                        TestHelpers();
                        break;
                    case 6:
                        SetDirectorId();
                        break;
                    case 7:
                        QuickInitializeData();
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
        Console.WriteLine("3. Add Order");
        Console.WriteLine("4. Update Order");
        Console.WriteLine("5. Cancel Order");
        Console.WriteLine("6. Remove Order");
        Console.WriteLine("7. Get Orders Summary");
        Console.WriteLine("8. Get Orders (orderInLists)");
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
                case 3:
                    AddOrder();
                    break;
                case 4:
                    UpdateOrder();
                    break;
                case 5:
                    CancelOrder();
                    break;
                case 6:
                    RemoveOrder();
                    break;
                case 7:
                    GetOrdersBySummary();
                    break;
                case 8:
                    ListOrders(); // same as orderInLists sample
                    break;
                case 0:
                    return;
                default:
                    break;
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

    private static void AddOrder()
    {
        Console.WriteLine("Enter minimal order fields (press Enter to accept default):");
        var order = new BO.Order();
        Console.Write("Customer Name: ");
        var name = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(name)) order.CustomerName = name;
        Console.Write("Customer Address: ");
        var addr = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(addr)) order.CustomerAddress = addr;
        Console.Write("Customer Phone: ");
        var phone = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(phone)) order.CustomerPhone = phone;

        s_bl.Order.AddOrder(TestRequesterId, order);
        Console.WriteLine("Order added. Press Enter...");
        Console.ReadLine();
    }

    private static void UpdateOrder()
    {
        Console.Write("Order ID to update: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;

        var current = s_bl.Order.GetOrderDetails(TestRequesterId, id);
        Console.WriteLine("Current: " + current);
        Console.Write("New Customer Name (blank = keep): ");
        var name = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(name)) current.CustomerName = name;
        Console.Write("New Address (blank = keep): ");
        var addr = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(addr)) current.CustomerAddress = addr;

        s_bl.Order.UpdateOrderDetails(TestRequesterId, current);
        Console.WriteLine("Order updated. Press Enter...");
        Console.ReadLine();
    }

    private static void CancelOrder()
    {
        Console.Write("Order ID to cancel: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        s_bl.Order.CancelOrder(TestRequesterId, id);
        Console.WriteLine("Order canceled. Press Enter...");
        Console.ReadLine();
    }

    private static void RemoveOrder()
    {
        Console.Write("Order ID to remove: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        s_bl.Order.RemoveOrder(TestRequesterId, id);
        Console.WriteLine("Order removed. Press Enter...");
        Console.ReadLine();
    }

    private static void GetOrdersBySummary()
    {
        var summary = s_bl.Order.GetOrdersBySummary(TestRequesterId);
        Console.WriteLine("Orders by summary:");
        foreach (var v in summary) Console.WriteLine(v);
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
        Console.WriteLine("3. Add Courier");
        Console.WriteLine("4. Update Courier");
        Console.WriteLine("5. Remove Courier");
        Console.WriteLine("6. Promote To Director");
        Console.WriteLine("7. Login (username/password)");
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
                case 3:
                    AddCourier();
                    break;
                case 4:
                    UpdateCourier();
                    break;
                case 5:
                    RemoveCourier();
                    break;
                case 6:
                    PromoteCourier();
                    break;
                case 7:
                    LoginCourier();
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

    private static void AddCourier()
    {
        Console.WriteLine("Enter courier fields (press Enter to accept default):");
        var c = new BO.Courier();
        Console.Write("Name: ");
        var name = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(name)) c.Name = name;
        Console.Write("Phone: ");
        var phone = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(phone)) c.Phone = phone;
        Console.Write("Email: ");
        var email = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(email)) c.Email = email;
        Console.Write("Password: ");
        var pwd = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(pwd)) c.Password = pwd;
        Console.Write("IsActive (y/n): ");
        var a = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(a) && (a.StartsWith("y", StringComparison.OrdinalIgnoreCase))) c.IsActive = true;

        s_bl.Courier.addCourier(TestRequesterId, c);
        Console.WriteLine("Courier added. Press Enter...");
        Console.ReadLine();
    }

    private static void UpdateCourier()
    {
        Console.Write("Courier ID to update: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        var cur = s_bl.Courier.GetCourierDetails(TestRequesterId, id);
        Console.WriteLine("Current: " + cur);
        Console.Write("New Name (blank = keep): ");
        var name = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(name)) cur.Name = name;
        Console.Write("New Phone (blank = keep): ");
        var phone = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(phone)) cur.Phone = phone;

        s_bl.Courier.UpdateCourier(TestRequesterId, cur);
        Console.WriteLine("Courier updated. Press Enter...");
        Console.ReadLine();
    }

    private static void RemoveCourier()
    {
        Console.Write("Courier ID to remove: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        s_bl.Courier.removeCourier(TestRequesterId, id);
        Console.WriteLine("Courier removed. Press Enter...");
        Console.ReadLine();
    }

    private static void PromoteCourier()
    {
        Console.Write("Courier ID to promote: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        s_bl.Courier.PromoteToDirector(TestRequesterId, id);
        Console.WriteLine("Courier promoted. Press Enter...");
        Console.ReadLine();
    }

    private static void LoginCourier()
    {
        Console.Write("Username: ");
        var u = Console.ReadLine() ?? string.Empty;
        Console.Write("Password: ");
        var p = Console.ReadLine() ?? string.Empty;
        var admin = s_bl.Courier.Login(u, p);
        Console.WriteLine("Login result: " + admin);
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
        Console.WriteLine("3. Get Open Orders For Courier");
        Console.WriteLine("4. Get Closed Deliveries For Courier");
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
                case 3:
                    GetOpenOrdersForCourier();
                    break;
                case 4:
                    GetClosedDeliveriesForCourier();
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

    private static void GetOpenOrdersForCourier()
    {
        Console.Write("Courier ID: ");
        if (!int.TryParse(Console.ReadLine(), out int courierId)) return;
        var open = s_bl.Order.GetOpenOrdersForCourier(TestRequesterId, courierId, null, null);
        foreach (var o in open) Console.WriteLine(o);
        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    private static void GetClosedDeliveriesForCourier()
    {
        Console.Write("Courier ID: ");
        if (!int.TryParse(Console.ReadLine(), out int courierId)) return;
        var closed = s_bl.Order.GetClosedDeliveriesForCourier(TestRequesterId, courierId, null, null);
        foreach (var c in closed) Console.WriteLine(c);
        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    /* ============================================
       ADMIN / CONFIG
       ============================================ */

    private static void TestAdmin()
    {
        Console.Clear();
        Console.WriteLine("=== TEST ADMIN / CONFIG ===");
        Console.WriteLine("1. Get Config");
        Console.WriteLine("2. Set Config");
        Console.WriteLine("3. Initialize DB");
        Console.WriteLine("4. Reset DB");
        Console.WriteLine("5. Get Clock");
        Console.WriteLine("6. Forward Clock");
        Console.WriteLine("0. Back");
        Console.Write("Choose: ");

        if (!int.TryParse(Console.ReadLine(), out int choice))
            return;

        try
        {
            switch (choice)
            {
                case 1:
                    ShowConfig();
                    break;
                case 2:
                    SetConfig();
                    break;
                case 3:
                    s_bl.Admin.InitializeDB();
                    Console.WriteLine("DB Initialized. Press Enter...");
                    Console.ReadLine();
                    break;
                case 4:
                    s_bl.Admin.ResetDB();
                    Console.WriteLine("DB Reset. Press Enter...");
                    Console.ReadLine();
                    break;
                case 5:
                    Console.WriteLine("Clock: " + s_bl.Admin.GetClock());
                    Console.WriteLine("Press Enter...");
                    Console.ReadLine();
                    break;
                case 6:
                    ForwardClock();
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

    private static void ShowConfig()
    {
        var cfg = s_bl.Admin.GetConfig();
        Console.WriteLine(cfg);
        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    private static void SetConfig()
    {
        var cfg = s_bl.Admin.GetConfig() ?? new BO.Config();
        Console.WriteLine("Current config: " + cfg);
        Console.Write("CarSpeed (blank = keep): ");
        var s = Console.ReadLine();
        if (double.TryParse(s, out double car)) cfg.CarSpeed = car;
        Console.Write("MotorcycleSpeed (blank = keep): ");
        s = Console.ReadLine();
        if (double.TryParse(s, out double mc)) cfg.MotorcycleSpeed = mc;
        Console.Write("BikeSpeed (blank = keep): ");
        s = Console.ReadLine();
        if (double.TryParse(s, out double b)) cfg.BikeSpeed = b;
        Console.Write("WalkingSpeed (blank = keep): ");
        s = Console.ReadLine();
        if (double.TryParse(s, out double w)) cfg.WalkingSpeed = w;
        Console.Write("MaxDeliveryTime (minutes, blank = keep): ");
        s = Console.ReadLine();
        if (double.TryParse(s, out double m)) cfg.MaxDeliveryTime = TimeSpan.FromMinutes(m);
        Console.Write("InactivityThreshold (days, blank = keep): ");
        s = Console.ReadLine();
        if(double.TryParse(s, out double d)) cfg.InactivityThreshold = TimeSpan.FromDays(d);
        Console.Write("MaxDistance (km, blank = keep): ");
        s = Console.ReadLine();
        if (double.TryParse(s, out double md)) cfg.MaxDistance = md;

        s_bl.Admin.SetConfig(cfg);
        Console.WriteLine("Config set. Press Enter...");
        Console.ReadLine();
    }

    private static void ForwardClock()
    {
        Console.Write("Years to advance (0 to skip): ");
        _ = int.TryParse(Console.ReadLine(), out int years);

        Console.Write("Months to advance (0 to skip): ");
        _ = int.TryParse(Console.ReadLine(), out int months);

        Console.Write("Days to advance (0 to skip): ");
        _ = int.TryParse(Console.ReadLine(), out int days);

        Console.Write("Hours to advance (0 to skip): ");
        _ = int.TryParse(Console.ReadLine(), out int hours);

        Console.Write("Minutes to advance (0 to skip): ");
        _ = int.TryParse(Console.ReadLine(), out int minutes);

        int totalMonths = years * 12 + months;

        Console.WriteLine($"Will advance clock by: {years} year(s), {months} month(s), {days} day(s), {hours} hour(s), {minutes} minute(s). Confirm (y/n): ");
        var ans = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(ans) || !ans.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Forward cancelled. Press Enter...");
            Console.ReadLine();
            return;
        }

        try
        {
            // Use the BL Admin method which advances by one unit per call.
            // Years -> converted to months (AddMonths)
            for (int i = 0; i < totalMonths; i++)
                s_bl.Admin.ForwardClock(BO.TimeUnit.Month);

            for (int i = 0; i < days; i++)
                s_bl.Admin.ForwardClock(BO.TimeUnit.Day);

            for (int i = 0; i < hours; i++)
                s_bl.Admin.ForwardClock(BO.TimeUnit.Hour);

            for (int i = 0; i < minutes; i++)
                s_bl.Admin.ForwardClock(BO.TimeUnit.Minute);

            Console.WriteLine("Clock forwarded. New clock: " + s_bl.Admin.GetClock());
        }
        catch (Exception ex)
        {
            Console.WriteLine("Forward failed: " + ex.Message);
        }

        Console.WriteLine("Press Enter...");
        Console.ReadLine();
    }

    /* ============================================
       OTHER HELPERS / TESTS
       ============================================ */

    private static void TestHelpers()
    {
        Console.Clear();
        Console.WriteLine("=== HELPERS ===");
        Console.WriteLine("1. Set Director ID");
        Console.WriteLine("2. Get Open Orders for courier");
        Console.WriteLine("3. Get Closed Deliveries for courier");
        Console.WriteLine("0. Back");
        Console.Write("Choose: ");

        if (!int.TryParse(Console.ReadLine(), out int choice))
            return;

        try
        {
            switch (choice)
            {
                case 1:
                    SetDirectorId();
                    break;
                case 2:
                    GetOpenOrdersForCourier();
                    break;
                case 3:
                    GetClosedDeliveriesForCourier();
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
            // validate that the courier exists
            var _ = s_bl.Courier.GetCouriersList(id, null, null);

            // Persist boss id in config so XML is updated
            var cfg = s_bl.Admin.GetConfig() ?? new BO.Config();
            cfg.BossId = id;
            s_bl.Admin.SetConfig(cfg);

            TestRequesterId = id;
            Console.WriteLine($"Director ID set to {id} and persisted. Press Enter...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set Director ID: {ex.Message}");
            Console.WriteLine("If the ID does not exist in DB, initialize data or add courier with that ID.");
        }
        Console.ReadLine();
    }

    /// <summary>
    /// Quick helper to reinitialize the DB from the main menu (asks for confirmation).
    /// Uses the BL Admin InitializeDB method (same as TestAdmin option).
    /// </summary>
    private static void QuickInitializeData()
    {
        Console.Write("Are you sure you want to reinitialize all data? This will overwrite existing data (y/n): ");
        var ans = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(ans) || !ans.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Initialization cancelled. Press Enter...");
            Console.ReadLine();
            return;
        }

        try
        {
            s_bl.Admin.ResetDB();
            s_bl.Admin.InitializeDB();
            Console.WriteLine("Data reinitialized successfully. Press Enter...");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Initialization failed: " + ex.Message);
            Console.WriteLine("Press Enter...");
        }
        Console.ReadLine();
    }
}
