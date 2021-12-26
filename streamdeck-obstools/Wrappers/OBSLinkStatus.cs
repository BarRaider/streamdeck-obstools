using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    public class OBSLinkStatus
    {
        [JsonProperty(PropertyName = "connected")]
        public bool Connected { get; set; }

        [JsonProperty(PropertyName = "failCode")]
        public int FailCode { get; set; }

        public OBSLinkStatus(bool isConnected, int failCode)
        {
            Connected = isConnected;
            FailCode = failCode;
        }
    }
}
