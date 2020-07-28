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
    // Subscriber: Pokidj (Gifted by Flewp)
    // Flewp - Tip: $10
    // Subscriber: justgiggz
    // 1 Bits: nubby_ninja
    // 30.69 Bits: drewwatchyou
    // Subscriber: Nachtmeister666
    // 29 Bits: LlamaCadu
    //---------------------------------------------------
    //          Honorary Mention
    //  Marbles On Stream Winner: TheRickBlack
    //---------------------------------------------------
    [PluginActionId("com.barraider.obstools.studiomodetoggle")]
    public class StudioModeToggleAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false
                };
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

        public StudioModeToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

        public override void KeyPressed(KeyPayload payload)
        {
            baseHandledKeypress = false;
            base.KeyPressed(payload);

            if (!baseHandledKeypress)
            {
                if (payload.IsInMultiAction)
                {
                    HandleMultiAction(payload.UserDesiredState);
                    return;
                }
                else // Not a multi action, perform a normal toggle.
                {
                    if (OBSManager.Instance.IsStudioModeEnabled())
                    {
                        OBSManager.Instance.StopStudioMode();
                    }
                    else
                    {
                        OBSManager.Instance.StartStudioMode();
                    }
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
                await Connection.SetTitleAsync($"{(OBSManager.Instance.IsStudioModeEnabled() ? "🟢" : "🔲")}");
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        private void HandleMultiAction(uint desiredState)
        {
            // MultiAction: (0==Toggle, 1==On, 2==Off) 
            if ((desiredState == 1 || desiredState == 0) && !OBSManager.Instance.IsStudioModeEnabled()) // Desired state is on, and we're NOT in studio mode so Enable
            {
                OBSManager.Instance.StartStudioMode();
            }
            else if ((desiredState == 2 || desiredState == 0) && OBSManager.Instance.IsStudioModeEnabled()) // Desired state is off, and we ARE in studio mode so Disable
            {
                OBSManager.Instance.StopStudioMode();
            }
        }
        #endregion
    }
}