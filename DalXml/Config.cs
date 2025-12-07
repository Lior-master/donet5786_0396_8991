namespace Dal;

internal static class Config
{
    internal const string s_data_config_xml = "data-config.xml";
    internal const string s_couriers_xml = "couriers.xml";
    internal const string s_deliveries_xml = "deliveries.xml";
    internal const string s_enums_xml = "enums.xml";
    internal const string s_exceptions_xml = "exceptions.xml";
    internal const string s_orders_xml = "orders.xml";
    internal static int NextOrderId
    {
        get => XmlTools.GetAndIncreaseConfigIntVal(s_data_config_xml, "NextOrderId");
        private set => XmlTools.SetConfigIntVal(s_data_config_xml, "NextOrderId", value);
    }
    internal static int NextDeliveryId
    {
        get => XmlTools.GetAndIncreaseConfigIntVal(s_data_config_xml, "NextDeliveryId");
        private set => XmlTools.SetConfigIntVal(s_data_config_xml, "NextDeliveryId", value);
    }
    internal static DateTime Clock
    {
        get => XmlTools.GetConfigDateVal(s_data_config_xml, "Clock");
        set => XmlTools.SetConfigDateVal(s_data_config_xml, "Clock", value);
    }
    internal static int BossId
    {
        get => XmlTools.GetConfigIntVal(s_data_config_xml, "BossId");
        set => XmlTools.SetConfigIntVal(s_data_config_xml, "BossId", value);
    }
    internal static string BossPassword
    {
        get => XmlTools.GetConfigStringVal(s_data_config_xml, "BossPassword") ?? "";
        set => XmlTools.SetConfigStringVal(s_data_config_xml, "BossPassword", value);
    }
    internal static double CarSpeed
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "CarSpeed") ?? 40;
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "CarSpeed", value);
    }
    internal static double MotorcycleSpeed
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "MotorcycleSpeed") ?? 50;
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "MotorcycleSpeed", value);
    }
    internal static double BikeSpeed
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "BikeSpeed") ?? 20;
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "BikeSpeed", value);
    }
    internal static double WalkingSpeed
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "WalkingSpeed") ?? 5;
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "WalkingSpeed", value);
    }
    internal static TimeSpan MaxTimeDelivery
    {
        get => TimeSpan.FromHours(XmlTools.GetConfigDoubleVal(s_data_config_xml, "MaxTimeDelivery") ?? 1);
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "MaxTimeDelivery", value.TotalHours);
    }
    internal static TimeSpan RiskRange
    {
        get => TimeSpan.FromMinutes(XmlTools.GetConfigDoubleVal(s_data_config_xml, "RiskRange") ?? 30);
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "RiskRange", value.TotalMinutes);
    }
    internal static TimeSpan Inactivity
    {
        get => TimeSpan.FromDays(XmlTools.GetConfigDoubleVal(s_data_config_xml, "Inactivity") ?? 30);
        set => XmlTools.SetConfigDoubleVal(s_data_config_xml, "Inactivity", value.TotalDays);
    }
    internal static string? CompanyAddress
    {
        get => XmlTools.GetConfigStringVal(s_data_config_xml, "CompanyAddress");
        set
        {
            if(value != null)
                XmlTools.SetConfigStringVal(s_data_config_xml, "CompanyAddress", value); // if there is a value set it else do nothing
        }
    }
    internal static double? Latitude
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "Latitude");
        set
        {
            if (value != null)
                XmlTools.SetConfigDoubleVal(s_data_config_xml, "Latitude", value.Value);
        }
    }
    internal static double? Longitude
    {
        get => XmlTools.GetConfigDoubleVal(s_data_config_xml, "Longitude");
        set
        {
            if (value != null)
                XmlTools.SetConfigDoubleVal(s_data_config_xml, "Longitude", value.Value);
        }
    }
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
    }
}
