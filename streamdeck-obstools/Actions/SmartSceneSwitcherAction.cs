using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                    OverrideTransition = true,
                    PreviewColor = DEFAULT_PREVIEW_COLOR,
                    LiveColor = DEFAULT_LIVE_COLOR,
                    ShowPreview = false
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
        private const int STRING_SPLIT_SIZE = 7;
        private const string DEFAULT_PREVIEW_COLOR = "#FFA500";
        private const string DEFAULT_LIVE_COLOR = "#FF0000";

        private string revertTransition = String.Empty;
        private GlobalSettings global;


        #endregion
        public SmartSceneSwitcherAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            GlobalSettingsManager.Instance.RequestGlobalSettings();
            OBSManager.Instance.Connect();
            OBSManager.Instance.SceneChanged += SceneChanged_RevertTransition;
            CheckServerInfoExists();
            LoadScenes();
        }

        public override void Dispose()
        {
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

            if (OBSManager.Instance.IsConnected)
            {
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
                    if (Settings.OverrideTransition && OBSManager.Instance.CurrentSceneName == Settings.SceneName && String.IsNullOrEmpty(revertTransition))
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, "Overriding transition");
                        var transition = OBSManager.Instance.GetTransition();
                        revertTransition = transition?.Name;
                        OBSManager.Instance.SetTransition("Fade");
                    }

                    if (OBSManager.Instance.SetScene(Settings.SceneName))
                    {
                        await Connection.ShowOk();
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, "SetScene returned false");
                        await Connection.ShowAlert();
                    }
                }

            }
        }

        public override void KeyReleased(KeyPayload payload)  { }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (!baseHandledOnTick)
            {
                await Connection.SetTitleAsync(SplitLongWord(Settings.SceneName));
                if (!String.IsNullOrEmpty(Settings.SceneName))
                {
                    await DrawSceneBorder();
                }
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            SetGlobalSettings();
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

                if (Settings.ShowPreview)
                {
                    try
                    {
                        // Draw image preview
                        var snapshot = OBSManager.Instance.GetSourceSnapshot(Settings.SceneName);
                        if (snapshot != null && snapshot.ImageData != null)
                        {
                            using (Image background = Tools.Base64StringToImage(snapshot.ImageData))
                            {
                                graphics.DrawImage(background, new Rectangle(0, 0, width, height));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"DrawSceneBorder GetSnapshot Exception {ex}");
                    }
                }

                // Draw border
                graphics.DrawRectangle(new Pen(borderColor, SCENE_BORDER_SIZE), new Rectangle(0, 0, width, height) );

                await Connection.SetImageAsync(img);
                graphics.Dispose();
            }
        }

        private string SplitLongWord(string word)
        {
            if (String.IsNullOrEmpty(word))
            {
                return word;
            }

            // Split up to 4 lines
            for (int idx = 0; idx < 3; idx++)
            {
                int cutSize = STRING_SPLIT_SIZE * (idx + 1);
                if (word.Length > cutSize)
                {
                    word = $"{word.Substring(0, cutSize)}\n{word.Substring(cutSize)}";
                }
            }
            return word;
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

        #endregion
    }
}