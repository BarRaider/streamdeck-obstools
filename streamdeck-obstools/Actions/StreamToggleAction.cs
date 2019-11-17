using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace BarRaider.ObsTools.Actions
{
    [PluginActionId("com.barraider.obstools.streamtoggle")]
    public class StreamToggleAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false
                };
                return instance;
            }
        }

        protected PluginSettings Settings
        {
            get
            {
                var result = settings as PluginSettings;
                if (result == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "Cannot convert PluginSettingsBase to PluginSettings");
                }
                return result;
            }
            set
            {
                settings = value;
            }
        }

        public StreamToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public override void KeyPressed(KeyPayload payload)
        {
            baseHandledKeypress = false;
            base.KeyPressed(payload);

            if (!baseHandledKeypress)
            {
                if (payload.IsInMultiAction && payload.UserDesiredState == 0 && !OBSManager.Instance.IsStreaming) // Multiaction mode, check if desired state is 0 (0==Start, 1==Stop) 
                {
                    OBSManager.Instance.StartStreaming();
                }
                else if (payload.IsInMultiAction && payload.UserDesiredState == 1 && OBSManager.Instance.IsStreaming) // Multiaction mode, check if desired state is 1 (0==Start, 1==Stop) 
                {
                    OBSManager.Instance.StopStreaming();
                }
                else if (!payload.IsInMultiAction) // Not in a multi action
                {
                    if (OBSManager.Instance.IsStreaming)
                    {
                        OBSManager.Instance.StopStreaming();
                    }
                    else
                    {
                        OBSManager.Instance.StartStreaming();
                    }
                }
            }
        }


        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (!baseHandledOnTick)
            {
                await Connection.SetTitleAsync($"{(OBSManager.Instance.IsStreaming ? "📡" : "🔲")}");
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }
        #endregion
    }
}