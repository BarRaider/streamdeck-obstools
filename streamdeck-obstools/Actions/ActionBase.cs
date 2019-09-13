using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private bool serverSettingsChanged = false;

        #endregion

        public ActionBase(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            ServerManager.Instance.TokensChanged += Instance_TokensChanged;
            Connection.StreamDeckConnection.OnSendToPlugin += StreamDeckConnection_OnSendToPlugin;
        }

        #region Public Methods

        public override void Dispose()
        {
            ServerManager.Instance.TokensChanged -= Instance_TokensChanged;
            Connection.StreamDeckConnection.OnSendToPlugin -= StreamDeckConnection_OnSendToPlugin;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Base Destructor called");
        }

        public virtual Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        protected void CheckServerInfoExists()
        {
            settings.ServerInfoExists = ServerManager.Instance.ServerInfoExists;
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
                baseHandledOnTick = true;
                await Connection.SetImageAsync(base64Image);
            }
            else if (serverSettingsChanged)
            {
                serverSettingsChanged = false;
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
            return null;
        }

        private async void StreamDeckConnection_OnSendToPlugin(object sender, streamdeck_client_csharp.StreamDeckEventReceivedEventArgs<streamdeck_client_csharp.Events.SendToPluginEvent> e)
        {
            var payload = e.Event.Payload;
            if (Connection.ContextId != e.Event.Context)
            {
                return;
            }

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLower())
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
                serverSettingsChanged = true;
                settings.ServerInfoExists = serverInfoExists;
                SaveSettings();
            }
        }
        #endregion
    }
}
