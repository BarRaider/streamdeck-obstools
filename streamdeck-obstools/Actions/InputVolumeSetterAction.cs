using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet.Types;
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

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: nubby_ninja x5 Gifted Subs
    //---------------------------------------------------

    [PluginActionId("com.barraider.obstools.sourcevolumesetter")]
    public class InputVolumeSetterAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    Sources = null,
                    Volume = DEFAULT_VOLUME_PERCENTAGE.ToString(),
                    SourceName = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "volume")]
            public String Volume { get; set; }

            [JsonProperty(PropertyName = "sources")]
            public List<InputBasicInfo> Sources { get; set; }

            [JsonProperty(PropertyName = "sourceName")]
            public String SourceName { get; set; }            
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

        private const int DEFAULT_VOLUME_PERCENTAGE = 100;

        private int volume = DEFAULT_VOLUME_PERCENTAGE;


        #endregion
        public InputVolumeSetterAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            base.Dispose();
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Key Pressed");
            if (OBSManager.Instance.IsConnected)
            {
                if (String.IsNullOrEmpty(Settings.SourceName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Key Pressed but SourceName is empty");
                    await Connection.ShowAlert();
                    return;
                }

                OBSManager.Instance.SetInputVolume(Settings.SourceName, volume, true);
            }
            else
            {
                await Connection.ShowAlert();
            }
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (!baseHandledOnTick)
            {
                if (!String.IsNullOrEmpty(Settings.SourceName))
                {
                    var volumeInfo = OBSManager.Instance.GetInputVolume(Settings.SourceName);
                    if (volumeInfo != null)
                    {
                        if (OBSManager.Instance.IsInputMuted(Settings.SourceName))
                        {
                            await Connection.SetTitleAsync("🔇");
                        }
                        else
                        {
                            await Connection.SetTitleAsync($"{Math.Round(volumeInfo.VolumeDb,1)} db");
                        }
                    }
                }
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
            if (!Int32.TryParse(Settings.Volume, out volume))
            {
                Settings.Volume = DEFAULT_VOLUME_PERCENTAGE.ToString();
                SaveSettings();
            }
        }

        private void Connection_OnPropertyInspectorDidAppear(object sender, SdTools.Wrappers.SDEventReceivedEventArgs<SdTools.Events.PropertyInspectorDidAppear> e)
        {
            LoadSourcesList();
            SaveSettings();
        }
        private void LoadSourcesList()
        {
            Settings.Sources = OBSManager.Instance.GetAudioInputs().OrderBy(s => s.InputName).ToList();
        }

        #endregion
    }
}