namespace DalTest;

using DO;
using DalApi;

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

    }

    private static void createCouriers()
    {
        for (int i = 0; i < 25; i++)
        {
            var transport = (DeliveryTransport)s_rand.Next(0, 4);

            double maxDistance = transport switch
            {
                DeliveryTransport.Foot => s_rand.Next(0, 3),   // 0–2 km
                DeliveryTransport.Bike => s_rand.Next(0, 8),   // 0–7 km
                DeliveryTransport.Motorcycle => s_rand.Next(0, 15), // 0–14 km
                DeliveryTransport.Car => s_rand.Next(0, 50), // 0–49 km
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
                CustomerAddress: $"Address_{i + 1}",
                CustomerPhone: $"+200000000{i + 1:D2}",
                Status: status,
                OrderDate: DateTime.Now.AddHours(-s_rand.Next(0, 48))
            );
            s_dalOrder!.Create(order);
        }
    }
    private static void createDeliveries()
    {
        for (int i = 0; i < 50; i++)
        {
            int orderId = s_rand.Next(1, 61);  
            int courierId = s_rand.Next(1, 26); 

            DateTime pickupTime = DateTime.Now.AddMinutes(-s_rand.Next(0, 1440));

            Delivery delivery = new Delivery
            (
                Id: 0,
                OrderId: orderId,
                CourierId: courierId,
                PickupTime: pickupTime,
                Transport: (DeliveryTransport)s_rand.Next(0, 4)
            );

            s_dalDelivery!.Create(delivery);
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
   

}


