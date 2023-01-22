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
    [PluginActionId("com.barraider.obstools.virtualcamera")]
    public class VirtualCameraToggleAction : KeypadActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
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

        private const int CHECK_STATUS_COOLDOWN_MS = 3000;

        private DateTime lastStatusCheck = DateTime.MinValue;

        private readonly string[] DEFAULT_IMAGES = new string[]
        {
            @"images\vcamEnabled.png",
            @"images\vcamAction@2x.png"
        };

        #endregion
        public VirtualCameraToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            PrefetchImages(DEFAULT_IMAGES);
        }

        #region Public Methods

        public override void Dispose()
        {
            base.Dispose();
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            if (!OBSManager.Instance.IsConnected)
            {
                await Connection.ShowAlert();
                return;
            }

            if (payload.IsInMultiAction)
            {
                HandleMultiActionKeypress(payload.UserDesiredState);
                return;
            }

            OBSManager.Instance.ToggleVirtualCam();
            lastStatusCheck = DateTime.MinValue;
        }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (!baseHandledOnTick)
            {
                if ((DateTime.Now - lastStatusCheck).TotalMilliseconds >= CHECK_STATUS_COOLDOWN_MS)
                {
                    lastStatusCheck = DateTime.Now;
                    var isEnabled = OBSManager.Instance.IsVirtualCamEnabled();
                    if (isEnabled.HasValue)
                    {
                        await Connection.SetImageAsync(isEnabled.Value ? enabledImage : disabledImage);
                    }
                }
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            PrefetchImages(DEFAULT_IMAGES);
            SaveSettings();
            lastStatusCheck = DateTime.MinValue;
        }

        public override void KeyReleased(KeyPayload payload) { }
        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        #endregion

        #region Private Methods

        private void HandleMultiActionKeypress(uint state)
        {
            bool? isVisible;
            switch (state) // 0 = Toggle, 1 = Show, 2 = Hide
            {
                case 0:
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} HandleMultiActionKeypress - State: {state}. Toggling webcam");
                    OBSManager.Instance.ToggleVirtualCam();
                    break;
                case 1: // Show
                    isVisible = OBSManager.Instance.IsVirtualCamEnabled();
                    if (isVisible.HasValue)
                    {
                        if (isVisible.Value)
                        {
                            return;
                        }
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} HandleMultiActionKeypress - State: {state}. Toggling webcam");
                        OBSManager.Instance.ToggleVirtualCam();
                    }
                    break;
                case 2: // Hide
                    isVisible = OBSManager.Instance.IsVirtualCamEnabled();
                    if (isVisible.HasValue)
                    {
                        if (!isVisible.Value)
                        {
                            return;
                        }
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} HandleMultiActionKeypress - State: {state}. Toggling webcam");
                        OBSManager.Instance.ToggleVirtualCam();
                    }
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleMultiActionKeypress Invalid state: {state}");
                    return;
            }
        }

        #endregion
    }
}