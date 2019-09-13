using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChatPager.Twitch
{
        public class TwitchToken
    {
        [JsonProperty(PropertyName = "token")]
        public string Token { get; set; }

        [JsonProperty(PropertyName = "lastRefresh")]
        public DateTime TokenLastRefresh { get; set; }
    }
}
