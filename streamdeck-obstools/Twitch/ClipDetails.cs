using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChatPager.Twitch
{
    public class ClipDetails
    {
        [JsonProperty(PropertyName = "id")]
        public string ClipId { get; internal set; }

        [JsonProperty(PropertyName = "edit_url")]
        public string EditURL { get; internal set; }
    }
}
