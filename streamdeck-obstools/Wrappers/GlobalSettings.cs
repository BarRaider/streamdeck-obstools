﻿using ChatPager.Twitch;
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

        [JsonProperty(PropertyName = "delayReplaySeconds")]
        public int DelayReplaySeconds { get; set; }

        [JsonProperty(PropertyName = "sourceName")]
        public String SourceName { get; set; }

        [JsonProperty(PropertyName = "muteSound")]
        public bool MuteSound { get; set; }

        [JsonProperty(PropertyName = "serverInfo")]
        public ServerInfo ServerInfo { get; set; }

        [JsonProperty(PropertyName = "token")]
        public TwitchToken TwitchToken { get; set; }

        [JsonProperty(PropertyName = "playSpeed")]
        public int PlaySpeed { get; set; }

        [JsonProperty(PropertyName = "sceneSwitchPreviewColor")]
        public string SceneSwitchPreviewColor { get; set; }

        [JsonProperty(PropertyName = "sceneSwitchLiveColor")]
        public string SceneSwitchLiveColor { get; set; }


    }
}
