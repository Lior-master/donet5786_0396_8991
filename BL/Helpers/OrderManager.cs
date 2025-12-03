using System;
using System.Collections.Generic;
using System.Linq;
using BlApi;
using BO;
using DalApi;
using DO;

namespace BlImplementation;

internal class OrderManager : IOrder
{
    private readonly IDal _dal = Factory.Get();

    /* ============================================================
       1. DÉTAILS D’UNE COMMANDE (מסד "ניהול הזמנה בודדת")
       ============================================================ */
    public Order GetOrderDetails(int id)
    {
        var orderDo = _dal.Order.Read(id)
            ?? throw new KeyNotFoundException($"Order {id} not found.");

        var deliveriesDo = _dal.Delivery.ReadAll(d => d.OrderId == id);
        var couriersDo = _dal.Courier.ReadAll();

        return ConvertToBoOrder(orderDo, deliveriesDo, couriersDo);
    }

    /* ============================================================
       2. LISTE DES COMMANDES (מסד "ניהול הזמנות")
       ============================================================ */
    public IEnumerable<OrderInList> GetOrdersList(Func<OrderInList, bool>? filter = null)
    {
        var ordersDo = _dal.Order.ReadAll();
        var deliveries = _dal.Delivery.ReadAll();
        var couriers = _dal.Courier.ReadAll();

        var list = ordersDo
            .Select(o => ConvertToOrderInList(o, deliveries, couriers));

        return filter is null ? list : list.Where(filter);
    }

    /* ============================================================
       3. RÉSUMÉ PAR STATUT (מסד "ניהול ראשי")
       ============================================================ */
    public IEnumerable<IGrouping<OrderStatus, OrderInList>> GetOrderSummary()
    {
        return GetOrdersList().GroupBy(o => o.Status);
    }

    /* ============================================================
       4. MÀJ DÉTAILS COMMANDE
       ============================================================ */
    public void UpdateOrderDetails(Order order)
    {
        var existing = _dal.Order.Read(order.Id)
            ?? throw new KeyNotFoundException($"Order {order.Id} not found.");

        // On ne touche qu’aux infos client + description + fragilité.
        var updated = existing with
        {
            CustomerName = order.CustomerName,
            CustomerAddress = order.CustomerAddress,
            CustomerPhone = order.CustomerPhone,
            Latitude = order.Latitude,
            Longitude = order.Longitude,
            Fragility = order.Fragility is null ? null : (FragilityLevel?)order.Fragility,
            Description = order.OrderDescription
        };

        _dal.Order.Update(updated);
    }

    /* ============================================================
       5. ANNULER COMMANDE
       ============================================================ */
    public void CancelOrder(int id)
    {
        var existing = _dal.Order.Read(id)
            ?? throw new KeyNotFoundException($"Order {id} not found.");

        if (existing.Status == DO.OrderStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel a delivered order.");

        var updated = existing with { Status = DO.OrderStatus.Canceled };
        _dal.Order.Update(updated);
    }

    /* ============================================================
       6. SUPPRIMER COMMANDE (pour BlTest)
       ============================================================ */
    public void DeleteOrder(int id)
    {
        _dal.Order.Delete(id);
    }

    /* ============================================================
       7. AJOUTER COMMANDE
       ============================================================ */
    public void AddOrder(Order order)
    {
        // Très basique : on prend les infos BO et on fait un DO.Order
        var doOrder = new DO.Order(
            Id: order.Id, // à toi de gérer l’ID côté DAL
            Status: DO.OrderStatus.Pending,
            CustomerName: order.CustomerName,
            CustomerAddress: order.CustomerAddress,
            CustomerPhone: order.CustomerPhone,
            OrderDate: order.OrderDate,
            size: order.Volume,
            weight: order.Weight,
            Latitude: order.Latitude,
            Longitude: order.Longitude,
            Fragility: order.Fragility is null ? null : (FragilityLevel?)order.Fragility,
            Description: order.OrderDescription
        );

        _dal.Order.Create(doOrder);
    }

    /* ============================================================
       8. FIN DE TRAITEMENT D’UNE COMMANDE
       ============================================================ */
    public void FinishOrderHandling(int deliveryId, DeliveredStatus deliveredStatus)
    {
        var delivery = _dal.Delivery.Read(deliveryId)
            ?? throw new KeyNotFoundException($"Delivery {deliveryId} not found.");

        var order = _dal.Order.Read(delivery.OrderId)
            ?? throw new KeyNotFoundException($"Order {delivery.OrderId} not found.");

        if (delivery.ArrivalTime != null)
            throw new InvalidOperationException("Delivery already finished.");

        // on marque la livraison comme terminée
        var newDelivery = delivery with
        {
            ArrivalTime = DateTime.Now,
            Status = deliveredStatus switch
            {
                DeliveredStatus.Delivered => DO.OrderStatus.Delivered,
                DeliveredStatus.Canceled => DO.OrderStatus.Canceled,
                DeliveredStatus.Rejected => DO.OrderStatus.Returned,
                _ => DO.OrderStatus.Processing
            }
        };
        _dal.Delivery.Update(newDelivery);

        // On met l’order au bon statut si livré ou annulé
        var newOrderStatus = newDelivery.Status ?? order.Status;
        var newOrder = order with { Status = newOrderStatus };
        _dal.Order.Update(newOrder);
    }

    /* ============================================================
       9. LISTE DES LIVRAISONS FERMÉES D’UN COURSIER
       ============================================================ */
    public IEnumerable<ClosedDeliveryInList> GetClosedDeliveriesForCourier(int courierId)
    {
        var deliveries = _dal.Delivery.ReadAll(d => d.CourierId == courierId && d.ArrivalTime != null);
        var orders = _dal.Order.ReadAll();

        return deliveries.Select(d =>
        {
            var order = orders.First(o => o.Id == d.OrderId);

            return new ClosedDeliveryInList
            {
                DeliveryId = d.Id,
                OrderId = d.OrderId,
                OrderType = OrderType.Standard, // valeur par défaut (tu peux changer)
                CustomerAdress = order.CustomerAddress,
                DeliveryTransport = d.Transport,
                ActualDistance = d.Distance,
                DeliveryTotalTime = (d.ArrivalTime ?? DateTime.Now) - d.PickupTime,
                DeliveredStatus = d.Status switch
                {
                    DO.OrderStatus.Delivered => DeliveredStatus.Delivered,
                    DO.OrderStatus.Canceled => DeliveredStatus.Canceled,
                    DO.OrderStatus.Returned => DeliveredStatus.Rejected,
                    _ => DeliveredStatus.Failed
                }
            };
        });
    }

    /* ============================================================
       10. LISTE DES COMMANDES OUVERTES POUR CHOIX PAR COURSIER
       ============================================================ */
    public IEnumerable<OpenOrderInList> GetOpenOrdersForCourier(int courierId)
    {
        var courier = _dal.Courier.Read(courierId)
            ?? throw new KeyNotFoundException($"Courier {courierId} not found.");
        var orders = _dal.Order.ReadAll(o => o.Status == DO.OrderStatus.Pending);
        var deliveries = _dal.Delivery.ReadAll();

        return orders.Select(o =>
        {
            // vrai “ouvert” = pas encore de livraison livrée
            var orderDeliveries = deliveries.Where(d => d.OrderId == o.Id);
            var hasClosed = orderDeliveries.Any(d => d.ArrivalTime != null);

            if (hasClosed)
                return null!; // sera filtré ensuite

            return new OpenOrderInList
            {
                CourierId = null,
                OrderId = o.Id,
                OrderType = OrderType.Standard,
                Fragility = o.Fragility is null ? null : (FragilityLevel?)o.Fragility,
                CustomerAddress = o.CustomerAddress,
                BirdDistance = 0,          // pas de calcul de distance ici
                Distance = null,       // idem
                AddedTime = DateTime.Now - o.OrderDate,
                ScheduleStatus = ScheduleStatus.OnTime,
                EstimatedDeliveryTime = TimeSpan.Zero,
                MaxDeliveredTime = o.OrderDate  // valeur simple
            };
        })
        .Where(x => x != null)!;
    }

    /* ============================================================
       11. CHOISIR UNE COMMANDE POUR UN COURSIER (VERSION SIMPLE)
       ============================================================ */
    public OpenOrderInList? ChooseOrderForCourier(int courierId)
    {
        return GetOpenOrdersForCourier(courierId).FirstOrDefault();
    }

    /* ============================================================
       HELPERS PRIVÉS : CONVERSIONS DO → BO
       ============================================================ */

    private static Order ConvertToBoOrder(
        DO.Order order,
        IEnumerable<DO.Delivery> deliveries,
        IEnumerable<DO.Courier> couriers)
    {
        var deliveriesForOrder = deliveries.Where(d => d.OrderId == order.Id);

        var deliveriesBo = deliveriesForOrder
            .Select(d =>
            {
                var courier = couriers.FirstOrDefault(c => c.Id == d.CourierId);

                return new DeliveryPerOrderInList
                {
                    DeliveryId = d.Id,
                    CourierId = d.CourierId,
                    Name = courier?.Name ?? string.Empty,
                    OrderType = OrderType.Standard,
                    PickupTime = d.PickupTime,
                    OrderStatus = d.Status is null ? null : (OrderStatus?)d.Status,
                    ArrivalTime = d.ArrivalTime
                };
            })
            .ToList();

        return new Order
        {
            Id = order.Id,
            Type = OrderType.Standard,
            OrderDescription = order.Description,
            CustomerAddress = order.CustomerAddress,
            Latitude = order.Latitude ?? 0,
            Longitude = order.Longitude ?? 0,
            Distance = 0, // pas de calcul automatique
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            Weight = order.weight,
            Fragility = order.Fragility is null ? null : (FragilityLevel?)order.Fragility,
            Volume = order.size,
            OrderDate = order.OrderDate,
            ArrivalDateEstimeted = null,
            ArrivalDateMax = null,
            Status = (OrderStatus)order.Status,
            ScheduleStatus = ScheduleStatus.OnTime,
            ArrivalTimeEstimeted = TimeSpan.Zero,
            DeliveriesPerOrder = deliveriesBo
        };
    }

    private static OrderInList ConvertToOrderInList(
        DO.Order order,
        IEnumerable<DO.Delivery> deliveries,
        IEnumerable<DO.Courier> couriers)
    {
        var deliveriesForOrder = deliveries.Where(d => d.OrderId == order.Id).ToList();
        var lastDelivery = deliveriesForOrder.OrderByDescending(d => d.PickupTime).FirstOrDefault();

        var endTime = lastDelivery?.ArrivalTime ?? DateTime.Now;
        var orderEndTime = endTime - order.OrderDate;
        var treatmentEndTime = lastDelivery is null
            ? TimeSpan.Zero
            : (lastDelivery.ArrivalTime ?? DateTime.Now) - lastDelivery.PickupTime;

        var courierCount = deliveriesForOrder
            .Select(d => d.CourierId)
            .Distinct()
            .Count();

        return new OrderInList
        {
            DeliveryId = lastDelivery?.Id,
            OrderId = order.Id,
            Type = OrderType.Standard,
            Distance = 0, // ici aussi, pas de calcul
            Status = (OrderStatus)order.Status,
            ScheduleStatus = ScheduleStatus.OnTime,
            OrderEndTime = orderEndTime,
            TreatmentEndTime = treatmentEndTime,
            NumberOfCouriers = courierCount
        };
    }
}
