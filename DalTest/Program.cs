using Dal;
using DalApi;
using DO;

namespace DalTest;

internal class Program
{
    private static ICourier? s_dalCourier = new CourierImplementation();
    private static IDelivery? s_dalDelivery = new DeliveryImplementation();
    private static IOrder? s_dalOrder = new OrderImplementation();
    private static IConfig? s_dalConfig = new ConfigImplementation();

    private enum Menu
    {
        Exit = 0,
        Initialize = 1,
        ShowAllCouriers = 2,
        ShowAllOrders = 3,
        ShowAllDeliveries = 4,
        AddCourier = 5,
        ReadCourier = 6,
        DeleteCourier = 7
    }

    private static void Main(string[] args)
    {
        try
        {
            Menu choice;
            do
            {
                Console.WriteLine("\n--- MAIN MENU ---");
                Console.WriteLine("1. Initialize Data");
                Console.WriteLine("2. Show all Couriers");
                Console.WriteLine("3. Show all Orders");
                Console.WriteLine("4. Show all Deliveries");
                Console.WriteLine("5. Add Courier");
                Console.WriteLine("6. Read Courier by ID");
                Console.WriteLine("7. Delete Courier");
                Console.WriteLine("0. Exit");
                Console.Write("Enter your choice: ");

                choice = (Menu)int.Parse(Console.ReadLine() ?? "0");

                switch (choice)
                {
                    case Menu.Initialize:
                        
                        Initialization.Do(s_dalCourier, s_dalOrder, s_dalDelivery, s_dalConfig);
                        Console.WriteLine("Data initialized successfully!");
                        break;

                    case Menu.ShowAllCouriers:
                        foreach (var c in s_dalCourier!.ReadAll())
                            Console.WriteLine(c);
                        break;

                    case Menu.ShowAllOrders:
                        foreach (var o in s_dalOrder!.ReadAll())
                            Console.WriteLine(o);
                        break;

                    case Menu.ShowAllDeliveries:
                        foreach (var d in s_dalDelivery!.ReadAll())
                            Console.WriteLine(d);
                        break;

                    case Menu.AddCourier:
                        Console.Write("Enter courier name: ");
                        string? name = Console.ReadLine();
                        Console.Write("Enter phone: ");
                        string? phone = Console.ReadLine();
                        Console.Write("Enter email: ");
                        string? email = Console.ReadLine();
                        Console.Write("Enter Id: ");
                        int ID = int.Parse(Console.ReadLine() ?? "0");

                        Courier newCourier = new Courier(
                            Id: ID,
                            Name: name ?? "Unknown",
                            Phone: phone ?? "0000000000",
                            Email: email ?? "none@gmail.com",
                            Password: "password",
                            IsActive: true,
                            Transport: DeliveryTransport.Bike,
                            StartDate: DateTime.Now,
                            MaxDistance: 10
                        );

                        s_dalCourier!.Create(newCourier);
                        Console.WriteLine("Courier added successfully!");
                        break;

                    case Menu.ReadCourier:
                        Console.Write("Enter courier ID: ");
                        int id = int.Parse(Console.ReadLine() ?? "0");
                        Courier? courier = s_dalCourier!.Read(id);
                        if (courier != null)
                            Console.WriteLine(courier);
                        else
                            Console.WriteLine("Courier not found.");
                        break;

                    case Menu.DeleteCourier:
                        Console.Write("Enter courier ID to delete: ");
                        int idDel = int.Parse(Console.ReadLine() ?? "0");
                        s_dalCourier!.Delete(idDel);
                        Console.WriteLine("Courier deleted.");
                        break;

                    case Menu.Exit:
                        Console.WriteLine("Exiting program...");
                        break;

                    default:
                        Console.WriteLine("Invalid choice.");
                        break;
                }

            } while (choice != Menu.Exit);
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }
}
