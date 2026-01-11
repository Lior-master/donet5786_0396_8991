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

        for (int i = 0; i < 40; i++)
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

            // 80% chance d'être actif (1-80 = actif, 81-100 = inactif)
            bool isActive = s_rand.Next(1, 101) <= 80;

            Courier courier = new Courier
            (
                Id: i+100001,
                Name: $"Courier_{i + 1}",
                Phone: $"+100000000{i + 1:D2}",
                Email: $"Courier_{i + 1}@gmail.com",
                Password: "password",
                IsActive: isActive,
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

            var adress = addresses[s_rand.Next(0,5)];
            Order order = new Order
            (
                Id: 0,
                CustomerName: $"Customer_{i + 1}",
                CustomerAddress: adress.Street,
                CustomerPhone: $"+200000000{i + 1:D2}",
                Type: Type,
                // use DAL clock to keep all dates consistent with BL clock
                OrderDate: s_dal!.Config.Clock.AddMinutes(-s_rand.Next(0, 48)),
                Latitude: adress.Latitude,
                Longitude: adress.Longitude
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

        // Filter: active, not Director
        var availableCouriers = allCouriers
            .Where(c => c.IsActive && c.Administrator != Administrator.Director)
            .ToList();

        if (orders.Count == 0 || availableCouriers.Count == 0)
        {
            Console.WriteLine("No orders or available couriers to create deliveries.");
            return;
        }

        // Create deliveries with various statuses
        int attempts = 0;
        int created = 0;
        var usedOrders = new HashSet<int>();
        
        while (created < Math.Min(45, orders.Count) && attempts < 1000)
        {
            attempts++;

            // Pick a random order that hasn't been used yet
            var order = orders[s_rand.Next(orders.Count)];
            if (usedOrders.Contains(order.Id)) continue;

            // Pick a random available courier
            var courier = availableCouriers[s_rand.Next(availableCouriers.Count)];

            // Random pickup time - équilibré
            DateTime pickup = order.OrderDate.AddMinutes(s_rand.Next(3, 20)); // 3-20 minutes après commande

            // Determine delivery status and timing
            DO.DeliveredStatus? deliveryStatus;
            DateTime? arrivalTime = null;
            
            // Create different scenarios plus équilibrés:
            // 60% delivered, 20% processing, 15% pending, 5% canceled
            int statusRoll = s_rand.Next(1, 101);
            
            if (statusRoll <= 60) // 60% - Delivered
            {
                deliveryStatus = DO.DeliveredStatus.Delivered;
                
                // Temps de livraison basés sur le type de transport pour être plus réalistes
                int deliveryTime = courier.Transport switch
                {
                    DeliveryTransport.Foot => s_rand.Next(20, 35),      // 20-35 minutes
                    DeliveryTransport.Bike => s_rand.Next(15, 25),      // 15-25 minutes  
                    DeliveryTransport.Motorcycle => s_rand.Next(10, 20), // 10-20 minutes
                    DeliveryTransport.Car => s_rand.Next(8, 18),        // 8-18 minutes
                    _ => s_rand.Next(15, 30)
                };
                
                arrivalTime = pickup.AddMinutes(deliveryTime);
            }
            else if (statusRoll <= 80) // 20% - Canceled (picked up but not delivered yet)
            {
                deliveryStatus = DO.DeliveredStatus.Canceled;
                pickup = s_dal.Config.Clock.AddMinutes(s_rand.Next(0, 10)); // picked up in the past
                arrivalTime = null; // still in transit
            }
            else if (statusRoll <= 95) // 15% - Pending (not picked up yet)
            {
                deliveryStatus = DO.DeliveredStatus.Absent; // not yet started
                // Pickup dans le futur proche pour certains pending
                pickup = s_dal.Config.Clock.AddMinutes(s_rand.Next(2, 15));
                arrivalTime = null;
            }
            else // 5% - Failed
            {
                deliveryStatus = DO.DeliveredStatus.Failed;
                pickup = s_dal.Config.Clock.AddMinutes(s_rand.Next(0, 10));
                arrivalTime = null;
            }

            // Calculate distance for completed deliveries
            double? distance = null;
            if (arrivalTime.HasValue)
            {
                var address = addresses[s_rand.Next(addresses.Length)];
                distance = s_rand.NextDouble() * address.DistanceFromCompany + 0.8;
            }

            // Create delivery
            Delivery delivery = new Delivery
            (
                Id: 0,
                OrderId: order.Id,
                CourierId: courier.Id,
                PickupTime: pickup,
                Transport: courier.Transport,
                ArrivalTime: arrivalTime,
                Distance: distance,
                DeliveredStatus: deliveryStatus
            );

            s_dal!.Delivery.Create(delivery);
            usedOrders.Add(order.Id);
            created++;
        }

        Console.WriteLine($"Created {created} deliveries with various statuses.");
    }

    public static Adresses[] addresses = new Adresses[]
    {
        new Adresses("2, Kadish Luz, Holyland", 31.7587739, 35.1842047, 2.2, 2.6, 3.3),
        new Adresses("21 HaVaad Leumi israel",31.76577, 35.1931724, 1.5, 12.0, 3.7),
        new Adresses("31 HaRav Frank", 31.7633132, 35.186651, 1.1, 18.0, 2.5),
        new Adresses("73 HaRav Uziel", 31.7667718, 35.1858274, 0.9, 24.0, 1.8),
        new Adresses("87 Arieh Ben Eliezer", 31.7371065, 35.1969742, 1.9, 30.0, 3.3)
    };

    public static Adresses CompanyAdress = new Adresses("22 HaMeyasdim jerusalem", 31.7783596, 35.1871059, 0.0, 0.0, 0.0);

    public static void Do() 
    {
        s_dal = DalApi.Factory.Get;

        Console.WriteLine("Reset Configaration values and List values...");
        s_dal.ResetDB();

        // Initialize config to consistent values so BL AdminManager.Now is sane
        try
        {
            // set company address and coordinates (avoid (0,0))
            s_dal.Config.CompanyAdress = CompanyAdress.Street;
            s_dal.Config.Latitude = CompanyAdress.Latitude;
            s_dal.Config.Longitude = CompanyAdress.Longitude;

            // set the central clock to the current system time
            s_dal.Config.Clock = DateTime.Now;

            // Ajuster les paramètres pour avoir un équilibre réaliste
            if (s_dal.Config.MaxTimeDelivery == TimeSpan.Zero)
                s_dal.Config.MaxTimeDelivery = TimeSpan.FromMinutes(45); // 45 minutes
            if (s_dal.Config.RiskRange == TimeSpan.Zero)
                s_dal.Config.RiskRange = TimeSpan.FromMinutes(8); // 8 minutes avant la limite
        }
        catch
        {
            // best-effort, never crash init
        }

        Console.WriteLine("Initializing Delivery list...");
        createCouriers();
        createOrders();
        createDeliveries();
    }
}


