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
    [PluginActionId("com.barraider.obstools.setscenecollection")]
    public class SetSceneCollectionAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    SceneCollectionName = String.Empty,
                    SceneCollections = null
                };
                return instance;
            }

            [JsonProperty(PropertyName = "sceneCollectionName")]
            public String SceneCollectionName { get; set; }

            [JsonProperty(PropertyName = "sceneCollections")]
            public List<SceneCollectionInfo> SceneCollections { get; set; }
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

        public SetSceneCollectionAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
                if (String.IsNullOrEmpty(Settings.SceneCollectionName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Key Pressed but SceneCollectionName is empty");
                    await Connection.ShowAlert();
                    return;
                }

                OBSManager.Instance.SetSceneCollection(Settings.SceneCollectionName);
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

            if (!baseHandledOnTick && !String.IsNullOrEmpty(Settings.SceneCollectionName))
            {
                await Connection.SetTitleAsync($"{Settings.SceneCollectionName}");
                if (OBSManager.Instance.GetSceneCollection() == Settings.SceneCollectionName)
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
                Settings.SceneCollections = OBSManager.Instance.GetAllSceneCollections().Select(s => new SceneCollectionInfo() { Name = s }).ToList();
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