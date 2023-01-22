using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    public class StreamToggleAction : KeypadActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    StreamingIcon = DEFAULT_STREAMING_ICON,
                    StoppedIcon = DEFAULT_STOPPED_ICON
                };
                return instance;
            }

            [JsonProperty(PropertyName = "streamingIcon")]
            public string StreamingIcon { get; set; }

            [JsonProperty(PropertyName = "stoppedIcon")]
            public string StoppedIcon { get; set; }
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

        #region Private Members

        private const string DEFAULT_STREAMING_ICON = "📡";
        private const string DEFAULT_STOPPED_ICON = "🔲";

        #endregion

        public StreamToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
            InitializeSettings();
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
                if (payload.IsInMultiAction)
                {
                    HandleMultiActionKeyPress(payload.UserDesiredState);
                }
                else
                {
                    HandleKeyPress();
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
                await Connection.SetTitleAsync($"{(OBSManager.Instance.IsStreaming ? Settings.StreamingIcon : Settings.StoppedIcon)}");
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            InitializeSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        private void InitializeSettings()
        {
            bool saveSettings = false;

            if (String.IsNullOrEmpty(Settings.StreamingIcon))
            {
                Settings.StreamingIcon = DEFAULT_STREAMING_ICON;
                saveSettings = true;
            }

            if (String.IsNullOrEmpty(Settings.StoppedIcon))
            {
                Settings.StoppedIcon = DEFAULT_STOPPED_ICON;
                saveSettings = true;
            }

            if (saveSettings)
            {
                SaveSettings();
            }
        }

        private void HandleMultiActionKeyPress(uint state)
        {
            switch (state) // 0 = Start, 1 = Stop
            {
                case 0:
                    if (!OBSManager.Instance.IsStreaming)
                    {
                        OBSManager.Instance.StartStreaming();
                    }
                    break;
                case 1:
                    if (OBSManager.Instance.IsStreaming)
                    {
                        OBSManager.Instance.StopStreaming();
                    }
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} HandleMultiActionKeyPress: Invalid state {state}");
                    break;
            }
        }

        private void HandleKeyPress()
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
        #endregion
    }
}