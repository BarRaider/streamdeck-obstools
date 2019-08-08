using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    public class GlobalSettings
    {
        [JsonProperty(PropertyName = "replayDirectory")]
        public String ReplayDirectory { get; set; }

        [JsonProperty(PropertyName = "autoReplay")]
        public bool AutoReplay { get; set; }

        [JsonProperty(PropertyName = "hideReplaySeconds")]
        public int HideReplaySeconds { get; set; }

        [JsonProperty(PropertyName = "sourceName")]
        public String SourceName { get; set; }

        [JsonProperty(PropertyName = "muteSound")]
        public bool MuteSound { get; set; }

        [JsonProperty(PropertyName = "serverInfo")]
        public ServerInfo Server { get; set; }
    }
}
