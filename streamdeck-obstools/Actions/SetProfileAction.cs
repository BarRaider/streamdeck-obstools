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
    [PluginActionId("com.barraider.obstools.setprofile")]
    public class SetProfileAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    ProfileName = String.Empty,
                    Profiles = null
                };
                return instance;
            }

            [JsonProperty(PropertyName = "profileName")]
            public String ProfileName { get; set; }

            [JsonProperty(PropertyName = "profiles")]
            public List<ProfileInfo> Profiles { get; set; }
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

        public SetProfileAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed");
            if (OBSManager.Instance.IsConnected)
            {
                if (String.IsNullOrEmpty(Settings.ProfileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Key Pressed but ProfileName is empty");
                    await Connection.ShowAlert();
                    return;
                }

                OBSManager.Instance.SetProfile(Settings.ProfileName);
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

            if (!baseHandledOnTick && !String.IsNullOrEmpty(Settings.ProfileName))
            {
                await Connection.SetTitleAsync($"{Settings.ProfileName}");
                if (OBSManager.Instance.GetProfile() == Settings.ProfileName)
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
                Settings.Profiles = OBSManager.Instance.GetAllProfiles().Select(p => new ProfileInfo() { Name = p }).ToList();
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