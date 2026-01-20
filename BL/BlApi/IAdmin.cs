namespace BLApi;

using System.Threading.Tasks;

public interface IAdmin
{
    Task ResetDBAsync();                          // Reset all DB
    Task InitializeDBAsync();                     // Reset with initialize DB
    DateTime GetClock();                          // Read the clock
    void ForwardClock(BO.TimeUnit unit);          // to forward the clock
    BO.Config GetConfig();                        // Read the config
    Task SetConfigAsync(BO.Config config);        // to update the config
    void AddConfigObserver(Action configObserver);
    void RemoveConfigObserver(Action configObserver);
    void AddClockObserver(Action clockObserver);
    void RemoveClockObserver(Action clockObserver);
    void StartSimulator(int interval);             // stage 7
    void StopSimulator();                          // stage 7
}
