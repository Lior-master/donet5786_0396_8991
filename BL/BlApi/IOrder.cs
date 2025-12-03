namespace BlApi;

using BO;
using System.Reflection.Metadata.Ecma335;

public interface IOrder
{
    /* --- 1. קבלת פרטי ההזמנה --- */
    /// <summary>
    /// Retourne les détails complets d’une commande (Order BO)
    /// </summary>
    /// <param name="id">ID de la commande</param>
    Order GetOrderDetails(int id);



    /* --- 2. עדכון פרטי ההזמנה --- */
    /// <summary>
    /// Met à jour les informations d’une commande (adresse, client, etc.)
    /// </summary>
    /// <param name="order">BO.Order contenant les infos modifiées</param>
    void UpdateOrderDetails(Order order);



    /* --- 3. ביטול הזמנה (מותר רק כשהטיפול טרם התחיל) --- */
    /// <summary>
    /// Annule la commande totalement (seulement si elle n’a pas encore été traitée)
    /// </summary>
    /// <param name="id">ID de la commande</param>
    void CancelOrder(int id);



    /* --- 4. הוספת הזמנה חדשה --- */
    /// <summary>
    /// Ajoute une nouvelle commande (et crée automatiquement les données DAL nécessaires)
    /// </summary>
    /// <param name="order">BO.Order à créer</param>
    int AddOrder(Order order);



    /* --- 5. סיום טיפול בהזמנה (מסד ”שליחה ראשית”) --- */
    /// <summary>
    /// Marque la fin du traitement de la commande par le livreur (close delivery)
    /// </summary>
    /// <param name="orderId">ID de la commande</param>
    /// <param name="courierId">ID du livreur</param>
    /// <param name="deliveryId">ID de l'objet livraison</param>
    void FinishOrderHandling(int orderId, int courierId, int deliveryId);



    /* --- 6. בחירת הזמנה לטיפול על ידי שליח (מסד ”בחירת הזמנה לטיפול”) --- */
    /// <summary>
    /// Sélectionne une commande ouverte pour un livreur en fonction :
    /// - du type de livraison
    /// - de la distance autorisée
    /// - du statut désiré
    /// </summary>
    /// <param name="courierId">ID du livreur</param>
    /// <param name="deliveryType">Type de livraison choisi (nullable)</param>
    /// <param name="status">Filtre sur le statut de commande (nullable)</param>
    /// <returns>Liste des commandes ouvertes filtrées</returns>
    IEnumerable<OpenOrderInList> GetOpenOrdersForCourier(
        int courierId,
        DeliveryTransport? deliveryType = null,
        OrderStatus? status = null);



    /* --- 7. רשימת הזמנות שטופלו על ידי שליח (מסד ”היסטוריית משלוחים של שליח”) --- */
    /// <summary>
    /// Retourne la liste des livraisons fermées effectuées par un livreur
    /// </summary>
    /// <param name="courierId">ID du livreur</param>
    /// <param name="deliveryEndType">Filtre par type de livraison fermée (nullable)</param>
    /// <param name="orderStatus">Filtre par statut de commande (nullable)</param>
    /// <returns>Liste BO.ClosedDeliveryInList</returns>
    IEnumerable<ClosedDeliveryInList> GetClosedDeliveriesForCourier(
        int courierId,
        ClosedDeliveryType? deliveryEndType = null,
        OrderStatus? orderStatus = null);



    /* --- 8. רשימת הזמנות פתוחות לבחירת שליח (מסד ”בחירת הזמנה לטיפול”) --- */
    /// <summary>
    /// Retourne la liste des commandes ouvertes triées selon :
    /// - distance par rapport au livreur
    /// - paramètres de filtrage : type de livraison, statut
    /// </summary>
    /// <param name="courierId">ID du livreur</param>
    /// <param name="deliveryType">Type de livraison (nullable)</param>
    /// <param name="status">Statut de commande ouvert (nullable)</param>
    /// <returns>Liste BO.OpenOrderInList</returns>
    IEnumerable<OpenOrderInList> GetFilteredOpenOrders(
        int courierId,
        DeliveryStatus? deliveryType = null,
        OrderStatus? status = null);
       
}
