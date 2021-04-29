using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatPager.Twitch
{
    public class TwitchTokenManager
    {
        #region Private Members
        private static TwitchTokenManager instance = null;
        private static readonly object objLock = new object();
        private const string OAUTH_KEY_NAME = "access_token";

        private TwitchToken token;
        private TwitchUserDetails userDetails;
        private readonly object lockObj = new object();
        private GlobalSettings global = null;

        #endregion

        #region Constructors

        public static TwitchTokenManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new TwitchTokenManager();
                    }
                    return instance;
                }
            }
        }

        private TwitchTokenManager()
        {
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += Instance_OnReceivedGlobalSettings;
            OAuthTokenListener.Instance.OnReceivedTokenData += OAuthTokenListener_OnReceivedTokenData;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
        }

        private void OAuthTokenListener_OnReceivedTokenData(object sender, System.Collections.Specialized.NameValueCollection e)
        {
            if (e == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} OAuthTokenListener_OnReceivedTokenData returned null collection");
                return;
            }

            if (String.IsNullOrEmpty(e[OAUTH_KEY_NAME]))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} OAuthTokenListener_OnReceivedTokenData did not return an access token");
                return;
            }

            SetToken(new TwitchToken() { Token = e[OAUTH_KEY_NAME], TokenLastRefresh = DateTime.Now });
        }

        #endregion

        #region Public Members

        internal event EventHandler<TwitchTokenEventArgs> TokensChanged;
        public event EventHandler TokenStatusChanged;

        public TwitchUserDetails User
        {
            get
            {
                if (userDetails == null && token != null)
                {
                    lock (lockObj)
                    {
                        if (userDetails == null && token != null)
                        {
                            LoadUserDetails();
                        }
                    }
                    
                }
                return userDetails;
            }
        }

        public bool TokenExists
        {
            get
            {
                return token != null && !(String.IsNullOrWhiteSpace(token.Token));
            }
        }


        #endregion

        #region Public Methods

        public void SetToken(TwitchToken token)
        {
            if (token != null && (this.token == null || token.TokenLastRefresh > this.token.TokenLastRefresh))
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"New token set Token Size: {token?.Token?.Length}");
                this.token = token;
                if (ValidateToken())
                {
                    SaveToken();
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "TwitchTokenManager: Could not validate token with twitch");
                    this.token = null;
                }
            }
            RaiseTokenChanged();
        }

        internal TwitchToken GetToken()
        {
            if (token != null)
            {
                return new TwitchToken() { Token = token.Token, TokenLastRefresh = token.TokenLastRefresh };
            }
            return null;
        }

        public void RevokeToken()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "TwitchTokenManager: RevokeToken Called");
            this.token = null;
            SaveToken();
            RaiseTokenChanged();
        }

        #endregion

        #region Private Methods

        private void LoadToken(TwitchToken globalToken)
        {
            try
            {
                if (globalToken == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "TwitchTokenManager: Failed to load tokens, deserialized globalToken is null");
                    OAuthTokenListener.Instance.StartListener(Constants.OAUTH_PORT, Constants.OAUTH_REDIRECT_URL);
                    return;
                }

                if (token != null && token.Token == globalToken.Token && token.TokenLastRefresh == globalToken.TokenLastRefresh)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"TwitchTokenManager: LoadToken called for EXISTING token. Token Size: {token?.Token?.Length}");
                    return;
                }

                token = new TwitchToken()
                {
                    Token = globalToken.Token,
                    TokenLastRefresh = globalToken.TokenLastRefresh
                };

                Logger.Instance.LogMessage(TracingLevel.INFO, $"TwitchTokenManager: Token initialized. Last refresh date was: {token.TokenLastRefresh} Token Size: {token?.Token?.Length}");
                if (String.IsNullOrWhiteSpace(token.Token))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "Existing token in Global Settings is empty!");
                }
                RaiseTokenChanged();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TwitchTokenManager: Exception loading tokens: {ex}");
            }
        }

        private void SaveToken()
        {
            try
            {
                if (global == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "TwitchTokenManager: Global Settings is null, creating new instance");
                    global = new GlobalSettings();
                }

                // Set token in Global Settings
                if (token == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "TwitchTokenManager: Saving null token to Global Settings");
                    global.TwitchToken = null;
                }
                else
                {
                    global.TwitchToken = new TwitchToken()
                    {
                        Token = token.Token,
                        TokenLastRefresh = token.TokenLastRefresh
                    };
                    Logger.Instance.LogMessage(TracingLevel.INFO, "TwitchTokenManager saving token to global");
                }

                GlobalSettingsManager.Instance.SetGlobalSettings(JObject.FromObject(global));
                Logger.Instance.LogMessage(TracingLevel.INFO, $"TwitchTokenManager: New token saved. Last refresh date was: {token?.TokenLastRefresh} Token Size: {token?.Token?.Length}");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TwitchTokenManager: Exception saving tokens: {ex}");
            }
        }

        private void LoadUserDetails()
        {
            TwitchComm comm = new TwitchComm();
            userDetails = Task.Run(() => comm.GetUserDetails()).GetAwaiter().GetResult();
        }

        private void RaiseTokenChanged()
        {

            TokenStatusChanged?.Invoke(this, EventArgs.Empty);
            if (token != null)
            {
                TokensChanged?.Invoke(this, new TwitchTokenEventArgs(new TwitchToken() { Token = token.Token, TokenLastRefresh = token.TokenLastRefresh }));
            }
            else
            {
                TokensChanged?.Invoke(this, new TwitchTokenEventArgs(null));
                OAuthTokenListener.Instance.StartListener(Constants.OAUTH_PORT, Constants.OAUTH_REDIRECT_URL);
            }
        }

        private bool ValidateToken()
        {
            LoadUserDetails();
            return userDetails != null && !String.IsNullOrWhiteSpace(userDetails.UserName);
        }

        private void Instance_OnReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload payload)
        {
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                global = payload.Settings.ToObject<GlobalSettings>();
                LoadToken(global.TwitchToken);
            }
            else
            {
                OAuthTokenListener.Instance.StartListener(Constants.OAUTH_PORT, Constants.OAUTH_REDIRECT_URL);
            }
        }

        #endregion
    }
}
