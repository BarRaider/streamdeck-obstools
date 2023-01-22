using BarRaider.ObsTools.Backend;
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
using System.Runtime.Remoting.Contexts;
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
    public class SmartSceneSwitcherAction : KeypadActionBase
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
            public List<SceneBasicInfo> Scenes { get; set; }
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
        private readonly string[] DEFAULT_IMAGES = new string[]
        {
            @"images\noicon.png",
            @"images\noicon.png"
        };

        private string revertTransition = String.Empty;
        private TitleParameters titleParameters;
        private int experimentalScreenshotRetries = MAX_EXPERIMENTAL_RETRIES;
        private bool isFetchingScreenshot = false;
        private string lastSnapshotImageData = null;
        private DateTime lastSnapshotTime = DateTime.MinValue;

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
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Key pressed but OBS is not connected");
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
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"HandleMultiActionKeypress returned false");
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
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"SetPreviewScene returned false");
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
                    Logger.Instance.LogMessage(TracingLevel.WARN, "HandleTransitionToLive returned false");
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
            SetSceneSwitchColors();
            InitializeSettings();
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

        private void SetSceneSwitchColors()
        {
            SmartSceneSwitcherManager.Instance.SetColors(Settings.LiveColor, Settings.PreviewColor);
        }

        private async Task DrawSceneBorder()
        {
            using Image img = Tools.GenerateGenericKeyImage(out Graphics graphics);
            int height = img.Height;
            int width = img.Width;
            Color borderColor = Color.Black;
            graphics.PageUnit = GraphicsUnit.Pixel;

            if (OBSManager.Instance.CurrentSceneName == Settings.SceneName)
            {
                // Draw Live Border
                borderColor = ColorTranslator.FromHtml(SmartSceneSwitcherManager.Instance.SceneSwitchLiveColor);
            }
            else if (OBSManager.Instance.CurrentPreviewSceneName == Settings.SceneName)
            {
                // Draw Preview Border
                borderColor = ColorTranslator.FromHtml(SmartSceneSwitcherManager.Instance.SceneSwitchPreviewColor);
            }

            if (OBSManager.Instance.IsConnected)
            {

                if (Settings.ShowPreview)
                {
                    await DrawPreviewImage(graphics, width, height);
                }
                else if (enabledImage != null)
                {
                    graphics.DrawImage(enabledImage, new Rectangle(0, 0, width, height));
                }
            }

            // Draw border
            graphics.DrawRectangle(new Pen(borderColor, SCENE_BORDER_SIZE), new Rectangle(0, 0, width, height));

            await Connection.SetImageAsync(img);
            graphics.Dispose();
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
                var transition = OBSManager.Instance.GetCurrentTransition();
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
            try
            {
                titleParameters = e.Event.Payload.TitleParameters;
                Logger.Instance.LogMessage(TracingLevel.DEBUG, $"{this.GetType()} Got TitleParametersDidChange for {e.Event.Context} {Settings.SceneName}");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SmartSceneSwitcher TitleParams: Keyup Cache miss for {e.Event.Context} {ex}");
            }

            if (titleParameters == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SmartSceneSwitcher TitleParams null for {e.Event.Context}");
            }
        }

        private void InitializeSettings()
        {
            PrefetchImages(DEFAULT_IMAGES);

            Settings.LiveColor = SmartSceneSwitcherManager.Instance.SceneSwitchLiveColor;
            Settings.PreviewColor = SmartSceneSwitcherManager.Instance.SceneSwitchPreviewColor;

            // First load
            if (String.IsNullOrEmpty(Settings.LiveColor) || String.IsNullOrEmpty(Settings.PreviewColor))
            {
                Settings.LiveColor = DEFAULT_LIVE_COLOR;
                Settings.PreviewColor = DEFAULT_PREVIEW_COLOR;
                SetSceneSwitchColors();
            }

            SaveSettings();

        }

        private async Task DrawPreviewImage(Graphics graphics, int width, int height)
        {
            try
            {
                if ((DateTime.Now - lastSnapshotTime).TotalMilliseconds <= SNAPSHOT_COOLDOWN_TIME_MS)
                {
                    using Image background = Tools.Base64StringToImage(lastSnapshotImageData);
                    graphics.DrawImage(background, new Rectangle(0, 0, width, height));
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
                    else if (snapshot != null && !String.IsNullOrEmpty(snapshot))
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"DrawSceneBorder Got updated snapshot for {Settings.SceneName}");
                        lastSnapshotTime = DateTime.Now;
                        lastSnapshotImageData = snapshot;
                        experimentalScreenshotRetries = MAX_EXPERIMENTAL_RETRIES;
                        using Image background = Tools.Base64StringToImage(lastSnapshotImageData);
                        graphics.DrawImage(background, new Rectangle(0, 0, width, height));
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