using DalApi;

namespace Helpers;

internal static class OrderManager
{
    private static IDal s_dal = Factory.Get;
    
    internal static BO.Order? GetOrder(int id)
    {
        DO.Order doOrder;
        doOrder = s_dal.Order.Read(id) ?? throw new BO.BlDoesNotExistException($"Order with ID {id} does not exist.");

        return new BO.Order
        {
            Id = doOrder.Id,
            CustomerName = doOrder.CustomerName,
            CustomerAddress = doOrder.CustomerAddress,
            CustomerPhone = doOrder.CustomerPhone,
            OrderDate = doOrder.OrderDate,
            OrderDescription = doOrder.Description,
            Latitude = doOrder.Latitude ?? 0,
            Longitude = doOrder.Longitude ?? 0,
            Fragility = (BO.FragilityLevel?)doOrder.Fragility,
        };
    }

}
