using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog.Fluent;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace BarRaider.ObsTools.Actions
{
    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: TavernFire
    // Subscriber: CyberlightGames Gifted Sub
    // Subscriber: icessassin
    //---------------------------------------------------

    [PluginActionId("com.barraider.obstools.smartsceneswitcher")]
    public class SmartSceneSwitcherAction : ActionBase
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
                    OverrideTransition = true,
                    PreviewColor = DEFAULT_PREVIEW_COLOR,
                    LiveColor = DEFAULT_LIVE_COLOR,
                    ShowPreview = false,
                    CustomImage = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "sceneName")]
            public String SceneName { get; set; }

            [JsonProperty(PropertyName = "overrideTransition")]
            public bool OverrideTransition { get; set; }

            [JsonProperty(PropertyName = "liveColor")]
            public String LiveColor { get; set; }

            [JsonProperty(PropertyName = "previewColor")]
            public String PreviewColor { get; set; }

            [JsonProperty(PropertyName = "showPreview")]
            public bool ShowPreview { get; set; }

            [JsonProperty(PropertyName = "scenes")]
            public List<OBSScene> Scenes { get; set; }
            
            [FilenameProperty]
            [JsonProperty(PropertyName = "customImage")]
            public String CustomImage { get; set; }
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
        private static readonly object objRevertTransition = new object();

        private const int SCENE_BORDER_SIZE = 20;
        private const int MAX_EXPERIMENTAL_RETRIES = 5;
        private const string DEFAULT_PREVIEW_COLOR = "#FFA500";
        private const string DEFAULT_LIVE_COLOR = "#FF0000";
        private const int SNAPSHOT_COOLDOWN_TIME_MS = 5000;

        private string revertTransition = String.Empty;
        private GlobalSettings global;
        private TitleParameters titleParameters;
        private int experimentalScreenshotRetries = MAX_EXPERIMENTAL_RETRIES;
        private bool isFetchingScreenshot = false;
        private string lastSnapshotImageData = null;
        private DateTime lastSnapshotTime = DateTime.MinValue;
        private Image customImage;

        #endregion
        public SmartSceneSwitcherAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            GlobalSettingsManager.Instance.RequestGlobalSettings();
            OBSManager.Instance.Connect();
            OBSManager.Instance.SceneChanged += SceneChanged_RevertTransition;
            CheckServerInfoExists();
            InitializeSettings();
            LoadScenes();
        }

        public override void Dispose()
        {
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            OBSManager.Instance.SceneChanged -= SceneChanged_RevertTransition;
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

            if (!OBSManager.Instance.IsConnected)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "Key pressed but OBS is not connected");
                await Connection.ShowAlert();
                return;
            }

            if (payload.IsInMultiAction && payload.UserDesiredState > 0) // 0 = Standard, 1 = Force Studio, 2 = Force Live
            {
                if (HandleMultiActionKeypress(payload.UserDesiredState))
                {
                    await Connection.ShowOk();
                }
                else
                {
                    await Connection.ShowAlert();
                }
                return;
            }

            // Check if should move to Studio mode
            if (OBSManager.Instance.IsStudioModeEnabled() && OBSManager.Instance.CurrentPreviewSceneName != Settings.SceneName)
            {
                if (OBSManager.Instance.SetPreviewScene(Settings.SceneName))
                {
                    await Connection.ShowOk();
                }
                else
                {
                    await Connection.ShowAlert();
                }
            }
            else
            {
                if (HandleTransitionToLive())
                {
                    await Connection.ShowOk();
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "HandleTransitionToLive returned false");
                    await Connection.ShowAlert();
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
                if (String.IsNullOrEmpty(Settings.SceneName) || titleParameters == null)
                {
                    return;
                }
                await Connection.SetTitleAsync(GraphicsTools.WrapStringToFitImage(Settings.SceneName, titleParameters)); 
                if (!String.IsNullOrEmpty(Settings.SceneName) && !isFetchingScreenshot)
                {
                    // Run in task due to possible long wait times
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            isFetchingScreenshot = true;
                            _ = DrawSceneBorder();
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"SmartSceneSwitcherAction OnTick Exception: {ex}");
                        }
                        finally
                        {
                            isFetchingScreenshot = false;
                        }
                    });
                }
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            SetGlobalSettings();
            InitializeSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
            // Global Settings exist
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                global = payload.Settings.ToObject<GlobalSettings>();
                Settings.LiveColor = global.SceneSwitchLiveColor;
                Settings.PreviewColor = global.SceneSwitchPreviewColor;

                // First load
                if (String.IsNullOrEmpty(Settings.LiveColor) || String.IsNullOrEmpty(Settings.PreviewColor))
                {
                    Settings.LiveColor = DEFAULT_LIVE_COLOR;
                    Settings.PreviewColor = DEFAULT_PREVIEW_COLOR;
                    SetGlobalSettings();
                }

                SaveSettings();
            }
            else // Global settings do not exist
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"SmartSceneSwitcher received empty payload: {payload}, creating new instance");
                global = new GlobalSettings();
                SetGlobalSettings();
            }
        }

        #region Private Methods

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        private void SetGlobalSettings()
        {
            global.SceneSwitchLiveColor = Settings.LiveColor;
            global.SceneSwitchPreviewColor = Settings.PreviewColor;
            Connection.SetGlobalSettingsAsync(JObject.FromObject(global));
        }

        private async Task DrawSceneBorder()
        {
            using (Image img = Tools.GenerateGenericKeyImage(out Graphics graphics))
            {
                int height = img.Height;
                int width = img.Width;
                Color borderColor = Color.Black;
                graphics.PageUnit = GraphicsUnit.Pixel;

                if (OBSManager.Instance.CurrentSceneName == Settings.SceneName)
                {
                    // Draw Live Border
                    borderColor = ColorTranslator.FromHtml(Settings.LiveColor);
                }
                else if (OBSManager.Instance.CurrentPreviewSceneName == Settings.SceneName)
                {
                    // Draw Preview Border
                    borderColor = ColorTranslator.FromHtml(Settings.PreviewColor);
                }

                if (OBSManager.Instance.IsConnected)
                {

                    if (Settings.ShowPreview)
                    {
                        await DrawPreviewImage(graphics, width, height);
                    }
                    else if (customImage != null)
                    {
                        graphics.DrawImage(customImage, new Rectangle(0, 0, width, height));
                    }
                }

                // Draw border
                graphics.DrawRectangle(new Pen(borderColor, SCENE_BORDER_SIZE), new Rectangle(0, 0, width, height));

                await Connection.SetImageAsync(img);
                graphics.Dispose();
            }
        }

        private void SceneChanged_RevertTransition(object sender, SceneChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(revertTransition))
            {
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    lock (objRevertTransition)
                    {
                        if (!String.IsNullOrEmpty(revertTransition))
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO, $"Reverting back to original transition");
                            OBSManager.Instance.SetTransition(revertTransition);
                        }
                        revertTransition = String.Empty;
                    }
                });
            }
        }

        private void LoadScenes()
        {
            Task.Run(async () =>
            {
                Settings.Scenes = null;
                int retries = 40;

                while (!OBSManager.Instance.IsConnected && retries > 0)
                {
                    retries--;
                    await Task.Delay(250);
                }

                var scenes = OBSManager.Instance.GetAllScenes();
                if (scenes != null && scenes.Scenes != null)
                {
                    Settings.Scenes = scenes.Scenes;
                }
                await SaveSettings();
            });
        }

        private bool HandleTransitionToLive()
        {
            if (Settings.OverrideTransition && OBSManager.Instance.CurrentSceneName == Settings.SceneName && String.IsNullOrEmpty(revertTransition))
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, "Overriding transition");
                var transition = OBSManager.Instance.GetTransition();
                revertTransition = transition?.Name;
                OBSManager.Instance.SetTransition("Fade");
            }

            return OBSManager.Instance.SetScene(Settings.SceneName);
        }

        private bool HandleMultiActionKeypress(uint desiredState)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"SmartSceneSwitcher HandleMultiActionKeypress received: {desiredState}");
            switch (desiredState)
            {
                case 1: // Force Studio
                    if (!OBSManager.Instance.IsStudioModeEnabled())
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"SmartSceneSwitcher HandleMultiActionKeypress - Force Studio requested but Studio mode is not enabled");
                        return false;
                    }
                    return OBSManager.Instance.SetPreviewScene(Settings.SceneName);
                case 2: // Force Live
                    return HandleTransitionToLive();
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"SmartSceneSwitcher HandleMultiActionKeypress - Invalid state received: {desiredState}");
                    break;
            }
            return false;
        }

        private void Connection_OnTitleParametersDidChange(object sender, SDEventReceivedEventArgs<SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e.Event.Payload.TitleParameters;
        }

        private void InitializeSettings()
        {
            if (customImage != null)
            {
                customImage.Dispose();
                customImage = null;
            }

            if (IsValidFile(Settings.CustomImage))
            {
                customImage = Image.FromFile(Settings.CustomImage);
            }
        }

        private bool IsValidFile(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                return false;
            }

            if (!File.Exists(fileName))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"File not found: {fileName}");
                return false;
            }
            return true;
        }

        private async Task DrawPreviewImage(Graphics graphics, int width, int height)
        {
            try
            {
                if ((DateTime.Now - lastSnapshotTime).TotalMilliseconds <= SNAPSHOT_COOLDOWN_TIME_MS)
                {
                    using (Image background = Tools.Base64StringToImage(lastSnapshotImageData))
                    {
                        graphics.DrawImage(background, new Rectangle(0, 0, width, height));
                    }
                }
                else
                {
                    // Get update snapshot of source
                    var snapshot = OBSManager.Instance.GetSourceSnapshot(Settings.SceneName);
                    if (snapshot == null)
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"DrawSceneBorder GetSourceSnapshot returned null");
                        experimentalScreenshotRetries--;
                    }
                    else if (snapshot != null && !String.IsNullOrEmpty(snapshot.ImageData))
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"DrawSceneBorder Got updated snapshot for {Settings.SceneName}");
                        lastSnapshotTime = DateTime.Now;
                        lastSnapshotImageData = snapshot.ImageData;
                        experimentalScreenshotRetries = MAX_EXPERIMENTAL_RETRIES;
                        using (Image background = Tools.Base64StringToImage(lastSnapshotImageData))
                        {
                            graphics.DrawImage(background, new Rectangle(0, 0, width, height));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"DrawSceneBorder GetSnapshot Exception {ex}");
                experimentalScreenshotRetries--;
            }

            if (experimentalScreenshotRetries <= 0)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"experimentalScreenshotRetries limit hit - Disabling ShowPreview!");
                Settings.ShowPreview = false;
                await SaveSettings();
            }
        }

        #endregion
    }
}