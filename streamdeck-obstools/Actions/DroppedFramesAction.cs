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
    [PluginActionId("com.barraider.obstools.droppedframes")]
    public class DroppedFramesAction : ActionBase
    {
        public enum DroppedFramesType
        {
            DroppedFrames = 0,
            OutputSkipped = 1,
            RenderMissed = 2

        }
        protected class PluginSettings : PluginSettingsBase
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings();
                instance.ServerInfoExists = false;
                instance.DroppedFramesType = DroppedFramesType.DroppedFrames;
                return instance;
            }

            [JsonProperty(PropertyName = "droppedFramesType")]
            public DroppedFramesType DroppedFramesType { get; set; }
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

        private static readonly string[] pageArr = { Properties.Settings.Default.AlertPage1, Properties.Settings.Default.AlertPage2, Properties.Settings.Default.AlertPage3, Properties.Settings.Default.AlertPage2 };
        private StreamStatusEventArgs streamStatus;
        private int lastCountOfDroppedFrames = 0;
        private Timer tmrAlert = new Timer();
        private bool isAlerting = false;
        private int alertStage = 0;
        private StreamDeckDeviceType deviceType;
        private bool firstDataLoad = true;

        #endregion
        public DroppedFramesAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            OBSManager.Instance.StreamStatusChanged += Instance_StreamStatusChanged;

            tmrAlert.Interval = 200;
            tmrAlert.Elapsed += TmrAlert_Elapsed;
            deviceType = Connection.DeviceInfo().Type;
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
        }

        public override void Dispose()
        {
            OBSManager.Instance.StreamStatusChanged -= Instance_StreamStatusChanged;
            tmrAlert.Stop();
            base.Dispose();
        }

        public override void KeyPressed(KeyPayload payload)
        {
            baseHandledKeypress = false;
            base.KeyPressed(payload);

            if (!baseHandledKeypress)
            {

            }

            // for debug only:
            if (!isAlerting)
            {
                lastCountOfDroppedFrames--;
            }
            else
            {
                isAlerting = false;
            }
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            if (!baseHandledOnTick)
            {
                if (isAlerting && !tmrAlert.Enabled)
                {
                    alertStage = 0;
                    tmrAlert.Start();
                    await Connection.SetTitleAsync(null); // Clear the text of the dropped frames
                }
                else if (!isAlerting)
                {
                    tmrAlert.Stop();
                    await Connection.SetImageAsync((String)null); // Clear the image of the alert if it existed
                    await Connection.SetTitleAsync(lastCountOfDroppedFrames.ToString());

                }
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            var previousFramesMode = Settings.DroppedFramesType;
            Tools.AutoPopulateSettings(Settings, payload.Settings);

            if (previousFramesMode != Settings.DroppedFramesType)
            {
                lastCountOfDroppedFrames = GetCurrentDroppedFrames();
            }
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private void Instance_StreamStatusChanged(object sender, StreamStatusEventArgs e)
        {
            streamStatus = e;

            if (streamStatus != null)
            {
                int currentDroppedFrames = GetCurrentDroppedFrames();
                if (currentDroppedFrames > lastCountOfDroppedFrames)
                {
                    lastCountOfDroppedFrames = currentDroppedFrames;
                    if (!firstDataLoad)
                    {
                        InitiateAlert();
                    }
                    firstDataLoad = false;
                }
            }
        }

        private int GetCurrentDroppedFrames()
        {
            int currentDroppedFrames = 0;
            if (streamStatus != null)
            {
                switch (Settings.DroppedFramesType)
                {
                    case DroppedFramesType.DroppedFrames:
                        currentDroppedFrames = streamStatus.Status.DroppedFrames;
                        break;
                    case DroppedFramesType.OutputSkipped:
                        currentDroppedFrames = streamStatus.Status.SkippedFrames;
                        break;
                    case DroppedFramesType.RenderMissed:
                        currentDroppedFrames = streamStatus.Status.RenderMissedFrames;
                        break;
                }
            }

            return currentDroppedFrames;
        }

        private void InitiateAlert()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Alerting on dropped frames: {lastCountOfDroppedFrames}");
            isAlerting = true;
        }

        private void TmrAlert_Elapsed(object sender, ElapsedEventArgs e)
        {
            String message = lastCountOfDroppedFrames.ToString();
            Image img = Tools.Base64StringToImage(pageArr[alertStage]);
            // Todo: Change size if using different size image
            Graphics graphics = Graphics.FromImage(img);
            int height = 144;
            int width = 144;


            var font = new Font("Verdana", 50, FontStyle.Bold);
            var fgBrush = Brushes.White;
            SizeF stringSize = graphics.MeasureString(message, font);
            float stringPos = 0;
            float stringHeight = 54;
            if (stringSize.Width < width)
            {
                stringPos = Math.Abs((width - stringSize.Width)) / 2;
                stringHeight = Math.Abs((height - stringSize.Height)) / 2;
            }
            graphics.DrawString(message, font, fgBrush, new PointF(stringPos, stringHeight));
            Connection.SetImageAsync(img);

            alertStage = (alertStage + 1) % pageArr.Length;

        }

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        #endregion
    }
}