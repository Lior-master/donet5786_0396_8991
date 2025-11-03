using DO;

namespace DalApi;

public interface IConfig
{
    DateTime Clock { get; set; }
    internal static int NextOrderId => nextOrderId++;
    internal const int startOrderId = 1;
    internal static int nextOrderId = startOrderId;


    void Create(Delivery item); //Creates new entity object in DAL
    Config? Read(int id); //Reads entity object by its ID 
    List<T> ReadAll(); //stage 1 only, Reads all entity objects
    void Update(T item); //Updates entity object
    void Delete(int id); //Deletes an object by its Id
    void DeleteAll(); //Delete all entity objects

}
