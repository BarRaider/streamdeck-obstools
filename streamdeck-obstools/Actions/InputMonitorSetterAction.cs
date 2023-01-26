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

    [PluginActionId("com.barraider.obstools.sourcemonitorsetter")]
    public class InputMonitorSetterAction : KeypadActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    Inputs = null,
                    MonitorType = DEFAULT_MONITOR_TYPE,
                    InputName = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "volume")]
            public String Volume { get; set; }

            [JsonProperty(PropertyName = "sources")]
            public List<InputBasicInfo> Inputs { get; set; }

            [JsonProperty(PropertyName = "sourceName")]
            public String InputName { get; set; }

            [JsonProperty(PropertyName = "monitorType")]
            public MonitorTypes MonitorType { get; set; }
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

        private const MonitorTypes DEFAULT_MONITOR_TYPE = MonitorTypes.None;
        private const int CHECK_STATUS_COOLDOWN_MS = 3000;
        private readonly string[] DEFAULT_IMAGES = new string[]
        {
            @"images\monitorEnabled.png",
            @"images\volumeAction@2x.png"
        };
        private DateTime lastStatusCheck = DateTime.MinValue;

        #endregion
        public InputMonitorSetterAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

                OBSManager.Instance.SetInputAudioMonitorType(Settings.InputName, Settings.MonitorType);
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
                if (!String.IsNullOrEmpty(Settings.InputName) && (DateTime.Now - lastStatusCheck).TotalMilliseconds >= CHECK_STATUS_COOLDOWN_MS)
                {
                    lastStatusCheck = DateTime.Now;
                    var monitorType = OBSManager.Instance.GetInputAudioMonitorType(Settings.InputName);
                    await Connection.SetImageAsync(monitorType == Settings.MonitorType ? enabledImage : disabledImage);
                }
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            MonitorTypes monitorType = Settings.MonitorType;
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            if (monitorType != Settings.MonitorType)
            {
                lastStatusCheck = DateTime.MinValue;
            }
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
            PrefetchImages(DEFAULT_IMAGES);
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