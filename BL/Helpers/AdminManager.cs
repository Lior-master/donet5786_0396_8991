//using BO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Helpers;

/// <summary>
/// Internal BL manager for all Application's Configuration Variables and Clock logic policies
/// </summary>
internal static class AdminManager //stage 4
{
    #region Stage 4-7
    private static readonly DalApi.IDal s_dal = DalApi.Factory.Get; //stage 4

    /// <summary>
    /// Property for providing current application's clock value for any BL class that may need it
    /// </summary>
    internal static DateTime Now { get => s_dal.Config.Clock; } //stage 4

    internal static event Action? ConfigUpdatedObservers; //stage 5 - for config update observers
    internal static event Action? ClockUpdatedObservers; //stage 5 - for clock update observers

    private static Task? _periodicTask = null; //stage 7

    /// <summary>
    /// Method to update application's clock from any BL class as may be required
    /// </summary>
    /// <param name="newClock">updated clock value</param>
    internal static void UpdateClock(DateTime newClock) //stage 4-7
    {
        var oldClock = s_dal.Config.Clock; //stage 4
        s_dal.Config.Clock = newClock; //stage 4

        _ = Task.Run(()=> CourierManager.PeriodicCouriersUpdates(oldClock, newClock)); //stage 7
        _ = Task.Run(()=> OrderManager.PeriodicOrdersUpdates(oldClock, newClock)); //stage 7

        //Calling all the observers of clock update
        ClockUpdatedObservers?.Invoke(); //prepared for stage 5
    }

    /// <summary>
    /// Method for providing current configuration variables values for any BL class that may need it
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7
    internal static BO.Config GetConfig() //stage 4
    => new BO.Config()
    {
        Clock = s_dal.Config.Clock,
        BossId = s_dal.Config.BossId,
        BossPassword = s_dal.Config.BossPassword,
        CarSpeed = s_dal.Config.CarSpeed,
        MotorcycleSpeed = s_dal.Config.MotorcycleSpeed,
        BikeSpeed = s_dal.Config.BikeSpeed,
        WalkingSpeed = s_dal.Config.WalkingSpeed,
        MaxDeliveryTime = s_dal.Config.MaxTimeDelivery,
        RiskRange = s_dal.Config.RiskRange,
        InactivityThreshold = s_dal.Config.Inactivity,
        CompanyAddress = s_dal.Config.CompanyAdress,
        CompanyLatitude = s_dal.Config.Latitude,
        CompanyLongitude = s_dal.Config.Longitude,
        MaxDistance = s_dal.Config.MaxDistance,



    };

    /// <summary>
    /// Method for setting current configuration variables values for any BL class that may need it
    /// </summary>
    internal static async Task SetConfigAsync(BO.Config configuration) //stage 4
    {
        bool configChanged = false; // stage 5
        bool badAddress = false;
        bool shouldGeocode = false;
        string companyAddress = configuration.CompanyAddress;

        lock (BlMutex) //stage 7
        {
            if (s_dal.Config.BossId != configuration.BossId)
            {
                s_dal.Config.BossId = configuration.BossId;
                configChanged = true;
            }

            if (s_dal.Config.BossPassword != configuration.BossPassword)
            {
                s_dal.Config.BossPassword = configuration.BossPassword;
                configChanged = true;
            }

            if (s_dal.Config.CarSpeed != configuration.CarSpeed)
            {
                s_dal.Config.CarSpeed = configuration.CarSpeed;
                configChanged = true;
            }

            if (s_dal.Config.MotorcycleSpeed != configuration.MotorcycleSpeed)
            {
                s_dal.Config.MotorcycleSpeed = configuration.MotorcycleSpeed;
                configChanged = true;
            }

            if (s_dal.Config.BikeSpeed != configuration.BikeSpeed)
            {
                s_dal.Config.BikeSpeed = configuration.BikeSpeed;
                configChanged = true;
            }

            if (s_dal.Config.WalkingSpeed != configuration.WalkingSpeed)
            {
                s_dal.Config.WalkingSpeed = configuration.WalkingSpeed;
                configChanged = true;
            }

            if (s_dal.Config.MaxTimeDelivery != configuration.MaxDeliveryTime)
            {
                s_dal.Config.MaxTimeDelivery = configuration.MaxDeliveryTime;
                configChanged = true;
            }

            if (s_dal.Config.RiskRange != configuration.RiskRange)
            {
                s_dal.Config.RiskRange = configuration.RiskRange;
                configChanged = true;
            }

            if (s_dal.Config.Inactivity != configuration.InactivityThreshold)
            {
                s_dal.Config.Inactivity = configuration.InactivityThreshold;
                configChanged = true;
            }

            if (s_dal.Config.CompanyAdress != configuration.CompanyAddress)
            {
                s_dal.Config.CompanyAdress = configuration.CompanyAddress;
                s_dal.Config.Latitude = 0;
                s_dal.Config.Longitude = 0;
                shouldGeocode = !string.IsNullOrWhiteSpace(companyAddress) &&
                    !string.Equals(companyAddress.Trim(), Tools.InvalidAddressMarker, StringComparison.OrdinalIgnoreCase);
                if (string.Equals(companyAddress.Trim(), Tools.InvalidAddressMarker, StringComparison.OrdinalIgnoreCase))
                    badAddress = true;
                configChanged = true;
            }

            if (s_dal.Config.MaxDistance != configuration.MaxDistance)
            {
                s_dal.Config.MaxDistance = configuration.MaxDistance;
                configChanged = true;
            }
        }

        if (shouldGeocode)
        {
            var coords = await Tools.TryGetCoordinatesFromAddressAsync(companyAddress).ConfigureAwait(false);
            lock (BlMutex)
            {
                if (coords.HasValue)
                {
                    s_dal.Config.Latitude = coords.Value.Latitude;
                    s_dal.Config.Longitude = coords.Value.Longitude;
                }
                else
                {
                    s_dal.Config.CompanyAdress = Tools.InvalidAddressMarker;
                    badAddress = true;
                }
            }
        }

        if (configChanged) // stage 5
            ConfigUpdatedObservers?.Invoke(); // stage 5

        if (badAddress)
            throw new BO.BLBadAddressException("Company address is invalid. Configuration saved with INVALID_ADDRESS.");
    }

    internal static async Task ResetDBAsync() //stage 4-7
    {
        lock (BlMutex) //stage 7
        {
            s_dal.ResetDB(); //stage 4
            AdminManager.UpdateClock(AdminManager.Now); //stage 5 - needed since we want the label on Pl to be updated
        }
        await AdminManager.SetConfigAsync(AdminManager.GetConfig()).ConfigureAwait(false); //stage 5 - needed to update PL 
    }

    internal static async Task InitializeDBAsync() //stage 4-7
    {
        lock (BlMutex) //stage 7
        {
            DalTest.Initialization.Do(); //stage 4
            AdminManager.UpdateClock(AdminManager.Now);  //stage 5 - needed since we want the label on Pl to be updated           
        }
        await AdminManager.SetConfigAsync(AdminManager.GetConfig()).ConfigureAwait(false); //stage 5 - needed for update the PL
    }

    #endregion Stage 4-7

    #region Stage 7 base

    /// <summary>    
    /// Mutex to use from BL methods to get mutual exclusion while the simulator is running
    /// </summary>
    internal static readonly object BlMutex = new(); // BlMutex = s_dal; // This field is actually the same as s_dal - it is defined for readability of locks
    /// <summary>
    /// The thread of the simulator
    /// </summary>
    private static volatile Thread? s_thread;
    /// <summary>
    /// The Interval for clock updating
    /// in minutes by second (default value is 1, will be set on Start())    
    /// </summary>
    private static double s_interval = 1;
    /// <summary>
    /// The flag that signs whether simulator is running
    /// 
    private static volatile bool s_stop = false;

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7                                                 
    public static void ThrowOnSimulatorIsRunning()
    {
        if (s_thread is not null)
            throw new BO.BLTemporaryNotAvailableException("Cannot perform the operation since Simulator is running");
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7                                                 
    internal static void Start(double interval)
    {
        if (s_thread is null)
        {
            s_interval = interval;
            s_stop = false;
            s_thread = new(clockRunner) { Name = "ClockRunner" };
            s_thread.Start();
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //stage 7                                                 
    internal static void Stop()
    {
        if (s_thread is not null)
        {
            s_stop = true;
            s_thread.Interrupt(); //awake a sleeping thread
            s_thread.Name = "ClockRunner stopped";
            s_thread = null;
        }
    }

    private static Task? _simulateTask = null;

    private static void clockRunner()
    {
        while (!s_stop)
        {
            UpdateClock(Now.AddMinutes(s_interval));

            //TO_DO: //stage 7
            //Add calls here to any logic simulation that was required in stage 7
            //for example: course registration simulation
            if (_simulateTask is null || _simulateTask.IsCompleted)//stage 7
                _simulateTask = Task.Run(async () => await CourierManager.SimulateCourierActivityAsync().ConfigureAwait(false));

            //etc...

            try
            {
                Thread.Sleep(1000); // 1 second
            }
            catch (ThreadInterruptedException) { }
        }
    }

    #endregion Stage 7 base
}
