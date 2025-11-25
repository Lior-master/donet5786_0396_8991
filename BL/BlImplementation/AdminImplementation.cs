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
        // On avance l'horloge selon l'unité demandée
        DateTime newTime = unit switch
        {
            TimeUnit.Minute => AdminManager.Now.AddMinutes(1),
            TimeUnit.Hour => AdminManager.Now.AddHours(1),
            TimeUnit.Day => AdminManager.Now.AddDays(1),
            TimeUnit.Month => AdminManager.Now.AddMonths(1),
            _ => AdminManager.Now
        };

        AdminManager.UpdateClock(newTime);
    }

    public Config GetConfig()
        => AdminManager.GetConfig();

    public void SetConfig(Config configuration)
        => AdminManager.SetConfig(configuration);
}
