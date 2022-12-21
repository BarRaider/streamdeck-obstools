using ChatPager.Twitch;
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

        [JsonProperty(PropertyName = "instantReplaySettings")]
        public GlobalInstantReplaySettings InstantReplaySettings { get; set; }

        [JsonProperty(PropertyName = "serverInfo")]
        public ServerInfo ServerInfo { get; set; }

        [JsonProperty(PropertyName = "token")]
        public TwitchToken TwitchToken { get; set; }

        [JsonProperty(PropertyName = "sceneSwitchPreviewColor")]
        public string SceneSwitchPreviewColor { get; set; }

        [JsonProperty(PropertyName = "sceneSwitchLiveColor")]
        public string SceneSwitchLiveColor { get; set; }

        public GlobalSettings()
        {
            InstantReplaySettings = new GlobalInstantReplaySettings();
        }

    }
}
