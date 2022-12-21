using BarRaider.ObsTools.Backend;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
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
    [PluginActionId("com.barraider.obstools.imagesettings")]
    public class ImageSettingsAction : KeypadActionBase
    {
        private const int AUTO_HIDE_SECONDS = 20;
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    ImageFileName = String.Empty,
                    Scenes = null,
                    SceneName = String.Empty,
                    Inputs = null,
                    InputName = String.Empty,
                    AutoHideSettings = AUTO_HIDE_SECONDS.ToString(),
                };
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "imageFileName")]
            public String ImageFileName { get; set; }

            [JsonProperty(PropertyName = "autoHideSeconds")]
            public String AutoHideSettings { get; set; }

            [JsonProperty(PropertyName = "scenes", NullValueHandling = NullValueHandling.Ignore)]
            public List<SceneBasicInfo> Scenes { get; set; }

            [JsonProperty(PropertyName = "sceneName")]
            public String SceneName { get; set; }

            [JsonProperty(PropertyName = "inputs", NullValueHandling = NullValueHandling.Ignore)]
            public List<InputBasicInfo> Inputs { get; set; }

            [JsonProperty(PropertyName = "inputName")]
            public String InputName { get; set; }
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

        private const string IMAGE_SOURCE_TYPE = "image_source";

        private int autoHideTime = AUTO_HIDE_SECONDS;

        #endregion
        public ImageSettingsAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
                MakeBackwardsCompatibleForV5(payload.Settings);

            }

            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            base.Dispose();
        }

        #region Public Methods

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Image Settings KeyPress");

            baseHandledKeypress = false;
            base.KeyPressed(payload);

            if (!baseHandledKeypress)
            {
                if (String.IsNullOrEmpty(Settings.InputName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "KeyPressed called, but no Input set");
                    await Connection.ShowAlert();
                    return;
                }

                if (String.IsNullOrEmpty(Settings.ImageFileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "KeyPressed called, but no Image File configured");
                    await Connection.ShowAlert();
                    return;
                }

                if (!File.Exists(Settings.ImageFileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"KeyPressed called, but file does not exist: {Settings.ImageFileName}");
                    await Connection.ShowAlert();
                    return;
                }

                await OBSManager.Instance.ModifyImageSource(Settings.SceneName, Settings.InputName, Settings.ImageFileName, autoHideTime);
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
            if (String.IsNullOrEmpty(Settings.AutoHideSettings) || !int.TryParse(Settings.AutoHideSettings, out autoHideTime))
            {
                Settings.AutoHideSettings = AUTO_HIDE_SECONDS.ToString();
                SaveSettings();
            }
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
                        fileName = PickersUtil.Pickers.SaveFilePicker("Export Image Settings", null, "OBS Image Settings files (*.obsimg)|*.obsimg|All files (*.*)|*.*");
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO, $"Exporting settings to {fileName}");
                            File.WriteAllText(fileName, JsonConvert.SerializeObject(Settings));
                            await Connection.ShowOk();
                        }
                        break;
                    case "importsettings":
                        fileName = PickersUtil.Pickers.OpenFilePicker("Import Image Settings", null, "OBS Image Settings files (*.obsimg)|*.obsimg|All files (*.*)|*.*");
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

        private async void Connection_OnPropertyInspectorDidAppear(object sender, SDEventReceivedEventArgs<SdTools.Events.PropertyInspectorDidAppear> e)
        {
            await LoadScenes();
            LoadInputs();
            await SaveSettings();
        }

        private async Task LoadScenes()
        {
            Settings.Scenes = await CommonFunctions.FetchScenesAndActiveCaption();
            await SaveSettings();
        }

        private void LoadInputs()
        {
            Settings.Inputs = null;
            Settings.Inputs = OBSManager.Instance.GetAllInputs()?.Where(i => i.InputKind == IMAGE_SOURCE_TYPE)?.OrderBy(i => i.InputName)?.ToList();
        }

        private void MakeBackwardsCompatibleForV5(JObject oldSettings)
        {
            if (oldSettings.ContainsKey("sourceName") && string.IsNullOrEmpty(Settings.InputName))
            {
                Settings.InputName = (string)oldSettings["sourceName"];
                SaveSettings();
            }
        }

        #endregion

    }
}