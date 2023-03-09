using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet.Types;
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
    public class InputVolumeAdjusterAction : KeypadActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    Inputs = null,
                    VolumeStep = DEFAULT_VOLUME_STEP.ToString(),
                    InputName = String.Empty,
                    TitlePrefix = String.Empty,
                    HideVolume = false
                };
                return instance;
            }

            [JsonProperty(PropertyName = "volumeStep")]
            public String VolumeStep { get; set; }

            [JsonProperty(PropertyName = "sources")]
            public List<InputBasicInfo> Inputs { get; set; }

            [JsonProperty(PropertyName = "sourceName")]
            public String InputName { get; set; }

            [JsonProperty(PropertyName = "titlePrefix")]
            public String TitlePrefix { get; set; }

            [JsonProperty(PropertyName = "hideVolume")]
            public bool HideVolume { get; set; }
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
        public InputVolumeAdjusterAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
                if (String.IsNullOrEmpty(Settings.InputName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Key Pressed but SourceName is empty");
                    await Connection.ShowAlert();
                    return;
                }

                // Get current volume
                var volumeInfo = OBSManager.Instance.GetInputVolume(Settings.InputName);
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
                    OBSManager.Instance.SetInputVolume(Settings.InputName, outputVolume, true);
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
                if (!String.IsNullOrEmpty(Settings.InputName))
                {
                    string title = String.Empty;
                    if (!Settings.HideVolume)
                    {
                        var volumeInfo = OBSManager.Instance.GetInputVolume(Settings.InputName);
                        if (volumeInfo != null)
                        {
                            if (OBSManager.Instance.IsInputMuted(Settings.InputName))
                            {
                                title = "🔇";
                            }
                            else
                            {
                                title = $"{Math.Round(volumeInfo.VolumeDb, 1)} db";
                            }
                        }
                    }
                    await Connection.SetTitleAsync($"{Settings.TitlePrefix?.Replace(@"\n", "\n")}{title}");
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
            LoadInputsList();
            SaveSettings();
        }

        private void LoadInputsList()
        {
            Settings.Inputs = null;
            if (!OBSManager.Instance.IsConnected)
            {
                return;
            }

            var inputs = OBSManager.Instance.GetAudioInputs();
            if (inputs != null)
            {
                Settings.Inputs = inputs.OrderBy(s => s?.InputName ?? "Z").ToList();
            }
        }

        #endregion
    }
}