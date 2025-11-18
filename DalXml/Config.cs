using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dal
{
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
            get => XMLTools.GetAndIncreaseConfigIntVal(s_data_config_xml, "NextOrderId");
            private set => XMLTools.SetConfigIntVal(s_data_config_xml, "NextOrderId", value);
        }
        internal static int NextDeliveryId
        {
            get => XMLTools.GetAndIncreaseConfigIntVal(s_data_config_xml, "NextDeliveryId");
            private set => XMLTools.SetConfigIntVal(s_data_config_xml, "NextDeliveryId", value);
        }
        internal static DateTime Clock
        {
            get => XMLTools.GetConfigDateVal(s_data_config_xml, "Clock");
            set => XMLTools.SetConfigDateVal(s_data_config_xml, "Clock", value);
        }
        internal static int BossId
        {
            get => XMLTools.GetConfigIntVal(s_data_config_xml, "BossId");
            set => XMLTools.SetConfigIntVal(s_data_config_xml, "BossId", value);
        }
        internal static string BossPassword
        {
            get => XMLTools.GetConfigStringVal(s_data_config_xml, "BossPassword");
            set => XMLTools.SetConfigStringVal(s_data_config_xml, "BossPassword", value);
        }

    }
}
