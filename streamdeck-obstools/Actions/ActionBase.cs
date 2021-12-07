using BarRaider.ObsTools.Backend;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Actions
{
    public abstract class ActionBase : PluginBase
    {
        protected class PluginSettingsBase
        {
            [JsonProperty(PropertyName = "serverInfoExists")]
            public bool ServerInfoExists { get; set; }
        }

        #region Protected Members

        protected PluginSettingsBase settings;
        protected bool baseHandledKeypress = false;
        protected bool baseHandledOnTick = false;
        protected bool previousBaseHandledOnTick = false;
        private bool serverSettingsChanged = false;

        #endregion

        public ActionBase(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            ServerManager.Instance.TokensChanged += Instance_TokensChanged;
            OBSManager.Instance.ObsConnectionFailed += Instance_ObsConnectionFailed;
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
        }

        #region Public Methods

        public override void Dispose()
        {
            ServerManager.Instance.TokensChanged -= Instance_TokensChanged;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            OBSManager.Instance.ObsConnectionFailed -= Instance_ObsConnectionFailed;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Base Destructor called {this.GetType()}");
        }

        public virtual Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        protected void CheckServerInfoExists()
        {
            settings.ServerInfoExists = ServerManager.Instance.ServerInfoExists;
            if (!settings.ServerInfoExists)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"No server info for action {GetType()}");
            }
            SaveSettings();
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Keypress: {GetType()}");
            if (ServerManager.Instance.ServerInfoExists && !OBSManager.Instance.IsConnected)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "Keypress - OBS is not connected");
                baseHandledKeypress = true;
                await Connection.ShowAlert();
                OBSManager.Instance.Connect();
            }
        }

        public async override void OnTick()
        {
            string base64Image = GetBasicImage();
            if (!string.IsNullOrWhiteSpace(base64Image))
            {
                baseHandledOnTick = previousBaseHandledOnTick = true;
                await Connection.SetImageAsync(base64Image);
            }
            else if (serverSettingsChanged)
            {
                serverSettingsChanged = false;
                await Connection.SetImageAsync((String)null); // Reset image
            }
            else if (previousBaseHandledOnTick)
            {
                previousBaseHandledOnTick = false;
                await Connection.SetImageAsync((String)null); // Reset image
            }
        }

        #endregion

        #region Private Methods

        private string GetBasicImage()
        {
            if (!settings.ServerInfoExists)
            {
                return Properties.Settings.Default.ImgNoToken;
            }

            if (!OBSManager.Instance.IsValidVersion)
            {
                return Properties.Settings.Default.ImgUpdateWebsocket;
            }

            if (!OBSManager.Instance.IsConnected)
            {
                return Properties.Settings.Default.ImgNoConnection;
            }

            return null;
        }

        private async void Connection_OnSendToPlugin(object sender, SdTools.Wrappers.SDEventReceivedEventArgs<SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;
        
            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "setserverinfo":
                        string ip = ((string)payload["ip"]).Trim();
                        string port = ((string)payload["port"]).Trim();
                        string password = ((string)payload["password"]).Trim();
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting server info. Ip: {ip} Port: {port} Password: {String.IsNullOrEmpty(password)}");
                        ServerManager.Instance.InitTokens(ip, port, password, DateTime.Now);
                        break;
                    case "updateapproval":
                        string approvalCode = (string)payload["approvalCode"];
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Twitch Requesting approval with code: {approvalCode}");
                        ChatPager.Twitch.TwitchTokenManager.Instance.SetToken(new ChatPager.Twitch.TwitchToken() { Token = approvalCode, TokenLastRefresh = DateTime.Now });
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Twitch RefreshToken completed. Token Exists: {ChatPager.Twitch.TwitchTokenManager.Instance.TokenExists}");
                        break;
                    case "resetplugin":
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"ResetPlugin called. Tokens are cleared");
                        ChatPager.Twitch.TwitchTokenManager.Instance.RevokeToken();
                        Thread.Sleep(3000);
                        ServerManager.Instance.InitTokens(null, null, null, DateTime.Now);
                        await SaveSettings();
                        break;
                }
            }
        }

        private void Instance_TokensChanged(object sender, Wrappers.ServerInfoEventArgs e)
        {
            bool serverInfoExists = ServerManager.Instance.ServerInfoExists;
            if (settings.ServerInfoExists != serverInfoExists)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Server info is now {serverInfoExists} for action {GetType()}");
                serverSettingsChanged = true;
                settings.ServerInfoExists = serverInfoExists;
                SaveSettings();
            }
        }

        private async void Instance_ObsConnectionFailed(object sender, Exception e)
        {
            if (e is AuthFailureException)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} OBS Auth Failure");
                JObject obj = new JObject()
                {
                    new JProperty("linkStatus", new JObject() {
                                                    new JProperty("connected", 0)
                    })
                };
                await Connection.SendToPropertyInspectorAsync(obj);


            }    
        }

        #endregion
    }
}
