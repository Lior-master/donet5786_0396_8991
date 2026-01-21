namespace Dal;

/// <summary>
/// Represents the config component in this layer.
/// </summary>
internal static class Config
{
    internal const string s_data_config_xml = "data-config.xml";
    internal const string s_couriers_xml = "couriers.xml";
    internal const string s_deliveries_xml = "deliveries.xml";
    internal const string s_enums_xml = "enums.xml";
    internal const string s_exceptions_xml = "exceptions.xml";
    internal const string s_orders_xml = "orders.xml";
    /// <summary>
    /// Stores the next order id value.
    /// </summary>
    internal static int NextOrderId
    {
        get => XmlTools.GetAndIncreaseConfigIntVal(s_data_config_xml, "NextOrderId");
        private set => XmlTools.SetConfigIntVal(s_data_config_xml, "NextOrderId", value);
    }
    /// <summary>
    /// Stores the next delivery id value.
    /// </summary>
    internal static int NextDeliveryId
    {
        get => XmlTools.GetAndIncreaseConfigIntVal(s_data_config_xml, "NextDeliveryId");
        private set => XmlTools.SetConfigIntVal(s_data_config_xml, "NextDeliveryId", value);
    }
    /// <summary>
    /// Stores the clock value.
    /// </summary>
    internal static DateTime Clock
    {
        get => XmlTools.GetConfigDateVal(s_data_config_xml, "Clock");
        set => XmlTools.SetConfigDateVal(s_data_config_xml, "Clock", value);
    }
    /// <summary>
    /// Stores the boss id value.
    /// </summary>
    internal static int BossId
    {
        get => XmlTools.GetConfigIntVal(s_data_config_xml, "BossId");
        set => XmlTools.SetConfigIntVal(s_data_config_xml, "BossId", value);
    }
    /// <summary>
    /// Stores the boss password value.
    /// </summary>
    internal static string BossPassword
    {
        get => XmlTools.GetConfigStringVal(s_data_config_xml, "BossPassword") ?? "";
        set => XmlTools.SetConfigStringVal(s_data_config_xml, "BossPassword", value);
    }
    /// <summary>
    /// Stores the car speed value.
    /// </summary>
    internal static double CarSpeed
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "CarSpeed") ?? 40;
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "CarSpeed", value);
    }
    /// <summary>
    /// Stores the motorcycle speed value.
    /// </summary>
    internal static double MotorcycleSpeed
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "MotorcycleSpeed") ?? 50;
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "MotorcycleSpeed", value);
    }
    /// <summary>
    /// Stores the bike speed value.
    /// </summary>
    internal static double BikeSpeed
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "BikeSpeed") ?? 20;
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "BikeSpeed", value);
    }
    /// <summary>
    /// Stores the walking speed value.
    /// </summary>
    internal static double WalkingSpeed
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "WalkingSpeed") ?? 5;
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "WalkingSpeed", value);
    }
    /// <summary>
    /// Stores the max time delivery value.
    /// </summary>
    internal static TimeSpan MaxTimeDelivery
    {
        get => TimeSpan.FromHours(XmlTools.GetConfigDoubleVal(s_data_config_xml, "MaxTimeDelivery") ?? 1);
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "MaxTimeDelivery", value.TotalHours);
    }
    /// <summary>
    /// Stores the risk range value.
    /// </summary>
    internal static TimeSpan RiskRange
    {
        get => TimeSpan.FromMinutes(XmlTools.GetConfigDoubleVal(s_data_config_xml, "RiskRange") ?? 30);
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "RiskRange", value.TotalMinutes);
    }
    /// <summary>
    /// Stores the inactivity value.
    /// </summary>
    internal static TimeSpan Inactivity
    {
        get => TimeSpan.FromDays(XmlTools.GetConfigDoubleVal(s_data_config_xml, "Inactivity") ?? 30);
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "Inactivity", value.TotalDays);
    }
    /// <summary>
    /// Stores the company address value.
    /// </summary>
    internal static string? CompanyAddress
    {
        get => XmlTools.GetConfigStringVal(s_data_config_xml, "CompanyAddress");
        set
        {
            if(value != null)
                XmlTools.SetConfigStringVal(s_data_config_xml, "CompanyAddress", value); // if there is a value set it else do nothing
        }
    }
    /// <summary>
    /// Stores the latitude value.
    /// </summary>
    internal static double? Latitude
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "Latitude");
        set
        {
            if (value != null)
                XmlTools.SetConfigDoubleVal(s_data_config_xml, "Latitude", value.Value);
        }
    }
    /// <summary>
    /// Stores the longitude value.
    /// </summary>
    internal static double? Longitude
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "Longitude");
        set
        {
            if (value != null)
                XmlTools.SetConfigDoubleVal(s_data_config_xml, "Longitude", value.Value);
        }
    }
    /// <summary>
    /// Stores the max distance value.
    /// </summary>
    internal static double? MaxDistance
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "MaxDistance");
        set
        {
            if (value != null)
                XmlTools.SetConfigDoubleVal(s_data_config_xml, "MaxDistance", value.Value);
        }
    }
    internal static void Reset()
    {
        NextDeliveryId = 5000;
        NextOrderId = 1000;
        Clock = DateTime.Now;
        BossId = 347657991;
        BossPassword = "admin";
        MaxTimeDelivery = TimeSpan.FromHours(1);
        RiskRange = TimeSpan.FromMinutes(30);
        Inactivity = TimeSpan.FromDays(30);
        CompanyAddress = "22 Hameyasdim St";
        Latitude = 31.778449894212013;
        Longitude = 35.18761502733661;
        MaxDistance = 25;
        CarSpeed = 40;
        MotorcycleSpeed = 50;
        BikeSpeed = 20;
        WalkingSpeed = 5;
    }
}
