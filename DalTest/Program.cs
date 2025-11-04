using Dal;
using DalApi;

namespace DalTest;

internal class Program
{
    private static ICourier? s_dalCourier = new CourierImplementation();
    private static IDelivery? s_dalDelivery = new DeliveryImplementation();
    private static IOrder? s_dalOrder = new OrderImplementation();
    private static IConfig? s_dalConfig = new ConfigImplementation();

    private enum Menu
    {
        Exit,

    }

    public void Main(string[] args)
    {
        try
        {
            // Your code here
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }
}
