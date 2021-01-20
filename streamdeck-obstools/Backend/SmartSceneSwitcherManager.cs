using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Backend
{

    public class SmartSceneSwitcherManager
    {
        #region Private Members

        private static SmartSceneSwitcherManager instance = null;
        private static readonly object objLock = new object();

        private GlobalSettings global;

        #endregion

        #region Constructors

        public static SmartSceneSwitcherManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new SmartSceneSwitcherManager();
                    }
                    return instance;
                }
            }
        }

        private SmartSceneSwitcherManager()
        {
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += Instance_OnReceivedGlobalSettings;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
        }

        private void Instance_OnReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload payload)
        {
            // Global Settings exist
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                global = payload.Settings.ToObject<GlobalSettings>();
            }
            else // Global settings do not exist
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"SmartSceneSwitcher received empty payload: {payload}, creating new instance");
                global = new GlobalSettings();
                GlobalSettingsManager.Instance.SetGlobalSettings(JObject.FromObject(global));
            }
        }

        #endregion

        #region Public Methods

        public string SceneSwitchLiveColor => global?.SceneSwitchLiveColor;
        public string SceneSwitchPreviewColor => global?.SceneSwitchPreviewColor;

        public void SetColors(string liveColor, string previewColor)
        {
            if (global == null)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"SmartSceneSwitcherManager SetColors: Ignoring SetColors as global is null");
                return;
            }
            global.SceneSwitchLiveColor = liveColor;
            global.SceneSwitchPreviewColor = previewColor;
            GlobalSettingsManager.Instance.SetGlobalSettings(JObject.FromObject(global));
        }

        public void Initialize()
        {

        }

        #endregion
    }


}
