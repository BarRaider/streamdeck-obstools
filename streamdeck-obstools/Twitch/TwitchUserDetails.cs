using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChatPager.Twitch
{
    public class TwitchUserDetails
    {
        [JsonProperty(PropertyName = "user_id")]
        public string UserId { get; internal set; }

        [JsonProperty(PropertyName = "user_name")]
        public string UserName { get; internal set; }
    }
}
