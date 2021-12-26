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
                    return "none";
                case MonitorTypes.MonitorOnly:
                    return "monitorOnly";
                case MonitorTypes.MonitorAndOutput:
                    return "monitorAndOutput";
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
                case "none":
                    return MonitorTypes.None;
                case "monitorOnly":
                    return MonitorTypes.MonitorOnly;
                case "monitorAndOutput":
                    return MonitorTypes.MonitorAndOutput;
                default:
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"MonitorTypes.ToMonitorType - Invalid Type: {strType}");
                    break;

            }
            return MonitorTypes.None;
        }
    }
}
