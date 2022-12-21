using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    public class GlobalInstantReplaySettings
    {
        [JsonProperty(PropertyName = "replayDirectory")]
        public String ReplayDirectory { get; set; }

        [JsonProperty(PropertyName = "autoReplay")]
        public bool AutoReplay { get; set; } = false;

        [JsonProperty(PropertyName = "hideReplaySeconds")]
        public int HideReplaySeconds { get; set; }

        [JsonProperty(PropertyName = "delayReplaySeconds")]
        public int DelayReplaySeconds { get; set; }

        [JsonProperty(PropertyName = "sceneName")]
        public String SceneName { get; set; }

        [JsonProperty(PropertyName = "inputName")]
        public String InputName { get; set; }

        [JsonProperty(PropertyName = "muteSound")]
        public bool MuteSound { get; set; } = true;

        [JsonProperty(PropertyName = "playSpeed")]
        public int PlaySpeed { get; set; } = 100;

        [JsonProperty(PropertyName = "autoSwitch")]
        public bool AutoSwitch { get; set; }
    }
}
