namespace DalTest;

using Dal;
using DalApi;
using DO;
using System.Diagnostics.Metrics;

public static class Initialization
{
    private static ICourier? s_dalCourier;
    private static IDelivery? s_dalDelivery;
    private static IOrder? s_dalOrder;
    private static IConfig? s_dalConfig;

    private static readonly Random s_rand = new();

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
                Id: 0,
                Name: $"Courier_{i + 1}",
                Phone: $"+100000000{i + 1:D2}",
                Email: $"Courier_{i + 1}@gmail.com",
                Password: "password",
                IsActive: s_rand.Next(0, 5) % 2 == 0,
                Transport: transport,
                StartDate: DateTime.Now.AddDays(s_rand.Next(-365, 0)),
                MaxDistance: maxDistance
            );

            s_dalCourier!.Create(courier);
        }
    }

    private static void createOrders()
    {
        for (int i = 0; i < 60; i++)
        {
            OrderStatus status;
            if (i < 25)
                status = OrderStatus.Pending;
            else if (i < 37)
                status = OrderStatus.Processing;
            else
                status = (OrderStatus)s_rand.Next(2, 5);

            Order order = new Order
            (
                Id: 0,
                CustomerName: $"Customer_{i + 1}",
                CustomerAddress: addresses[s_rand.Next(0,5)].Street,
                CustomerPhone: $"+200000000{i + 1:D2}",
                Status: status,
                OrderDate: DateTime.Now.AddHours(-s_rand.Next(0, 48))
            );
            s_dalOrder!.Create(order);
        }
    }

    private static void createDeliveries()
    {
        // Keep track of each courier’s last delivery end time
        List<int> courierIds = new List<int>();
        List<DateTime> courierEndTimes = new List<DateTime>();

        for (int i = 0; i < 50; i++)
        {
            // 1️ Choose a random order
            int orderId = s_rand.Next(1, 60);
            Order? order = s_dalOrder!.Read(orderId);
            if (order == null)
                continue;

            // 2️ Find the customer's address
            Adresses? addr = null;
            foreach (var a in addresses)
            {
                if (a.Street == order.CustomerAddress)
                {
                    addr = a;
                    break;
                }
            }
            if (addr == null)
                continue;

            // 3️ Find couriers who can handle this order
            List<Courier> possibleCouriers = new List<Courier>();

            foreach (var courier in s_dalCourier!.ReadAll())
            {
                if (courier != null && courier.IsActive)
                {
                    double distance = 0;

                    // Choose the correct distance according to transport type
                    if (courier.Transport == DeliveryTransport.Foot)
                        distance = addr.DistanceWalkingFromCompany;
                    else if (courier.Transport == DeliveryTransport.Bike)
                        distance = addr.DistanceWalkingFromCompany;
                    else if (courier.Transport == DeliveryTransport.Motorcycle)
                        distance = addr.DistanceCarFromCompany;
                    else if (courier.Transport == DeliveryTransport.Car)
                        distance = addr.DistanceCarFromCompany;
                    else
                        distance = addr.DistanceFromCompany;

                    // Check if the courier can deliver this distance
                    if (courier.MaxDistance >= distance)
                        possibleCouriers.Add(courier);
                }
            }

            if (possibleCouriers.Count == 0)
                continue;

            // 4️ Pick a random eligible courier
            Courier chosen = possibleCouriers[s_rand.Next(possibleCouriers.Count)];

            // 5️ Calculate distance and delivery duration
            double deliveryDistance = 0;
            double deliveryTimeMinutes = 0;

            if (chosen.Transport == DeliveryTransport.Foot)
            {
                deliveryDistance = addr.DistanceWalkingFromCompany;
                deliveryTimeMinutes = deliveryDistance * 15; // about 4 km/h
            }
            else if (chosen.Transport == DeliveryTransport.Bike)
            {
                deliveryDistance = addr.DistanceWalkingFromCompany;
                deliveryTimeMinutes = deliveryDistance * 6; // about 10 km/h
            }
            else if (chosen.Transport == DeliveryTransport.Motorcycle)
            {
                deliveryDistance = addr.DistanceCarFromCompany;
                deliveryTimeMinutes = deliveryDistance * 2; // about 30 km/h
            }
            else if (chosen.Transport == DeliveryTransport.Car)
            {
                deliveryDistance = addr.DistanceCarFromCompany;
                deliveryTimeMinutes = deliveryDistance * 1.5; // about 40 km/h
            }
            else
            {
                deliveryDistance = addr.DistanceFromCompany;
                deliveryTimeMinutes = deliveryDistance * 10;
            }

            // 6️ Determine pickup time
            DateTime pickupTime = DateTime.Now.AddMinutes(-s_rand.Next(0, 1440));

            // Check if this courier already has a previous delivery
            bool found = false;
            for (int j = 0; j < courierIds.Count; j++)
            {
                if (courierIds[j] == chosen.Id)
                {
                    found = true;
                    // If the courier is still busy, delay the next pickup
                    if (pickupTime < courierEndTimes[j])
                    {
                        pickupTime = courierEndTimes[j].AddMinutes(10); // 10 min break
                    }
                    // Update this courier's last delivery end time
                    courierEndTimes[j] = pickupTime.AddMinutes(deliveryTimeMinutes);
                    break;
                }
            }

            // If this courier has no previous deliveries, add them to the tracking lists
            if (!found)
            {
                courierIds.Add(chosen.Id);
                courierEndTimes.Add(pickupTime.AddMinutes(deliveryTimeMinutes));
            }

            // 7️ Create the delivery
            Delivery delivery = new Delivery
            (
                Id: 0,
                OrderId: orderId,
                CourierId: chosen.Id,
                PickupTime: pickupTime,
                Transport: chosen.Transport,
                Distance: deliveryDistance,
                ArrivalTime: pickupTime.AddMinutes(deliveryTimeMinutes)
            );

            // 8️ Save the delivery in DAL
            s_dalDelivery!.Create(delivery);
        }
    }

    private static Adresses[] addresses = new Adresses[]
    {
        new Adresses("2 Kadish Luz St", 31.759170644410922, 35.18416389561243, 2.2, 2.6, 3.3),
        new Adresses("21 Vaad Haleumi St",31.76503763226389, 35.19018701095478, 1.5, 12.0, 3.7),
        new Adresses("42 Bayit Vagan St", 31.768730189008583, 35.184873153283796, 1.1, 18.0, 2.5),
        new Adresses("24 Ouziel St", 31.770329906428557, 35.1847366055818, 0.9, 24.0, 1.8),
        new Adresses("30 Barouh Duvdevani St", 31.761875305999634, 35.19177485143465, 1.9, 30.0, 3.3)
    };

    public static Adresses CompanyAdress = new Adresses("22 Hameyasdim St", 31.778449894212013, 35.18761502733661, 0.0, 0.0, 0.0);
   
    public static void Do(ICourier? dalCourier, IOrder? dalOrder, IDelivery? dalDelivery, IConfig? dalConfig)
    {
        s_dalCourier = dalCourier ?? throw new NullReferenceException("DAL object cannot be null!");
        s_dalOrder = dalOrder ?? throw new NullReferenceException("DAL object cannot be null!");
        s_dalDelivery = dalDelivery ?? throw new NullReferenceException("DAL object cannot be null!");
        s_dalConfig = dalConfig ?? throw new NullReferenceException("DAL object cannot be null!");

        Console.WriteLine("Reset Configaration values and List values...");
        s_dalConfig.Reset();
        s_dalCourier.DeleteAll();
        s_dalOrder.DeleteAll();
        s_dalDelivery.DeleteAll();

        Console.WriteLine("Initializing Delivery list...");
        createCouriers();
        createOrders();
        createDeliveries();
    }
}


