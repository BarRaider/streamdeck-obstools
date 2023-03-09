using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet.Types;
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

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: SP__LIT
    //---------------------------------------------------
    [PluginActionId("com.barraider.obstools.volumedial")]
    public class InputVolumeDialAction : EncoderActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    Inputs = null,
                    InputName = String.Empty,
                    StepSize = DEFAULT_STEP_SIZE.ToString()
                };
                return instance;
            }

            [JsonProperty(PropertyName = "inputs")]
            public List<InputBasicInfo> Inputs { get; set; }

            [JsonProperty(PropertyName = "inputName")]
            public String InputName { get; set; }

            [JsonProperty(PropertyName = "stepSize")]
            public String StepSize { get; set; }
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

        private const float MINIMAL_DB_VALUE = -95.8f;
        private const int DEFAULT_STEP_SIZE = 1;
        private const int DIAL_PRESS_INCREMENT = 10;

        private readonly string[] DEFAULT_IMAGES = new string[]
       {
            @"images\muteEnabled.png",
            @"images\volumeAction@2x.png"
       };
        private string mutedImageStr;
        private string unmutedImageStr;
        private bool dialWasRotated = false;
        private int stepSize = DEFAULT_STEP_SIZE;
        private VolumeInfoInternal prevVolumeInfo;


        #endregion
        public InputVolumeDialAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            PrefetchImages(DEFAULT_IMAGES);
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            base.Dispose();
        }

        public async override void DialRotate(DialRotatePayload payload)
        {
            baseHandledDialInteraction = false;
            base.DialRotate(payload);
            if (baseHandledDialInteraction)
            {
                return;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Dial Rotate");
            dialWasRotated = true;
            if (OBSManager.Instance.IsConnected)
            {
                if (String.IsNullOrEmpty(Settings.InputName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Dial Rotate but SourceName is empty");
                    await Connection.ShowAlert();
                    return;
                }

                // Get current volume
                var volumeInfo = OBSManager.Instance.GetInputVolume(Settings.InputName);
                if (volumeInfo != null)
                {
                    int increment = payload.Ticks * stepSize;
                    if (payload.IsDialPressed)
                    {
                        increment = DIAL_PRESS_INCREMENT * (payload.Ticks > 0 ? 1 : -1);
                    }
                    double outputVolume = Math.Round(volumeInfo.VolumeDb + increment);
                    if (outputVolume > 0)
                    {
                        outputVolume = 0;
                    }

                    if (outputVolume < MINIMAL_DB_VALUE)
                    {
                        outputVolume = MINIMAL_DB_VALUE;
                    }

                    OBSManager.Instance.SetInputVolume(Settings.InputName, (float)outputVolume, true);
                }
            }
            else
            {
                await Connection.ShowAlert();
            }
        }

        public async override void DialPress(DialPressPayload payload)
        {
            baseHandledDialInteraction = false;
            base.DialPress(payload);
            if (baseHandledDialInteraction)
            {
                return;
            }

            if (payload.IsDialPressed)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Dial Pressed");
                dialWasRotated = false;
                return;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Dial Released");
            if (dialWasRotated)
            {
                return;
            }

            if (!ToggleMute())
            {
                await Connection.ShowAlert();
                return;

            }
        }

        public async override void TouchPress(TouchpadPressPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Touch Pressed");
            if (!ToggleMute())
            {
                await Connection.ShowAlert();
                return;

            }
        }

        public override void OnTick()
        {
            try
            {
                baseHandledOnTick = false;
                base.OnTick();
                if (baseHandledOnTick)
                {
                    return;
                }

                if (String.IsNullOrEmpty(Settings.InputName))
                {
                    return;
                }

                VolumeInfo volumeInfo = OBSManager.Instance.GetInputVolume(Settings.InputName);
                if (volumeInfo == null)
                {
                    return;
                }

                bool isMuted = OBSManager.Instance.IsInputMuted(Settings.InputName);
                if (prevVolumeInfo == null || volumeInfo.VolumeDb != prevVolumeInfo.Volume ||
                    isMuted != prevVolumeInfo.IsMuted)
                {
                    prevVolumeInfo = new VolumeInfoInternal(volumeInfo.VolumeDb, isMuted);
                    Dictionary<string, string> dkv = new Dictionary<string, string>();
                    if (isMuted)
                    {
                        dkv["icon"] = mutedImageStr;
                        dkv["title"] = Settings.InputName;
                        dkv["value"] = "Muted";
                        dkv["indicator"] = "0";
                        Connection.SetFeedbackAsync(dkv);
                    }
                    else
                    {
                        var volume = Math.Round(volumeInfo.VolumeDb, 1);
                        dkv["icon"] = unmutedImageStr;
                        dkv["title"] = Settings.InputName;
                        dkv["value"] = $"{volume} db";
                        dkv["indicator"] = Tools.RangeToPercentage((int)volume, -95, 0).ToString();
                        Connection.SetFeedbackAsync(dkv);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} OnTick Exception: {ex}");
            }

        }

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
            prevVolumeInfo = null;
            _ = Connection.SetFeedbackAsync("title", Settings.InputName);
            if (!Int32.TryParse(Settings.StepSize, out stepSize))
            {
                stepSize = DEFAULT_STEP_SIZE;
                SaveSettings();
            }
        }

        private void Connection_OnPropertyInspectorDidAppear(object sender, SdTools.Wrappers.SDEventReceivedEventArgs<SdTools.Events.PropertyInspectorDidAppear> e)
        {
            LoadInputsList();
            SaveSettings();
        }

        private void LoadInputsList()
        {
            Settings.Inputs = null;
            if (!OBSManager.Instance.IsConnected)
            {
                return;
            }

            var inputs = OBSManager.Instance.GetAudioInputs();
            if (inputs != null)
            {
                Settings.Inputs = inputs.OrderBy(s => s?.InputName ?? "Z").ToList();
            }
        }

        protected void PrefetchImages(string[] defaultImages)
        {
            if (defaultImages.Length < 2)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} PrefetchImages: Invalid default images list");
                return;
            }

            mutedImageStr = Tools.ImageToBase64(Image.FromFile(IsValidFile(settings.EnabledImage) ? settings.EnabledImage : defaultImages[0]), true);
            unmutedImageStr = Tools.ImageToBase64(Image.FromFile(IsValidFile(settings.DisabledImage) ? settings.DisabledImage : defaultImages[1]), true);
        }

        private bool IsValidFile(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                return false;
            }

            if (!File.Exists(fileName))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} IsValidFile - File not found: {fileName}");
                return false;
            }
            return true;
        }

        private bool ToggleMute()
        {
            if (String.IsNullOrEmpty(Settings.InputName))
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Dial Pressed but Input Name is empty");
                return false;
            }

            return OBSManager.Instance.ToggleInputMute(Settings.InputName);
        }

        #endregion


        private class VolumeInfoInternal
        {
            public float Volume { get; private set; }
            public bool IsMuted { get; private set; }
            public VolumeInfoInternal(float volume, bool isMuted)
            {
                Volume = volume;
                IsMuted = isMuted;
            }
        }
    }
}