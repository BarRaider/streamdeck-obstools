using BarRaider.ObsTools.Backend;
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
                PluginSettings instance = new PluginSettings
                {
                    ServerInfoExists = false,
                    DroppedFramesType = DroppedFramesType.DroppedFrames,
                    AlertColor = "#FF0000",
                    MinFrames = MIN_FRAMES_DEFAULT.ToString()
                };
                return instance;
            }

            [JsonProperty(PropertyName = "droppedFramesType")]
            public DroppedFramesType DroppedFramesType { get; set; }

            [JsonProperty(PropertyName = "alertColor")]
            public String AlertColor { get; set; }

            [JsonProperty(PropertyName = "minFrames")]
            public String MinFrames { get; set; }
            
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

        private const int TOTAL_ALERT_STAGES = 4;
        private const int MIN_FRAMES_DEFAULT = 0;

        private StreamStatusEventArgs streamStatus;
        private int lastCountOfDroppedFrames = 0;
        private readonly Timer tmrAlert = new Timer();
        private bool isAlerting = false;
        private int alertStage = 0;
        private bool firstDataLoad = true;
        private int minFrames = MIN_FRAMES_DEFAULT;

        #endregion
        public DroppedFramesAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            OBSManager.Instance.StreamStatusChanged += Instance_StreamStatusChanged;

            tmrAlert.Interval = 200;
            tmrAlert.Elapsed += TmrAlert_Elapsed;
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
            InitializeSettings();
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

            if (isAlerting)
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
            InitializeSettings();
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

        private Color GenerateStageColor(string initialColor, int stage, int totalAmountOfStages)
        {
            Color color = ColorTranslator.FromHtml(initialColor);
            int a = color.A;
            double r = color.R;
            double g = color.G;
            double b = color.B;

            // Try and increase the color in the last stage;
            if (stage == totalAmountOfStages - 1)
            {
                stage = 1;
            }

            for (int idx = 0; idx < stage; idx++)
            {
                r /= 2;
                g /= 2;
                b /= 2;
            }

            return Color.FromArgb(a, (int)r, (int)g, (int)b);
        }

        private async void TmrAlert_Elapsed(object sender, ElapsedEventArgs e)
        {
            String message = lastCountOfDroppedFrames.ToString();
            using (Bitmap img = Tools.GenerateGenericKeyImage(out Graphics graphics))
            {
                int height = img.Height;
                int width = img.Width;

                // Background
                var bgBrush = new SolidBrush(GenerateStageColor(Settings.AlertColor, alertStage, TOTAL_ALERT_STAGES));
                graphics.FillRectangle(bgBrush, 0, 0, width, height);

                var font = new Font("Verdana", 34, FontStyle.Bold, GraphicsUnit.Pixel);
                var fgBrush = Brushes.White;
                SizeF stringSize = graphics.MeasureString(message, font);
                float stringPos = 0;
                float stringHeight = Math.Abs((height - stringSize.Height)) / 2;
                if (stringSize.Width < width)
                {
                    stringPos = Math.Abs((width - stringSize.Width)) / 2;
                }
                graphics.DrawString(message, font, fgBrush, new PointF(stringPos, stringHeight));
                await Connection.SetImageAsync(img);
                graphics.Dispose();
            }
            alertStage = (alertStage + 1) % TOTAL_ALERT_STAGES;
        }

        private void InitializeSettings()
        {
            if (!Int32.TryParse(Settings.MinFrames, out minFrames))
            {
                minFrames = MIN_FRAMES_DEFAULT;
                Settings.MinFrames = MIN_FRAMES_DEFAULT.ToString();
                SaveSettings();
            }
        }

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        #endregion
    }
}