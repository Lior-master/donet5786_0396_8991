namespace BLImplementation;

using BLApi;
using BO;
using Helpers;

internal class AdminImplementation : IAdmin
{
    public void ResetDB()
        => AdminManager.ResetDB();

    public void InitializeDB()
        => AdminManager.InitializeDB();

    public DateTime GetClock()
        => AdminManager.Now;

    public void ForwardClock(TimeUnit unit)
    {
        // we forward the clock
        DateTime newTime = unit switch
        {
            TimeUnit.Second => AdminManager.Now.AddSeconds(1),
            TimeUnit.Minute => AdminManager.Now.AddMinutes(1),
            TimeUnit.Hour => AdminManager.Now.AddHours(1),
            TimeUnit.Day => AdminManager.Now.AddDays(1),
            TimeUnit.Month => AdminManager.Now.AddMonths(1),
            TimeUnit.Year => AdminManager.Now.AddYears(1),
            _ => AdminManager.Now
        };

        AdminManager.UpdateClock(newTime);
    }

    public Config GetConfig()
        => AdminManager.GetConfig();

    public void SetConfig(Config configuration)
        => AdminManager.SetConfig(configuration);
    public void AddClockObserver(Action clockObserver)
        => AdminManager.ClockUpdatedObservers += clockObserver;
    public void RemoveClockObserver(Action clockObserver)
        => AdminManager.ClockUpdatedObservers -= clockObserver;
    public void AddConfigObserver(Action configObserver)
        => AdminManager.ConfigUpdatedObservers += configObserver;
    public void RemoveConfigObserver(Action configObserver)
        => AdminManager.ConfigUpdatedObservers -= configObserver;
}
