using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
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
    [PluginActionId("com.barraider.obstools.sourcemutetoggle")]
    public class InputMuteToggleAction : KeypadActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    Sources = null,
                    SourceName = String.Empty,
                };
                return instance;
            }

            [JsonProperty(PropertyName = "sources", NullValueHandling = NullValueHandling.Ignore)]
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

        private const int CHECK_STATUS_COOLDOWN_MS = 3000;
        private readonly string[] DEFAULT_IMAGES = new string[]
       {
            @"images\muteEnabled.png",
            @"images\volumeAction@2x.png"
       };

        private DateTime lastStatusCheck = DateTime.MinValue;

        #endregion
        public InputMuteToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            PrefetchMuteImages(DEFAULT_IMAGES);
        }

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            base.Dispose();
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Key Pressed");

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

            if (!OBSManager.Instance.ToggleInputMute(Settings.SourceName))
            {
                await Connection.ShowAlert();
                return;
            }
            lastStatusCheck = DateTime.MinValue;
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (!baseHandledOnTick)
            {
                if (String.IsNullOrEmpty(Settings.SourceName))
                {
                    return;
                }

                if (!OBSManager.Instance.IsConnected)
                {
                    return;
                }

                if ((DateTime.Now - lastStatusCheck).TotalMilliseconds >= CHECK_STATUS_COOLDOWN_MS)
                {
                    lastStatusCheck = DateTime.Now;
                    var isEnabled = OBSManager.Instance.IsInputMuted(Settings.SourceName);
                    await Connection.SetImageAsync(isEnabled ? enabledImage : disabledImage);
                }
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);
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

        private async void Connection_OnPropertyInspectorDidAppear(object sender, SDEventReceivedEventArgs<SdTools.Events.PropertyInspectorDidAppear> e)
        {
            LoadInputsList();
            await SaveSettings();
        }

        private void LoadInputsList()
        {
            Settings.Sources = OBSManager.Instance.GetAudioInputs().OrderBy(s => s?.InputName ?? "Z").ToList();
        }

        protected void PrefetchMuteImages(string[] defaultImages)
        {
            if (enabledImage != null)
            {
                enabledImage.Dispose();
                enabledImage = null;
            }

            if (disabledImage != null)
            {
                disabledImage.Dispose();
                disabledImage = null;
            }

            if (defaultImages.Length < 2)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} PrefetchImages: Invalid default images list");
                return;
            }

            enabledImage = Image.FromFile(IsValidFile(settings.EnabledImage) ? settings.EnabledImage : defaultImages[0]);
            disabledImage = Image.FromFile(IsValidFile(settings.DisabledImage) ? settings.DisabledImage : defaultImages[1]);
        }

        private bool IsValidFile(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                return false;
            }

            if (!File.Exists(fileName))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} IsValidFile - File not found: {fileName}");
                return false;
            }
            return true;
        }

        #endregion
    }
}