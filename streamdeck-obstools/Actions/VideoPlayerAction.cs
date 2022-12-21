using BarRaider.ObsTools.Backend;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OTI.Shared;
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
    // Subscriber: iMackk x2
    //---------------------------------------------------
    [PluginActionId("com.barraider.obstools.videoplayer")]
    public class VideoPlayerAction : KeypadActionBase
    {
        private const int HIDE_REPLAY_SECONDS = 20;
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    VideoFileName = String.Empty,
                    MuteSound = false,
                    SourceName = String.Empty,
                    HideReplaySeconds = HIDE_REPLAY_SECONDS.ToString(),
                    PlaySpeed = DEFAULT_PLAY_SPEED_PERCENTAGE.ToString()
                };
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "videoFileName")]
            public String VideoFileName { get; set; }

            [JsonProperty(PropertyName = "hideReplaySeconds")]
            public String HideReplaySeconds { get; set; }

            [JsonProperty(PropertyName = "sourceName")]
            public String SourceName { get; set; }

            [JsonProperty(PropertyName = "muteSound")]
            public bool MuteSound { get; set; }

            [JsonProperty(PropertyName = "playSpeed")]
            public String PlaySpeed { get; set; }
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

        private int hideReplaySettings = HIDE_REPLAY_SECONDS;
        private const int DEFAULT_PLAY_SPEED_PERCENTAGE = 100;

        private int speed = DEFAULT_PLAY_SPEED_PERCENTAGE;

        #endregion
        public VideoPlayerAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
            InitializeSettings();
        }

        private async void Connection_OnSendToPlugin(object sender, SdTools.Wrappers.SDEventReceivedEventArgs<SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                string fileName;
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "exportsettings":
                        fileName = PickersUtil.Pickers.SaveFilePicker("Export Video Player Settings", null, "OBS Video Player files (*.obsvplay)|*.obsvplay|All files (*.*)|*.*");
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO, $"Exporting settings to {fileName}");
                            File.WriteAllText(fileName, JsonConvert.SerializeObject(Settings));
                            await Connection.ShowOk();
                        }
                        break;
                    case "importsettings":
                        fileName = PickersUtil.Pickers.OpenFilePicker("Import Video Player Settings", null, "OBS Video Player files (*.obsvplay)|*.obsvplay|All files (*.*)|*.*");
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            if (!File.Exists(fileName))
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ImportSettings called but file does not exist {fileName}");
                                await Connection.ShowAlert();
                                return;
                            }

                            try
                            {
                                Logger.Instance.LogMessage(TracingLevel.INFO, $"Importing settings from {fileName}");
                                string json = File.ReadAllText(fileName);
                                Settings = JsonConvert.DeserializeObject<PluginSettings>(json);
                                await SaveSettings();
                                await Connection.ShowOk();
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ImportSettings exception:\n\t{ex}");
                                await Connection.ShowAlert();
                                return;
                            }
                        }
                        break;
                }
            }
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            base.Dispose();
        }

        #region Public Methods

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Video Player KeyPress");

            baseHandledKeypress = false;
            base.KeyPressed(payload);

            if (!baseHandledKeypress)
            {
                if (String.IsNullOrEmpty(Settings.VideoFileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "KeyPressed called, but no Video File configured");
                    await Connection.ShowAlert();
                    return;
                }

                if (!File.Exists(Settings.VideoFileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"KeyPressed called, but file does not exist: {Settings.VideoFileName}");
                    await Connection.ShowAlert();
                    return;
                }

                await OBSManager.Instance.PlayInstantReplay(new SourcePropertyVideoPlayer()
                {
                    VideoFileName = Settings.VideoFileName,
                    SourceName = Settings.SourceName,
                    DelayPlayStartSeconds = 0,
                    HideReplaySeconds = hideReplaySettings,
                    MuteSound = Settings.MuteSound,
                    PlaySpeedPercent = speed
                });
            }
        }

        public override void KeyReleased(KeyPayload payload)
        {
        }

        public override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            InitializeSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
        }

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        #endregion

        #region Private Methods

        private void InitializeSettings()
        {
            if (String.IsNullOrEmpty(Settings.HideReplaySeconds) || !int.TryParse(Settings.HideReplaySeconds, out hideReplaySettings))
            {
                Settings.HideReplaySeconds = HIDE_REPLAY_SECONDS.ToString();
                SaveSettings();
            }

            if (!Int32.TryParse(Settings.PlaySpeed, out speed))
            {
                Settings.PlaySpeed = DEFAULT_PLAY_SPEED_PERCENTAGE.ToString();
                SaveSettings();
            }
        }

        #endregion

    }
}