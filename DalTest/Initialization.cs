namespace DalTest;

using Dal;
using DalApi;
using DO;
using System.Diagnostics.Metrics;

public static class Initialization
{
   
    private static IDal? s_dal;

    private static readonly Random s_rand = new();
    private const int MIN_ID = 200000000;
    private const int MAX_ID = 400000000;


    public class Adresses
    {
        public string Street { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceFromCompany { get; set; }
        public double DistanceWalkingFromCompany { get; set; }
        public double DistanceCarFromCompany { get; set; } 

        public Adresses(string street, double latitude, double longitude, double distanceFromCompany, double distanceWalkingFromCompany, double distanceCarFromCompany)
        {
            Street = street;
            Latitude = latitude;
            Longitude = longitude;
            DistanceFromCompany = distanceFromCompany;
            DistanceWalkingFromCompany = distanceWalkingFromCompany;
            DistanceCarFromCompany = distanceCarFromCompany;
        }
        // methode pour calculer la distance entre deux points geographiques si besoin
    }

    private static void createCouriers()
    {
        s_dal!.Courier.Create(new Courier(
            Id: 347657991,
            Name: "Boss",
            Phone: "+111111111",
            Email: "boss@company.com",
            Password: "admin",
            IsActive: true,
            Transport: DeliveryTransport.Car,
            StartDate: DateTime.Now,
            MaxDistance: 999,
            Administrator: Administrator.Director
        ));

        for (int i = 0; i < 25; i++)
        {
            var transport = (DeliveryTransport)s_rand.Next(0, 4);

            double maxDistance = transport switch
            {
                DeliveryTransport.Foot => s_rand.Next(0, 3),   // 0–2 km
                DeliveryTransport.Bike => s_rand.Next(3, 8),   // 3–7 km
                DeliveryTransport.Motorcycle => s_rand.Next(8, 15), // 8–14 km
                DeliveryTransport.Car => s_rand.Next(15, 50), // 15–49 km
                _ => 1
            };

            Courier courier = new Courier
            (
                Id: i+100001,
                Name: $"Courier_{i + 1}",
                Phone: $"+100000000{i + 1:D2}",
                Email: $"Courier_{i + 1}@gmail.com",
                Password: "password",
                IsActive: s_rand.Next(0, 5) % 2 == 0,
                Transport: transport,
                StartDate: DateTime.Now.AddDays(s_rand.Next(-365, 0)),
                MaxDistance: maxDistance,
                Administrator : Administrator.Courier
            );

            s_dal!.Courier.Create(courier);
        }
    }

    private static void createOrders()
    {
        for (int i = 0; i < 60; i++)
        {
            OrderType Type;
            if (i < 25)
                Type = OrderType.FastFood;
            else if (i < 37)
                Type = OrderType.Pizza;
            else
                Type = (OrderType)s_rand.Next(2, 5);

            Order order = new Order
            (
                Id: 0,
                CustomerName: $"Customer_{i + 1}",
                CustomerAddress: addresses[s_rand.Next(0,5)].Street,
                CustomerPhone: $"+200000000{i + 1:D2}",
                Type: Type,
                OrderDate: DateTime.Now.AddHours(-s_rand.Next(0, 48))
            );
            s_dal!.Order.Create(order);
        }
    }

    private static void createDeliveries()
    {
        // Get all existing orders and couriers
        var orders = s_dal!.Order.ReadAll().ToList();
        var allCouriers = s_dal!.Courier.ReadAll().ToList();
        var deliveriesSnapshot = s_dal!.Delivery.ReadAll().ToList();

        // Filter: active, not Director, and with no open (ArrivalTime == null) deliveries
        var availableCouriers = allCouriers
            .Where(c => c.IsActive && c.Administrator != Administrator.Director
                        && !deliveriesSnapshot.Any(d => d.CourierId == c.Id && d.ArrivalTime == null))
            .ToList();

        if (orders.Count == 0 || availableCouriers.Count == 0)
        {
            Console.WriteLine("No orders or available couriers to create deliveries.");
            return;
        }

        // create up to 50 deliveries but avoid assigning more than one open delivery per courier
        int attempts = 0;
        int created = 0;
        while (created < 50 && created < orders.Count && availableCouriers.Count > 0 && attempts < 1000)
        {
            attempts++;

            // Pick a random order that doesn't already have a processing delivery (best-effort)
            var order = orders[s_rand.Next(orders.Count)];

            // ensure order isn't already assigned in an open delivery
            bool orderHasOpenDelivery = deliveriesSnapshot.Any(d => d.OrderId == order.Id && d.ArrivalTime == null);
            if (orderHasOpenDelivery) continue;

            // Pick a random available courier
            var courier = availableCouriers[s_rand.Next(availableCouriers.Count)];

            // Random pickup time (past 24h)
            DateTime pickup = DateTime.Now.AddMinutes(-s_rand.Next(0, 1440));

            // Create delivery
            Delivery delivery = new Delivery
            (
                Id: 0,
                OrderId: order.Id,
                CourierId: courier.Id,
                PickupTime: pickup,
                Transport: courier.Transport
            );

            s_dal!.Delivery.Create(delivery);

            // update snapshots: add delivery and remove courier from available pool so they won't get another open delivery
            deliveriesSnapshot.Add(delivery);
            availableCouriers.RemoveAll(c => c.Id == courier.Id);

            created++;
        }
    }

    public static Adresses[] addresses = new Adresses[]
    {
        new Adresses("2 Kadish Luz St", 31.759170644410922, 35.18416389561243, 2.2, 2.6, 3.3),
        new Adresses("21 Vaad Haleumi St",31.76503763226389, 35.19018701095478, 1.5, 12.0, 3.7),
        new Adresses("42 Bayit Vagan St", 31.768730189008583, 35.184873153283796, 1.1, 18.0, 2.5),
        new Adresses("24 Ouziel St", 31.770329906428557, 35.1847366055818, 0.9, 24.0, 1.8),
        new Adresses("30 Barouh Duvdevani St", 31.761875305999634, 35.19177485143465, 1.9, 30.0, 3.3)
    };

    public static Adresses CompanyAdress = new Adresses("22 Hameyasdim St", 31.778449894212013, 35.18761502733661, 0.0, 0.0, 0.0);

    public static void Do() 

    {
        s_dal = DalApi.Factory.Get;

        Console.WriteLine("Reset Configaration values and List values...");
        s_dal.ResetDB();

        Console.WriteLine("Initializing Delivery list...");
        createCouriers();
        createOrders();
        createDeliveries();
    }
}


