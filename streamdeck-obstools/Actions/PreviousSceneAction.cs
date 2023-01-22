using BarRaider.ObsTools.Backend;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json.Linq;
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
    [PluginActionId("com.barraider.obstools.previousscene")]
    public class PreviousSceneAction : KeypadActionBase
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

        #region Private Members

        private bool showedPrevScene = false;
        private string prevSceneName = string.Empty;
        private TitleParameters titleParameters;

        #endregion

        public PreviousSceneAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            CheckServerInfoExists();
        }

        public override void Dispose()
        {
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            base.Dispose();
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            baseHandledKeypress = false;
            base.KeyPressed(payload);

            if (!baseHandledKeypress)
            {
                bool sceneChanged = false;
                if (!string.IsNullOrEmpty(OBSManager.Instance.PreviousSceneName))
                {
                    sceneChanged = OBSManager.Instance.ChangeScene(OBSManager.Instance.PreviousSceneName);
                }

                if (sceneChanged)
                {
                    await Connection.ShowOk();
                }
                else
                {
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
                if (!String.IsNullOrEmpty(OBSManager.Instance.PreviousSceneName))
                {
                    if (prevSceneName != OBSManager.Instance.PreviousSceneName)
                    {
                        prevSceneName = OBSManager.Instance.PreviousSceneName;
                        await Connection.SetTitleAsync(OBSManager.Instance.PreviousSceneName.SplitToFitKey(titleParameters));
                    }
                    showedPrevScene = true;                    
                }
                else if (showedPrevScene)
                {
                    await Connection.SetTitleAsync(null);
                    showedPrevScene = false;
                }
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
        #endregion

        #region Private Methods

        private void Connection_OnTitleParametersDidChange(object sender, SDEventReceivedEventArgs<SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e?.Event?.Payload?.TitleParameters;
        }

        #endregion
    }
}