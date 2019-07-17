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
    [PluginActionId("com.barraider.obstools.instantreply")]
    public class InstantReplyAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings();
                instance.ServerInfoExists = false;
                return instance;
            }
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
        }

        public override void Dispose()
        {
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
                if (timeKeyWasPressed >= LONG_KEYPRESS_LENGTH) // User is issuing a long keypress
                {
                    LongKeyPress();
                }
            }

            if (!baseHandledOnTick)
            {
                await Connection.SetTitleAsync($"Replay:\n{(OBSManager.Instance.InstantReplyStatus == OutputState.Started ? "On" : "Off")}");
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

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


        #endregion



    }
}