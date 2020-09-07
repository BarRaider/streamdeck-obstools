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

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: Nachtmeister666 x2
    // Subscriber: thejediforce
    // Followers: dehinferno
    // 400 Bits: NathanOrDie
    // 300 Bits: Nachtmeister666
    //---------------------------------------------------

    [PluginActionId("com.barraider.obstools.browsersource")]
    public class BrowserSourceAction : ActionBase
    {
        private const int HIDE_SOURCE_SECONDS = 20;
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
                    HideSourceSeconds = HIDE_SOURCE_SECONDS.ToString(),
                    SourceURL = String.Empty,
                    LocalFile = false
                };
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "videoFileName")]
            public String VideoFileName { get; set; }

            [JsonProperty(PropertyName = "hideReplaySeconds")]
            public String HideSourceSeconds { get; set; }

            [JsonProperty(PropertyName = "sourceName")]
            public String SourceName { get; set; }

            [JsonProperty(PropertyName = "muteSound")]
            public bool MuteSound { get; set; }

            [JsonProperty(PropertyName = "sourceURL")]
            public String SourceURL { get; set; }

            [JsonProperty(PropertyName = "localFile")]
            public bool LocalFile { get; set; }
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

        private int hideSourceSettings = HIDE_SOURCE_SECONDS;

        #endregion
        public BrowserSourceAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

        #region Public Methods

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Browser Source KeyPress");

            baseHandledKeypress = false;
            base.KeyPressed(payload);

            if (!baseHandledKeypress)
            {
                string urlOrFile;

                // Validate parameters based on is this a local file or a URL
                if (Settings.LocalFile)
                {
                    if (String.IsNullOrEmpty(Settings.VideoFileName))
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, "KeyPressed called, but no File configured");
                        await Connection.ShowAlert();
                        return;
                    }

                    if (!File.Exists(Settings.VideoFileName))
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"KeyPressed called, but file does not exist: {Settings.VideoFileName}");
                        await Connection.ShowAlert();
                        return;
                    }
                    urlOrFile = Settings.VideoFileName;
                }
                else
                {
                    if (String.IsNullOrEmpty(Settings.SourceURL))
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, "KeyPressed called, but no URL configured");
                        await Connection.ShowAlert();
                        return;
                    }
                    urlOrFile = Settings.SourceURL;
                }

                await OBSManager.Instance.ModifyBrowserSource(urlOrFile, Settings.LocalFile, Settings.SourceName, 0, hideSourceSettings, Settings.MuteSound);
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
            if (String.IsNullOrEmpty(Settings.HideSourceSeconds) || !int.TryParse(Settings.HideSourceSeconds, out hideSourceSettings))
            {
                Settings.HideSourceSeconds = HIDE_SOURCE_SECONDS.ToString();
                SaveSettings();
            }
        }

        #endregion

    }
}