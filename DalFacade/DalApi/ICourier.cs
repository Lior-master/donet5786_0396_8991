using DO;
namespace BlApi;

public interface ICourier
{
    // --- בקשת כניסה לשליח (login) ---
    /// <summary>
    /// Checks login of a courier using his ID and password.
    /// </summary>
    /// <param name="id">Courier ID</param>
    /// <param name="password">Password</param>
    /// <returns>BO.Courier</returns>
    Courier Login(int id, string password);


    // --- בקשת רשימת שליחים ---
    /// <summary>
    /// Returns a list of couriers filtered by activity or type.
    /// </summary>
    /// <param name="onlyActive">Return only active couriers (nullable)</param>
    /// <param name="deliveryType">Filter by delivery type (nullable)</param>
    /// <returns>List of BO.CourierInList</returns>
    IEnumerable<CourierInList> GetCourierList(bool? onlyActive = null, DO.DeliveryType? deliveryType = null);


    // --- בקשת פרטי שליח ---
    /// <summary>
    /// Returns full business object of a courier by ID.
    /// </summary>
    /// <param name="id">Courier ID</param>
    /// <returns>BO.Courier</returns>
    Courier Read(int id);


    // --- בקשת עדכון שליח ---
    /// <summary>
    /// Updates an existing courier.
    /// </summary>
    /// <param name="courier">Courier object containing updated data</param>
    void Update(Courier courier);


    // --- בקשת מחיקת שליח ---
    /// <summary>
    /// Deletes a courier by ID if possible.
    /// </summary>
    /// <param name="id">Courier ID</param>
    void Delete(int id);


    // --- בקשת הוספת שליח ---
    /// <summary>
    /// Creates a new courier.
    /// </summary>
    /// <param name="courier">New courier business object</param>
    /// <returns>ID of the created courier</returns>
    int Create(Courier courier);
}
