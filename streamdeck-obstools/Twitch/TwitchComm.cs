using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ChatPager.Twitch
{
    internal enum SendMethod
    {
        GET,
        POST,
        PUT,
        POST_QUERY_PARAMS
    }

    public class TwitchComm : IDisposable
    {
        #region Private Members

        private const string TWITCH_URI_PREFIX = "https://api.twitch.tv/kraken";
        private const string TWITCH_HELIX_URI_PREFIX = "https://api.twitch.tv/helix";
        private const string TWITCH_ACCEPT_HEADER = "application/vnd.twitchtv.v5+json";
        private const string TWITCH_CREATE_CLIP_URI = "/clips?broadcaster_id=";

        private TwitchToken token;

        #endregion

        #region Public Methods

        public TwitchComm()
        {
            TwitchTokenManager.Instance.TokensChanged += Instance_TokensChanged;
            token = TwitchTokenManager.Instance.GetToken();
        }

        public void Dispose()
        {
            TwitchTokenManager.Instance.TokensChanged -= Instance_TokensChanged;
        }
       
        #endregion

        #region Private Methods



        internal async Task<TwitchUserDetails> GetUserDetails()
        {
            HttpResponseMessage response = await TwitchQuery(String.Empty, SendMethod.GET, null, null);
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    string body = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(body);
                    TwitchUserDetails userDetails = json["token"].ToObject<TwitchUserDetails>();
                    //userDetails = new TwitchUserDetails() { UserName = "KayRaid", UserId = "86502273" };
                    return userDetails;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetUserDetails Exception: {ex}");
                }
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "GetUserDetails Fetch Failed");
            }
            return null;
        }

        internal async Task<ClipDetails> CreateClip()
        {
            if (TwitchTokenManager.Instance.User == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "Cannot create Twitch clip, User object is null");
                return null;
            }

            string uri = TWITCH_CREATE_CLIP_URI + TwitchTokenManager.Instance.User?.UserId;
            HttpResponseMessage response = await TwitchHelixQuery(uri, SendMethod.POST, null, null);
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    string body = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(body);
                    return json["data"][0].ToObject<ClipDetails>();
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"CreateClip Exception: {ex}");
                }
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"CreateClip Fetch Failed. StatusCode: {response.StatusCode}");
            }
            return null;
        }

        internal async Task<HttpResponseMessage> TwitchQuery(string uriPath, SendMethod sendMethod, List<KeyValuePair<string, string>> optionalContent, JObject body)
        {
            try
            {
                if (token == null || String.IsNullOrEmpty(token.Token))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "TwitchQuery called without a valid token");
                    return new HttpResponseMessage() { StatusCode = HttpStatusCode.Conflict };
                }

                HttpResponseMessage response = await TwitchQueryInternal(uriPath, sendMethod, optionalContent, body);
                if (response == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"TwitchQueryInternal returned null");
                    return response;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"TwitchQueryInternal  returned with StatusCode: {response.StatusCode}");
                }

                return response;

            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TwitchQuery Exception: {ex}");
            }
            return new HttpResponseMessage() { StatusCode = HttpStatusCode.InternalServerError, ReasonPhrase = "TwitchQuery Exception" };
        }

        private async Task<HttpResponseMessage> TwitchQueryInternal(string uriPath, SendMethod sendMethod, List<KeyValuePair<string, string>> optionalContent, JObject body)
        {
            if (token == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "TwitchQueryInternal called with null token object");
            }

            HttpContent content = null;
            string queryParams = string.Empty;
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Client-ID", TwitchSecret.CLIENT_ID);
            client.DefaultRequestHeaders.Add("Authorization", $"OAuth {token.Token}");
            client.DefaultRequestHeaders.Add("Accept", TWITCH_ACCEPT_HEADER); 
            client.Timeout = new TimeSpan(0, 0, 10);

            switch (sendMethod)
            {
                case SendMethod.POST:
                case SendMethod.POST_QUERY_PARAMS:

                    if (optionalContent != null && sendMethod == SendMethod.POST)
                    {
                        content = new FormUrlEncodedContent(optionalContent);
                    }
                    else if (optionalContent != null && sendMethod == SendMethod.POST_QUERY_PARAMS)
                    {
                        queryParams = "?" + CreateQueryString(optionalContent);
                    }
                    return await client.PostAsync($"{TWITCH_URI_PREFIX}{uriPath}{queryParams}", content);
                case SendMethod.PUT:
                case SendMethod.GET:
                    if (optionalContent != null)
                    {
                        queryParams = "?" + CreateQueryString(optionalContent);
                    }

                    if (sendMethod == SendMethod.GET)
                    {
                        return await client.GetAsync($"{TWITCH_URI_PREFIX}{uriPath}{queryParams}");
                    }

                    if (body != null)
                    {
                        content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
                    }

                    return await client.PutAsync($"{TWITCH_URI_PREFIX}{uriPath}{queryParams}", content);
            }
            return null;
        }

        #region Helix

        internal async Task<HttpResponseMessage> TwitchHelixQuery(string uriPath, SendMethod sendMethod, List<KeyValuePair<string, string>> optionalContent, JObject body)
        {
            try
            {
                if (token == null || String.IsNullOrEmpty(token.Token))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "TwitchHelixQuery called without a valid token");
                    return new HttpResponseMessage() { StatusCode = HttpStatusCode.Conflict };
                }

                HttpResponseMessage response = await TwitchHelixQueryInternal(uriPath, sendMethod, optionalContent, body);
                if (response == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"TwitchHelixQueryInternal returned null");
                    return response;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"TwitchHelixQueryInternal  returned with StatusCode: {response.StatusCode}");
                }

                return response;

            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TwitchHelixQuery Exception: {ex}");
            }
            return new HttpResponseMessage() { StatusCode = HttpStatusCode.InternalServerError, ReasonPhrase = "TwitchHelixQuery Exception" };
        }

        private async Task<HttpResponseMessage> TwitchHelixQueryInternal(string uriPath, SendMethod sendMethod, List<KeyValuePair<string, string>> optionalContent, JObject body)
        {
            if (token == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "TwitchHelixQueryInternal called with null token object");
            }

            HttpContent content = null;
            string queryParams = string.Empty;
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Client-ID", TwitchSecret.CLIENT_ID);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.Token}");
            client.DefaultRequestHeaders.Add("Accept", TWITCH_ACCEPT_HEADER);
            client.Timeout = new TimeSpan(0, 0, 10);

            switch (sendMethod)
            {
                case SendMethod.POST:
                case SendMethod.POST_QUERY_PARAMS:

                    if (optionalContent != null && sendMethod == SendMethod.POST)
                    {
                        content = new FormUrlEncodedContent(optionalContent);
                    }
                    else if (optionalContent != null && sendMethod == SendMethod.POST_QUERY_PARAMS)
                    {
                        queryParams = "?" + CreateQueryString(optionalContent);
                    }
                    return await client.PostAsync($"{TWITCH_HELIX_URI_PREFIX}{uriPath}{queryParams}", content);
                case SendMethod.PUT:
                case SendMethod.GET:
                    if (optionalContent != null)
                    {
                        queryParams = "?" + CreateQueryString(optionalContent);
                    }

                    if (sendMethod == SendMethod.GET)
                    {
                        return await client.GetAsync($"{TWITCH_HELIX_URI_PREFIX}{uriPath}{queryParams}");
                    }

                    if (body != null)
                    {
                        content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
                    }

                    return await client.PutAsync($"{TWITCH_HELIX_URI_PREFIX}{uriPath}{queryParams}", content);
            }
            return null;
        }

        #endregion

        private string CreateQueryString(List<KeyValuePair<string, string>> parameters)
        {
            List<string> paramList = new List<string>();
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    paramList.Add($"{kvp.Key}={kvp.Value}");
                }
            }
            return string.Join("&", paramList);
        }

        private void Instance_TokensChanged(object sender, TwitchTokenEventArgs e)
        {
            token = e.Token;
        }

        #endregion
    }
}
