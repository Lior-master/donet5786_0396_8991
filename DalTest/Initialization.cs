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
                StartDate: DateTime.Now.AddDays(-s_rand.Next(0, 365)),
                MaxDistance: maxDistance
            );

            s_dalCourier!.Create(courier);
        }
    }

    private static void createOrders()
    {
        for (int i = 0; i < 50; i++)
        {
            Order order = new Order
            (
                Id: 0,
                CustomerName: $"Customer_{i + 1}",
                CustomerAddress: $"Address_{i + 1}",
                OrderTime: DateTime.Now.AddMinutes(-s_rand.Next(0, 1440)),
                Weight: s_rand.Next(1, 10),
                Status: OrderStatus.Pending);
            s_dalOrder!.Create(order);
        }
    }
}
