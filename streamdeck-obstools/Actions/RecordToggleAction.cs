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
    // Subscriber: CarstenPet
    // CarstenPet - 5 Gifted Subs
    //---------------------------------------------------
    public enum RecordingAction
    {
        START_STOP = 0,
        PAUSE_RESUME = 1
    }

    [PluginActionId("com.barraider.obstools.recordingtoggle")]
    public class RecordToggleAction : ActionBase
    {
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    ShortPressAction = RecordingAction.START_STOP,
                    LongPressAction = RecordingAction.START_STOP, // Websocket 4.9 -> Change to Pause/Resume
                    LongKeypressTime = LONG_KEYPRESS_LENGTH_MS.ToString() 
                };
                return instance;
            }

            [JsonProperty(PropertyName = "shortPressAction")]
            public RecordingAction ShortPressAction { get; set; }

            [JsonProperty(PropertyName = "longPressAction")]
            public RecordingAction LongPressAction { get; set; }

            [JsonProperty(PropertyName = "longKeypressTime")]
            public string LongKeypressTime { get; set; }
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

        private const int LONG_KEYPRESS_LENGTH_MS = 600;
        private int longKeypressTime = LONG_KEYPRESS_LENGTH_MS;
        private readonly System.Timers.Timer tmrRunLongPress = new System.Timers.Timer();

        private bool longKeyPressed = false;

        #endregion

        public RecordToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            tmrRunLongPress.Elapsed += TmrRunLongPress_Elapsed;
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
            InitializeSettings();
        }

        public override void Dispose()
        {
            tmrRunLongPress.Stop();
            tmrRunLongPress.Elapsed -= TmrRunLongPress_Elapsed;
            base.Dispose();
        }

        public override void KeyPressed(KeyPayload payload)
        {
            baseHandledKeypress = false;
            base.KeyPressed(payload);

            if (!baseHandledKeypress)
            {
                // Used for long press
                longKeyPressed = false;

                tmrRunLongPress.Interval = longKeypressTime > 0 ? longKeypressTime : LONG_KEYPRESS_LENGTH_MS;
                tmrRunLongPress.Start();
            }
        }

        public override void KeyReleased(KeyPayload payload) 
        {
            tmrRunLongPress.Stop();

            if (!longKeyPressed)
            {
                if (payload.IsInMultiAction)
                {
                    HandleMultiActionKeyPress(payload.UserDesiredState);
                }
                else
                {
                    HandleAction(Settings.ShortPressAction);
                }
            }
        }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (!baseHandledOnTick)
            {
                await Connection.SetTitleAsync($"{(OBSManager.Instance.IsRecording ? "🔴" : "🔲")}");
                /*
                var recordingInfo = OBSManager.Instance.GetRecordingStatus();
                if (recordingInfo != null)
                {
                    string icon = "🔲";
                    if (recordingInfo.IsRecordingPaused)
                    {
                        icon = "| |";
                    }
                    else if (recordingInfo.IsRecording)
                    {
                        icon = "🔴";
                    }                   
                    await Connection.SetTitleAsync(icon);
                }
                else
                {
                    await Connection.SetTitleAsync($"{(OBSManager.Instance.IsRecording ? "🔴" : "🔲")}");
                }*/
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

        private void TmrRunLongPress_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            tmrRunLongPress.Stop();
            LongKeyPressed();
        }

        private void LongKeyPressed()
        {
            longKeyPressed = true;
            HandleAction(Settings.LongPressAction);
        }

        private void InitializeSettings()
        {
            if (!Int32.TryParse(Settings.LongKeypressTime, out longKeypressTime))
            {
                Settings.LongKeypressTime = LONG_KEYPRESS_LENGTH_MS.ToString();
                SaveSettings();
            }
        }

        private void HandleAction(RecordingAction action)
        {
            if (OBSManager.Instance.IsRecording)
            {
                OBSManager.Instance.StopRecording();
            }
            else
            {
                OBSManager.Instance.StartRecording();
            }

            /*
            var recordingInfo = OBSManager.Instance.GetRecordingStatus();
            if (recordingInfo == null)
            {
                // TODO: Remove in Websocket 4.9
                recordingInfo = new OBSWebsocketDotNet.Types.RecordingStatus
                {
                    IsRecording = OBSManager.Instance.IsRecording,
                    IsRecordingPaused = false,
                    RecordTimeCode = null
                };
                action = RecordingAction.START_STOP;

                // TODO: Uncomment in Websocket 4.9
                //Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleAction: GetRecordingStatus returned null");
                //return;
            }
            switch (action)
            {
                case RecordingAction.START_STOP:
                    if (recordingInfo.IsRecording)
                    {
                        OBSManager.Instance.StopRecording();
                    }
                    else
                    {
                        OBSManager.Instance.StartRecording();
                    }
                    break;
                case RecordingAction.PAUSE_RESUME:
                    if (!recordingInfo.IsRecording)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleAction: Pause/Resume called requested but is not recording");
                        await Connection.ShowAlert();
                        return;
                    }

                    if (recordingInfo.IsRecordingPaused)
                    {
                        OBSManager.Instance.ResumeRecording();
                    }
                    else
                    {
                        OBSManager.Instance.PauseRecording();
                    }
                    break;
            }*/
        }

        private void HandleMultiActionKeyPress(uint state)
        {
            switch (state)
            {
                case 0:
                    if (!OBSManager.Instance.IsRecording)
                    {
                        OBSManager.Instance.StartRecording();
                    }
                    break;
                case 1:
                    if (OBSManager.Instance.IsRecording)
                    {
                        OBSManager.Instance.StopRecording();
                    }
                    break;
            }

            /*
            var recordingInfo = OBSManager.Instance.GetRecordingStatus();
            if (recordingInfo == null)
            {
                // TODO: Remove in Websocket 4.9
                recordingInfo = new OBSWebsocketDotNet.Types.RecordingStatus
                {
                    IsRecording = OBSManager.Instance.IsRecording,
                    IsRecordingPaused = false,
                    RecordTimeCode = null
                };
                
                // TODO: Uncomment in Websocket 4.9
                //Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleMultiActionKeyPress: GetRecordingStatus returned null");
                //return;
            }
            switch (state) // 0 = Start, 1 = Stop, 2 = Pause, 3 = Resume
            {
                case 0:
                    if (!recordingInfo.IsRecording)
                    {
                        OBSManager.Instance.StartRecording();
                    }
                    break;
                case 1:
                    if (recordingInfo.IsRecording)
                    {
                        OBSManager.Instance.StopRecording();
                    }
                    break;
                case 2:
                    if (recordingInfo.IsRecording && !recordingInfo.IsRecordingPaused)
                    {
                        OBSManager.Instance.PauseRecording();
                    }
                    break;
                case 3:
                    if (recordingInfo.IsRecording && recordingInfo.IsRecordingPaused)
                    {
                        OBSManager.Instance.ResumeRecording();
                    }
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleMultiActionKeyPress: Invalid state {state}");
                    break;
            }*/
        }

        #endregion
    }
}