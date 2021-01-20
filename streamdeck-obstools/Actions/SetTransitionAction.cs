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
    [PluginActionId("com.barraider.obstools.settransition")]
    public class SetTransitionAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    TransitionName = String.Empty,
                    Transitions = null
                };
                return instance;
            }

            [JsonProperty(PropertyName = "transitionName")]
            public String TransitionName { get; set; }

            [JsonProperty(PropertyName = "transitions")]
            public List<TransitionInfo> Transitions { get; set; }
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
        private const string SELECTED_IMAGE_FILE = @"images/transitionSelected.png";

        private Image prefetchedSelectedImage = null;
        private bool selectedImageShown = false;

        #endregion
        public SetTransitionAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor Called {this.GetType()}");
            base.Dispose();
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Key Pressed");
            if (OBSManager.Instance.IsConnected)
            {
                if (String.IsNullOrEmpty(Settings.TransitionName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Key Pressed but TransitionName is empty");
                    await Connection.ShowAlert();
                    return;
                }

                OBSManager.Instance.SetTransition(Settings.TransitionName);
            }
            else
            {
                await Connection.ShowAlert();
            }
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (!baseHandledOnTick && !String.IsNullOrEmpty(Settings.TransitionName))
            {
                try
                {
                    await Connection.SetTitleAsync($"{Settings.TransitionName}");
                    if (OBSManager.Instance.GetTransition()?.Name == Settings.TransitionName)
                    {
                        selectedImageShown = true;
                        await Connection.SetImageAsync(GetSelectedImage());
                    }
                    else if (selectedImageShown)
                    {
                        selectedImageShown = false;
                        await Connection.SetImageAsync((String)null);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} OnTick exception: {ex}");
                }
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);
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
            if (OBSManager.Instance.IsConnected)
            {
                Settings.Transitions = OBSManager.Instance.GetAllTransitions().Select(t => new TransitionInfo() { Name = t }).ToList();
                SaveSettings();
            }
        }

        private Image GetSelectedImage()
        {
            if (prefetchedSelectedImage == null)
            {
                if (File.Exists(SELECTED_IMAGE_FILE))
                {
                    prefetchedSelectedImage = Image.FromFile(SELECTED_IMAGE_FILE);
                }
            }

            return prefetchedSelectedImage;
        }

        #endregion
    }
}