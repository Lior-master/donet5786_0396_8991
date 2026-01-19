namespace BLApi;

public interface IAdmin
{
    void ResetDB();                     // Reset all DB
    void InitializeDB();                // Reset with initialize DB
    DateTime GetClock();                // Read the clock
    void ForwardClock(BO.TimeUnit unit); // to forward the clock
    BO.Config GetConfig();              // Read the config
    void SetConfig(BO.Config config);   // to update the config
    void AddConfigObserver(Action configObserver);
    void RemoveConfigObserver(Action configObserver);
    void AddClockObserver(Action clockObserver);
    void RemoveClockObserver(Action clockObserver);
}
