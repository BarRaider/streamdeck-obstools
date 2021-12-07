using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
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
    [PluginActionId("com.barraider.obstools.filtertoggle")]
    public class FilterToggleAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Sources = null,
                    Filters = null,
                    ServerInfoExists = false,
                    SourceName = String.Empty,
                    FilterName = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "sources")]
            public List<SceneSourceInfo> Sources { get; set; }

            [JsonProperty(PropertyName = "filters")]
            public List<FilterSettings> Filters { get; set; }

            [JsonProperty(PropertyName = "sourceName")]
            public String SourceName { get; set; }

            [JsonProperty(PropertyName = "filterName")]
            public String FilterName { get; set; }
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

        private const int CHECK_STATUS_COOLDOWN_MS = 3000;

        private bool enableFilter = true;
        private DateTime lastStatusCheck = DateTime.MinValue;

        private readonly string[] DEFAULT_IMAGES = new string[]
        {
            @"images\filterEnabled.png",
            @"images\filterAction@2x.png"
        };
        private Image filterEnabledImage = null;
        private Image filterDisabledImage = null;

        #endregion
        public FilterToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
            PrefetchImages();

        }

        #region Public Methods

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            base.Dispose();
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            if (OBSManager.Instance.IsConnected)
            {
                // Set enableFilter to a specific state if in a Mutli-Action
                if (payload.IsInMultiAction && payload.UserDesiredState == 0) // 0 = Enable, 1 = Disable
                {
                    enableFilter = true;
                }
                else if (payload.IsInMultiAction && payload.UserDesiredState == 1)
                {
                    enableFilter = false;
                }

                OBSManager.Instance.ToggleFilterVisibility(Settings.SourceName, Settings.FilterName, enableFilter);
                enableFilter = !enableFilter;
                lastStatusCheck = DateTime.MinValue;
            }
            else
            {
                await Connection.ShowAlert();
            }
        }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (!baseHandledOnTick)
            {
                if (String.IsNullOrEmpty(Settings.SourceName) || String.IsNullOrEmpty(Settings.FilterName))
                {
                    return;
                }

                if ((DateTime.Now - lastStatusCheck).TotalMilliseconds >= CHECK_STATUS_COOLDOWN_MS)
                {
                    lastStatusCheck = DateTime.Now;
                    var isEnabled = OBSManager.Instance.IsFilterEnabled(Settings.SourceName, Settings.FilterName, out bool exceptionRaised);
                    if (isEnabled.HasValue)
                    {
                        enableFilter = !isEnabled.Value;
                        await Connection.SetImageAsync(isEnabled.Value ? filterEnabledImage : filterDisabledImage);
                    }
                    else if (exceptionRaised)
                    {

                        Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Exception raised for IsFilterEnabled, resetting settings");
                        Settings.FilterName = null;
                        Settings.SourceName = null;
                    }
                }
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            string sourceName = Settings.SourceName;
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            if (Settings.SourceName != sourceName)
            {
                LoadSourceFilters();
            }
            SaveSettings();
            lastStatusCheck = DateTime.MinValue;
        }

        public override void KeyReleased(KeyPayload payload) { }
        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) {  }

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        #endregion

        #region Private Methods

        private void PrefetchImages()
        {
            if (filterEnabledImage != null)
            {
                filterEnabledImage.Dispose();
                filterEnabledImage = null;
            }

            if (filterDisabledImage != null)
            {
                filterDisabledImage.Dispose();
                filterDisabledImage = null;
            }

            filterEnabledImage = Image.FromFile(DEFAULT_IMAGES[0]);
            filterDisabledImage = Image.FromFile(DEFAULT_IMAGES[1]);
        }

        private void Connection_OnPropertyInspectorDidAppear(object sender, SdTools.Wrappers.SDEventReceivedEventArgs<SdTools.Events.PropertyInspectorDidAppear> e)
        {
            LoadSourcesList();
            LoadSourceFilters();
            SaveSettings();
        }

        private void LoadSourcesList()
        {
            Settings.Sources = OBSManager.Instance.GetAllSceneAndSourceNames();
        }

        private void LoadSourceFilters()
        {
            Settings.Filters = null;
            if (String.IsNullOrEmpty(Settings.SourceName))
            {
                return;
            }
            Settings.Filters = OBSManager.Instance.GetSourceFilters(Settings.SourceName)?.OrderBy(s => s.Name)?.ToList();
        }

        #endregion
    }
}