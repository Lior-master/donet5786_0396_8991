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

    }
}
