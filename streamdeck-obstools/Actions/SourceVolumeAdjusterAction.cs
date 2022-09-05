using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
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
    // Subscriber: SP__LIT
    //---------------------------------------------------
    [PluginActionId("com.barraider.obstools.sourcevolumeadjuster")]
    public class SourceVolumeAdjusterAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    Sources = null,
                    VolumeStep = DEFAULT_VOLUME_STEP.ToString(),
                    SourceName = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "volumeStep")]
            public String VolumeStep { get; set; }

            [JsonProperty(PropertyName = "sources")]
            public List<SceneSourceInfo> Sources { get; set; }

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

        private const int DEFAULT_VOLUME_STEP = 5;
        private const float MINIMAL_DB_VALUE = -95.8f;

        private int volumeStep = DEFAULT_VOLUME_STEP;


        #endregion
        public SourceVolumeAdjusterAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

                // Get current volume
                var volumeInfo = OBSManager.Instance.GetInputVolume(Settings.SourceName);
                if (volumeInfo != null)
                {
                    float outputVolume = volumeInfo.VolumeDb + volumeStep;
                    if (outputVolume > 0)
                    {
                        outputVolume = 0;
                    }
                    if (outputVolume < MINIMAL_DB_VALUE)
                    {
                        outputVolume = MINIMAL_DB_VALUE;
                    }
                    OBSManager.Instance.SetInputVolume(Settings.SourceName, outputVolume, true);
                }
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
                            await Connection.SetTitleAsync($"{Math.Round(volumeInfo.VolumeDb, 1)} db");
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
            if (!Int32.TryParse(Settings.VolumeStep, out volumeStep))
            {
                Settings.VolumeStep = DEFAULT_VOLUME_STEP.ToString();
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
            Settings.Sources = OBSManager.Instance.GetAllSceneAndSceneItemNames();
        }

        #endregion
    }
}