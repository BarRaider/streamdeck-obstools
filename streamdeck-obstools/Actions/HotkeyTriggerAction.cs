using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OTI.Shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace BarRaider.ObsTools.Actions
{
    [PluginActionId("com.barraider.obstools.hotkeytrigger")]
    public class HotkeyTriggerAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    Hotkey = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "hotkey")]
            public string Hotkey { get; set; }
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

        HotkeySequence sequence = null;

        #endregion

        public HotkeyTriggerAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

        public async override void KeyPressed(KeyPayload payload)
        {
            baseHandledKeypress = false;
            base.KeyPressed(payload);

            if (!baseHandledKeypress)
            {
                if (String.IsNullOrEmpty(Settings.Hotkey) || sequence == null || !sequence.IsValidSequence)
                {

                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} KeyPressed but Invalid Hotkey {Settings.Hotkey}");
                    await Connection.ShowAlert();
                    return;
                }
                else
                {
                    if (OBSManager.Instance.TriggerHotkey(sequence.Keycode.ToOBSKey(), sequence.CtrlPressed, sequence.AltPressed, sequence.ShiftPressed, sequence.WinPressed))
                    {
                        await Connection.ShowOk();
                    }
                    else
                    {
                        await Connection.ShowAlert();
                    }
                }
            }
        }


        public override void KeyReleased(KeyPayload payload) { }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            InitializeSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        private void InitializeSettings()
        {
            sequence = new HotkeySequence(Settings.Hotkey);
            if (!String.IsNullOrEmpty(Settings.Hotkey) && !sequence.IsValidSequence)
            {
                Settings.Hotkey = "INVALID - TRY AGAIN";
                SaveSettings();
            }
        }

        #endregion
    }
}