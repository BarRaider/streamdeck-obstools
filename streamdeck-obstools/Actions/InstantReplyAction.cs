﻿using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
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

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // 100 Bits: siliconart
    // Subscriber: ELGNTV
    // 100 Bits: Nachtmeister666
    // Subscriber: nubby_ninja x4
    // Subscriber: nubby_ninja x5 Gifted Subs
    //---------------------------------------------------

    [PluginActionId("com.barraider.obstools.instantreply")]
    public class InstantReplyAction : KeypadActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    TwitchTokenExists = false,
                    AutoReplay = false,
                    ReplayDirectory = String.Empty,
                    MuteSound = false,
                    Scenes = null,
                    SceneName = String.Empty,
                    Inputs = null,
                    InputName = String.Empty,
                    HideReplaySeconds = HIDE_REPLAY_SECONDS.ToString(),
                    DelayReplaySeconds = DELAY_REPLAY_SECONDS.ToString(),
                    TwitchIntegration = false,
                    TwitchClip = false,
                    ChatReplay = false,
                    ReplayCooldown = "30",
                    AllowedUsers = String.Empty,
                    PlaySpeed = DEFAULT_PLAY_SPEED_PERCENTAGE.ToString(),
                    AutoSwitch = false
                };
                return instance;
            }

            [JsonProperty(PropertyName = "replayDirectory")]
            public String ReplayDirectory { get; set; }

            [JsonProperty(PropertyName = "autoReplay")]
            public bool AutoReplay { get; set; }

            [JsonProperty(PropertyName = "delayReplaySeconds")]
            public String DelayReplaySeconds { get; set; }

            [JsonProperty(PropertyName = "hideReplaySeconds")]
            public String HideReplaySeconds { get; set; }

            [JsonProperty(PropertyName = "scenes", NullValueHandling = NullValueHandling.Ignore)]
            public List<SceneBasicInfo> Scenes { get; set; }

            [JsonProperty(PropertyName = "sceneName")]
            public String SceneName { get; set; }

            [JsonProperty(PropertyName = "inputs", NullValueHandling = NullValueHandling.Ignore)]
            public List<InputBasicInfo> Inputs { get; set; }

            [JsonProperty(PropertyName = "inputName")]
            public String InputName { get; set; }

            [JsonProperty(PropertyName = "muteSound")]
            public bool MuteSound { get; set; }

            [JsonProperty(PropertyName = "twitchIntegration")]
            public bool TwitchIntegration { get; set; }

            [JsonProperty(PropertyName = "twitchClip")]
            public bool TwitchClip { get; set; }

            [JsonProperty(PropertyName = "chatReplay")]
            public bool ChatReplay { get; set; }

            [JsonProperty(PropertyName = "replayCooldown")]
            public string ReplayCooldown { get; set; }

            [JsonProperty(PropertyName = "allowedUsers")]
            public string AllowedUsers { get; set; }

            [JsonProperty(PropertyName = "twitchTokenExists")]
            public bool TwitchTokenExists { get; set; }

            [JsonProperty(PropertyName = "playSpeed")]
            public String PlaySpeed { get; set; }

            [JsonProperty(PropertyName = "autoSwitch")]
            public bool AutoSwitch { get; set; }
            

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

        private const int HIDE_REPLAY_SECONDS = 20;
        private const int DEFAULT_REPLAY_COOLDOWN = 30;
        private const int DELAY_REPLAY_SECONDS = 1;
        private const int LONG_KEYPRESS_LENGTH = 600;
        private const int DEFAULT_PLAY_SPEED_PERCENTAGE = 100;
        private const string MEDIA_PLAYER_TYPE = "ffmpeg_source";

        private readonly string[] DEFAULT_IMAGES = new string[]
        {
            @"images\replayEnabled.png",
            @"images\replayAction@2x.png"
        };


        private bool keyPressed = false;
        private bool longKeyPressed = false;
        private DateTime keyPressStart;
        private GlobalSettings global;
        private int hideReplaySettings = HIDE_REPLAY_SECONDS;
        private int replayCooldown = DEFAULT_REPLAY_COOLDOWN;
        private int delayReplaySettings = DELAY_REPLAY_SECONDS;
        private int speed = DEFAULT_PLAY_SPEED_PERCENTAGE;

        #endregion
        public InstantReplyAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.GetGlobalSettingsAsync();
            ChatPager.Twitch.TwitchChat.Instance.PageRaised += Instance_PageRaised;
            ChatPager.Twitch.TwitchTokenManager.Instance.TokenStatusChanged += Instance_TokenStatusChanged;
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            ChatPager.Twitch.TwitchChat.Instance.PageRaised -= Instance_PageRaised;
            ChatPager.Twitch.TwitchTokenManager.Instance.TokenStatusChanged -= Instance_TokenStatusChanged;
            base.Dispose();
        }

        #region Public Methods

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Instant Replay KeyPress");
            keyPressed = true;
            longKeyPressed = false;
            keyPressStart = DateTime.Now;

            baseHandledKeypress = false;
            base.KeyPressed(payload);
        }

        public async override void KeyReleased(KeyPayload payload)
        {
            keyPressed = false;
            if (!baseHandledKeypress && !longKeyPressed) // Short keypress
            {
                if (payload.IsInMultiAction)
                {
                    await HandleMultiAction(payload);
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Instant Replay Short KeyPress");
                    await HandleInstantReplayRequest();
                }
            }
        }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (keyPressed)
            {
                int timeKeyWasPressed = (int)(DateTime.Now - keyPressStart).TotalMilliseconds;
                if (timeKeyWasPressed >= LONG_KEYPRESS_LENGTH && !longKeyPressed) // User is issuing a long keypress
                {
                    LongKeyPress();
                }
            }

            if (!baseHandledOnTick)
            {
                await Connection.SetImageAsync(OBSManager.Instance.IsReplayBufferEnabled() ? enabledImage : disabledImage);
                await Connection.SetTitleAsync($"Buffer\n{(OBSManager.Instance.IsReplayBufferEnabled() ? "On" : "Off")}");
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            InitializeSettings();
            SetGlobalSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
            try
            {
                // Global Settings exist
                if (payload?.Settings != null && payload.Settings.Count > 0)
                {
                    global = payload.Settings.ToObject<GlobalSettings>();

                    if (global.InstantReplaySettings == null)
                    {
                        global.InstantReplaySettings = new GlobalInstantReplaySettings();
                        SetGlobalSettings();
                        return;
                    }

                    Settings.SceneName = global.InstantReplaySettings.SceneName;
                    Settings.AutoReplay = global.InstantReplaySettings.AutoReplay;
                    Settings.ReplayDirectory = global.InstantReplaySettings.ReplayDirectory;
                    Settings.HideReplaySeconds = global.InstantReplaySettings.HideReplaySeconds.ToString();
                    Settings.InputName = global.InstantReplaySettings.InputName;
                    Settings.MuteSound = global.InstantReplaySettings.MuteSound;
                    Settings.PlaySpeed = global.InstantReplaySettings.PlaySpeed.ToString();
                    Settings.AutoSwitch = global.InstantReplaySettings.AutoSwitch;
                    InitializeSettings();
                    SaveSettings();
                }
                else // Global settings do not exist
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"InstantReplayAction received empty payload: {payload}, creating new instance");
                    global = new GlobalSettings();
                    SetGlobalSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} ReceivedGlobalSettings Exception: {ex}");
            }
        }

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        #endregion

        #region Private Methods

        private async Task HandleMultiAction(KeyPayload payload)
        {
            switch (payload.UserDesiredState) // 0 = Enable, 1 = Disable, 2 = Create Replay
            {
                case 0:
                    if (!OBSManager.Instance.IsReplayBufferEnabled())
                    {
                        OBSManager.Instance.StartInstantReplay();
                    }
                    break;
                case 1:
                    if (OBSManager.Instance.IsReplayBufferEnabled())
                    {
                        OBSManager.Instance.StopInstantReplay();
                    }
                    break;
                case 2:
                    if (OBSManager.Instance.IsReplayBufferEnabled())
                    {
                        await HandleInstantReplayRequest();
                    }
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Invalid MultiAction State: {payload.UserDesiredState}");
                    break;
            }
        }


        private void LongKeyPress()
        {
            longKeyPressed = true;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Instant Replay LongKeyPressed");
            try
            {
                if (OBSManager.Instance.IsReplayBufferEnabled())
                {
                    // Disable Instant Reply Buffer
                    if (OBSManager.Instance.StopInstantReplay())
                    {
                        Connection.ShowOk();
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"Instant Replay StopInstantReplay Failed");
                        Connection.ShowAlert();
                    }
                }
                else
                {
                    // Enable Instant Reply Buffer
                    if (OBSManager.Instance.StartInstantReplay())
                    {
                        Connection.ShowOk();
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"Instant Replay StartInstantReplay Failed");
                        Connection.ShowAlert();
                    }
                }
                OnTick();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Instant Replay LongKeyPress Exception: {ex}");
                Connection.ShowAlert();
            }
        }

        private void SetGlobalSettings()
        {
            if (global.InstantReplaySettings == null)
            {
                global.InstantReplaySettings = new GlobalInstantReplaySettings();
            }
            global.InstantReplaySettings.AutoReplay = Settings.AutoReplay;
            global.InstantReplaySettings.ReplayDirectory = Settings.ReplayDirectory;
            global.InstantReplaySettings.HideReplaySeconds = hideReplaySettings;
            global.InstantReplaySettings.MuteSound = Settings.MuteSound;
            global.InstantReplaySettings.SceneName = Settings.SceneName;
            global.InstantReplaySettings.InputName = Settings.InputName;
            global.InstantReplaySettings.DelayReplaySeconds = delayReplaySettings;
            global.InstantReplaySettings.PlaySpeed = speed;
            global.InstantReplaySettings.AutoSwitch = Settings.AutoSwitch;
            Connection.SetGlobalSettingsAsync(JObject.FromObject(global));
        }

        private void InitializeSettings()
        {
            PrefetchImages(DEFAULT_IMAGES);

            // Port is empty or not numeric
            if (String.IsNullOrEmpty(Settings.HideReplaySeconds) || !int.TryParse(Settings.HideReplaySeconds, out hideReplaySettings))
            {
                Settings.HideReplaySeconds = HIDE_REPLAY_SECONDS.ToString();
                SaveSettings();
            }

            if (String.IsNullOrEmpty(Settings.ReplayCooldown) || !int.TryParse(Settings.ReplayCooldown, out replayCooldown))
            {
                Settings.ReplayCooldown = DEFAULT_REPLAY_COOLDOWN.ToString();
            }

            if (String.IsNullOrEmpty(Settings.DelayReplaySeconds) || !int.TryParse(Settings.DelayReplaySeconds, out delayReplaySettings))
            {
                Settings.DelayReplaySeconds = DELAY_REPLAY_SECONDS.ToString();
                SaveSettings();
            }

            if (!Int32.TryParse(Settings.PlaySpeed, out speed))
            {
                Settings.PlaySpeed = DEFAULT_PLAY_SPEED_PERCENTAGE.ToString();
                SaveSettings();
            }

            if (Settings.TwitchIntegration)
            {
                ResetChat();
            }
        }

        private void ResetChat()
        {
            List<string> allowedPagers = null;

            Logger.Instance.LogMessage(TracingLevel.INFO, $"Initializing Twitch Chat for instant replay");
            if (!String.IsNullOrWhiteSpace(Settings.AllowedUsers))
            {
                allowedPagers = Settings.AllowedUsers?.Replace("\r\n", "\n").Split('\n').ToList();
            }
            ChatPager.Twitch.TwitchChat.Instance.Initialize(replayCooldown, Settings.ChatReplay, allowedPagers);
        }

        private async void Instance_PageRaised(object sender, ChatPager.Twitch.PageRaisedEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Replay requested by chat");
            await HandleInstantReplayRequest();
        }

        private void Instance_TokenStatusChanged(object sender, EventArgs e)
        {
            Settings.TwitchTokenExists = ChatPager.Twitch.TwitchTokenManager.Instance.TokenExists;
        }

        private async Task HandleInstantReplayRequest()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"HandleInstantReplayRequest called");
            if (Settings.TwitchClip)
            {
                ChatPager.Twitch.TwitchChat.Instance.CreateClip();
            }

            if (OBSManager.Instance.IsReplayBufferEnabled()) // Actively running Instant Replay
            {
                if (await OBSManager.Instance.SaveInstantReplay())
                {
                    await Connection.ShowOk();
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Instant Replay SaveInstantReplay Failed");
                    await Connection.ShowAlert();
                }
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Instant Replay not enabled. Status: {OBSManager.Instance.InstantReplyStatus}");
                await Connection.ShowAlert();
            }
        }

        private void Connection_OnSendToPlugin(object sender, SdTools.Wrappers.SDEventReceivedEventArgs<SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "loadfolderpicker":
                        string folderPropertyName = (string)payload["property_name"];
                        string folderTitle = (string)payload["picker_title"];
                        string folderName = PickersUtil.Pickers.FolderPicker(folderTitle, null);
                        if (!string.IsNullOrEmpty(folderName))
                        {
                            if (!PickersUtil.Pickers.SetJsonPropertyValue(settings, folderPropertyName, folderName))
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, "Failed to save picker value to settings");
                            }
                            SaveSettings();
                            SetGlobalSettings();
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
            Settings.Inputs = OBSManager.Instance.GetAllInputs()?.Where(i => i.InputKind == MEDIA_PLAYER_TYPE)?.OrderBy(i => i.InputName)?.ToList();
        }


        #endregion

    }
}