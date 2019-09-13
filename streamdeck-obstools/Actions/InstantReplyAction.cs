﻿using BarRaider.ObsTools.Wrappers;
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
    // 100 Bits: siliconart
    //---------------------------------------------------

    [PluginActionId("com.barraider.obstools.instantreply")]
    public class InstantReplyAction : ActionBase
    {
        private const int HIDE_REPLAY_SECONDS = 20;
        private const int DEFAULT_REPLAY_COOLDOWN = 30;
        private const int DELAY_REPLAY_SECONDS = 1;
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
                    SourceName = String.Empty,
                    HideReplaySeconds = HIDE_REPLAY_SECONDS.ToString(),
                    DelayReplaySeconds = DELAY_REPLAY_SECONDS.ToString(),
                    TwitchIntegration = false,
                    ReplayCooldown = "30",
                    AllowedUsers = String.Empty
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

            [JsonProperty(PropertyName = "sourceName")]
            public String SourceName { get; set; }

            [JsonProperty(PropertyName = "muteSound")]
            public bool MuteSound { get; set; }

            [JsonProperty(PropertyName = "twitchIntegration")]
            public bool TwitchIntegration { get; set; }

            [JsonProperty(PropertyName = "replayCooldown")]
            public string ReplayCooldown { get; set; }

            [JsonProperty(PropertyName = "allowedUsers")]
            public string AllowedUsers { get; set; }

            [JsonProperty(PropertyName = "twitchTokenExists")]
            public bool TwitchTokenExists { get; set; }
            
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

        private const int LONG_KEYPRESS_LENGTH = 600;

        private bool keyPressed = false;
        private bool longKeyPressed = false;
        private DateTime keyPressStart;
        private GlobalSettings global;
        private int hideReplaySettings = HIDE_REPLAY_SECONDS;
        private int replayCooldown = DEFAULT_REPLAY_COOLDOWN;
        private int delayReplaySettings = DELAY_REPLAY_SECONDS;

        #endregion
        public InstantReplyAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
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
            InitializeSettings();
        }

        public override void Dispose()
        {
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
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Instant Replay Short KeyPress");
                if (OBSManager.Instance.InstantReplyStatus == OutputState.Started) // Actively running Instant Replay
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
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Instant Replay Cannot Save Status: {OBSManager.Instance.InstantReplyStatus.ToString()}");
                    await Connection.ShowAlert();
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
                await Connection.SetTitleAsync($"Replay:\n{(OBSManager.Instance.IsReplayBuffer || OBSManager.Instance.InstantReplyStatus == OutputState.Started ? "On" : "Off")}");
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
            // Global Settings exist
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                global = payload.Settings.ToObject<GlobalSettings>();
                Settings.AutoReplay = global.AutoReplay;
                Settings.ReplayDirectory = global.ReplayDirectory;
                Settings.HideReplaySeconds = global.HideReplaySeconds.ToString();
                Settings.SourceName = global.SourceName;
                Settings.MuteSound = global.MuteSound;
                SaveSettings();
            }
            else // Global settings do not exist
            {
                global = new GlobalSettings();
                SetGlobalSettings();
            }

        }

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        #endregion

        #region Private Methods

        private void LongKeyPress()
        {
            longKeyPressed = true;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Instant Replay LongKeyPressed");
            try
            {
                if (OBSManager.Instance.IsStreaming && OBSManager.Instance.InstantReplyStatus == OutputState.Stopped) 
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
                else if (OBSManager.Instance.IsStreaming && OBSManager.Instance.InstantReplyStatus == OutputState.Started) 
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
                else // Not streaming or maybe the buffer is not in a stable state
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Instant Replay Cannot change mode: IsStreaming {OBSManager.Instance.IsStreaming} Status: {OBSManager.Instance.InstantReplyStatus.ToString()}");
                    Connection.ShowAlert();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Instant Replay LongKeyPress Exception: {ex}");
                Connection.ShowAlert();
            }
        }

        private void SetGlobalSettings()
        {
            global.AutoReplay = Settings.AutoReplay;
            global.ReplayDirectory = Settings.ReplayDirectory;
            global.HideReplaySeconds = hideReplaySettings;
            global.MuteSound = Settings.MuteSound;
            global.SourceName = Settings.SourceName;
            global.DelayReplaySeconds = delayReplaySettings;
            Connection.SetGlobalSettingsAsync(JObject.FromObject(global));
        }

        private void InitializeSettings()
        {
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

            if (Settings.TwitchIntegration)
            {
                ResetChat();
            }
        }

        private void ResetChat()
        {
            List<string> allowedPagers = null;

            if (!String.IsNullOrWhiteSpace(Settings.AllowedUsers))
            {
                allowedPagers = Settings.AllowedUsers?.Replace("\r\n", "\n").Split('\n').ToList();
            }
            ChatPager.Twitch.TwitchChat.Instance.Initialize(replayCooldown, allowedPagers);
        }

        private async void Instance_PageRaised(object sender, ChatPager.Twitch.PageRaisedEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Replay requested by chat");
            if (OBSManager.Instance.InstantReplyStatus == OutputState.Started) // Actively running Instant Replay
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
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Instant Replay Cannot Save Status: {OBSManager.Instance.InstantReplyStatus.ToString()}");
            }
        }

        private void Instance_TokenStatusChanged(object sender, EventArgs e)
        {
            Settings.TwitchTokenExists = ChatPager.Twitch.TwitchTokenManager.Instance.TokenExists;
        }


        #endregion

    }
}