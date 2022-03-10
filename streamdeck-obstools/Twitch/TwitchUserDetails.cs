using Newtonsoft.Json;

namespace ChatPager.Twitch
{
    public class TwitchUserDetails
    {
        [JsonProperty(PropertyName = "id")]
        public string UserId { get; internal set; }

        [JsonProperty(PropertyName = "login")]
        public string UserName { get; internal set; }
    }
}