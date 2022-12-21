using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    public enum MonitorTypes
    {
        None = 0,
        MonitorOnly = 1,
        MonitorAndOutput = 2
    }

    public static class MonitorTypesExtensionMethods
    {
        public static string ToStringEx(this MonitorTypes monitorType)
        {
            switch (monitorType)
            {
                case MonitorTypes.None:
                    return "OBS_MONITORING_TYPE_NONE";
                case MonitorTypes.MonitorOnly:
                    return "OBS_MONITORING_TYPE_MONITOR_ONLY";
                case MonitorTypes.MonitorAndOutput:
                    return "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT";
                default:
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"MonitorTypes.ToString - Invalid Type: {monitorType}");
                    break;

            }
            return null;
        }

        public static MonitorTypes ToMonitorType(this string strType)
        {
            switch (strType)
            {
                case "OBS_MONITORING_TYPE_NONE":
                    return MonitorTypes.None;
                case "OBS_MONITORING_TYPE_MONITOR_ONLY":
                    return MonitorTypes.MonitorOnly;
                case "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT":
                    return MonitorTypes.MonitorAndOutput;
                default:
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"MonitorTypes.ToMonitorType - Invalid Type: {strType}");
                    break;

            }
            return MonitorTypes.None;
        }
    }
}
