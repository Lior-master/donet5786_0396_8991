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
            Courier courier = new Courier
            (
                Id: 0,
                Name: $"Courier_{i + 1}",
                Phone: $"+100000000{i + 1:D2}",
                Email: $"Courier_{i + 1}@gmail.com",
                Password: "password",
                IsActive: s_rand.Next(0, 5) % 2 == 0,
                Transport: (DeliveryTransport)(s_rand.Next(0, 4)),
                StartDate: DateTime.Now.AddDays(-s_rand.Next(0, 365)),
                MaxDistance: s_rand.Next(0, 20));
            s_dalCourier!.Create(courier);
        }
    }
}
