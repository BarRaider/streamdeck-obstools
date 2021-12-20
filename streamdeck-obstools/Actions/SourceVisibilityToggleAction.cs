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

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: Tek_Soup
    // 100 Bits: CyberlightGames
    // Subscriber: nubby_ninja
    // tobitege - 5 Gifted Subs
    // CarstenPet - 5 Gifted Subs
    //---------------------------------------------------

    [PluginActionId("com.barraider.obstools.sourcetoggle")]
    public class SourceVisibilityToggleAction : ActionBase
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
            public List<OBSScene> Scenes { get; set; }

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

        private const string ACTIVE_SCENE_NAME = "- Active Scene -";

        private TitleParameters titleParameters;

        #endregion
        public SourceVisibilityToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

            if (payload.IsInMultiAction)
            {
                await HandleMultiActionKeypress(payload.UserDesiredState);
                return;
            }

            string sceneName = Settings.SceneName;
            if (Settings.SceneName == ACTIVE_SCENE_NAME)
            {
                sceneName = null;
            }

            if (!OBSManager.Instance.ToggleSourceVisibility(sceneName, Settings.SourceName))
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
                if (String.IsNullOrEmpty(Settings.SceneName) || String.IsNullOrEmpty(Settings.SourceName))
                {
                    return;
                }

                if (!OBSManager.Instance.IsConnected)
                {
                    return;
                }

                string sceneName = Settings.SceneName;
                if (Settings.SceneName == ACTIVE_SCENE_NAME)
                {
                    sceneName = null;
                }

                await DrawImage(OBSManager.Instance.IsSourceVisible(sceneName, Settings.SourceName));
            }
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

        private Task LoadScenes()
        {
            return Task.Run(async () =>
            {
                Settings.Scenes = new List<OBSScene>
                {
                    new OBSScene
                    {
                        Name = ACTIVE_SCENE_NAME
                    }
                };
                int retries = 40;

                while (!OBSManager.Instance.IsConnected && retries > 0)
                {
                    retries--;
                    await Task.Delay(250);
                }

                var scenes = OBSManager.Instance.GetAllScenes();
                if (scenes != null && scenes.Scenes != null)
                {
                    Settings.Scenes.AddRange(scenes.Scenes.OrderBy(s => s.Name).ToList());
                }
                await SaveSettings();
            });
        }

        private void LoadSceneSources()
        {
            Settings.Sources = null;
            if (String.IsNullOrEmpty(Settings.SceneName))
            {
                return;
            }

            Settings.Sources = OBSManager.Instance.GetSceneSources(Settings.SceneName)?.OrderBy(s => s.SourceName)?.ToList();
        }

        private void Connection_OnTitleParametersDidChange(object sender, SDEventReceivedEventArgs<SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e?.Event?.Payload?.TitleParameters;
        }

        private async Task DrawImage(bool sourceVisible)
        {
            if (sourceVisible)
            {
                await Connection.SetImageAsync(enabledImage);
            }
            else
            {
                await Connection.SetImageAsync(disabledImage);
            }
        }

        private async Task HandleMultiActionKeypress(uint state)
        {
            string sceneName = Settings.SceneName;
            if (Settings.SceneName == ACTIVE_SCENE_NAME)
            {
                sceneName = null;
            }
            bool isVisible;
            switch (state) // 0 = Toggle, 1 = Show, 2 = Hide
            {
                case 0:
                    if (!OBSManager.Instance.ToggleSourceVisibility(sceneName, Settings.SourceName))
                    {
                        await Connection.ShowAlert();
                    }
                    break;
                case 1: // Show
                    isVisible = OBSManager.Instance.IsSourceVisible(sceneName, Settings.SourceName);
                    if (isVisible)
                    {
                        return;
                    }
                    if (!OBSManager.Instance.ToggleSourceVisibility(sceneName, Settings.SourceName))
                    {
                        await Connection.ShowAlert();
                    }
                    break;
                case 2: // Hide
                    isVisible = OBSManager.Instance.IsSourceVisible(sceneName, Settings.SourceName);
                    if (!isVisible)
                    {
                        return;
                    }
                    if (!OBSManager.Instance.ToggleSourceVisibility(sceneName, Settings.SourceName))
                    {
                        await Connection.ShowAlert();
                    }
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleMultiActionKeypress Invalid state: {state}");
                    return;
            }
        }

        private async void Connection_OnPropertyInspectorDidAppear(object sender, SDEventReceivedEventArgs<SdTools.Events.PropertyInspectorDidAppear> e)
        {
            await LoadScenes();
            LoadSceneSources();
            await SaveSettings();
        }


        #endregion
    }
}