using BarRaider.ObsTools.Backend;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog.Fluent;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace BarRaider.ObsTools.Actions
{
    [PluginActionId("com.barraider.obstools.focusedwindowcapture")]
    public class FocusedWindowCaptureAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    SceneName = String.Empty,
                    Scenes = null,
                    Sources = null,
                    SourceName = String.Empty,
                };
                return instance;
            }

            [JsonProperty(PropertyName = "sceneName")]
            public String SceneName { get; set; }

            [JsonProperty(PropertyName = "scenes", NullValueHandling = NullValueHandling.Ignore)]
            public List<SceneBasicInfo> Scenes { get; set; }

            [JsonProperty(PropertyName = "sources", NullValueHandling = NullValueHandling.Ignore)]
            public List<SceneItemDetails> Sources { get; set; }

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

        private readonly string[] DEFAULT_IMAGES = new string[]
       {
            @"images\sourceAction@2x.png",
            @"images\sourceDisabled.png"
       };

        private const string WINDOW_CAPTURE_TYPE = "window_capture";

        private TitleParameters titleParameters;

        #endregion
        public FocusedWindowCaptureAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.OnTitleParametersDidChange += Connection_OnTitleParametersDidChange;
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
            PrefetchImages(DEFAULT_IMAGES);
        }

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            base.Dispose();
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Key Pressed");
            if (String.IsNullOrEmpty(Settings.SceneName))
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Key Pressed but Scene Name is empty");
                await Connection.ShowAlert();
                return;
            }

            if (String.IsNullOrEmpty(Settings.SourceName))
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Key Pressed but Source Name is empty");
                await Connection.ShowAlert();
                return;
            }

            if (!OBSManager.Instance.IsConnected)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Key pressed but OBS is not connected");
                await Connection.ShowAlert();
                return;
            }

            SetFocusedWindowCapture();
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            string sceneName = Settings.SceneName;
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            if (sceneName != Settings.SceneName)
            {
                LoadSceneSources();
            }
            PrefetchImages(DEFAULT_IMAGES);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
        }

        #region Private Methods

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        private async Task LoadScenes()
        {
            Settings.Scenes = await CommonFunctions.FetchScenesAndActiveCaption();
            await SaveSettings();
        }

        private void LoadSceneSources()
        {
            Settings.Sources = null;
            if (String.IsNullOrEmpty(Settings.SceneName))
            {
                return;
            }

            Settings.Sources = OBSManager.Instance.GetSceneSources(Settings.SceneName)?.Where(s => s.SourceKind == WINDOW_CAPTURE_TYPE)?.OrderBy(s => s.SourceName)?.ToList();
        }

        private void Connection_OnTitleParametersDidChange(object sender, SDEventReceivedEventArgs<SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e?.Event?.Payload?.TitleParameters;
        }

        private async void Connection_OnPropertyInspectorDidAppear(object sender, SDEventReceivedEventArgs<SdTools.Events.PropertyInspectorDidAppear> e)
        {
            await LoadScenes();
            LoadSceneSources();
            await SaveSettings();
        }

        private async void SetFocusedWindowCapture()
        {
            try
            {
                string windowInfo = OTI.Shared.HelperUtils.GetForegroundWindowCaptureString();
                if (string.IsNullOrEmpty(windowInfo))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} SetFocusedWindowCapture focused window info not found");
                    await Connection.ShowAlert();
                    return;
                }

                if (OBSManager.Instance.SetWindowCaptureWindow(Settings.SourceName, windowInfo))
                {
                    await Connection.ShowOk();
                }
                else
                {
                    await Connection.ShowAlert();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} SetFocusedWindowCapture Exception: {ex}");
            }
        }

        #endregion
    }
}