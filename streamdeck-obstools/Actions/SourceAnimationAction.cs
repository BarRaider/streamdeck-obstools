using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OTI.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BarRaider.ObsTools.Actions
{

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: ericrisch
    // Subscriber: Vedeksu
    // Subscriber: Uslace
    // Subscriber: venomxapollo
    // Subscriber: ChessCoachNET
    // Subscriber: stea1e
    // Subscriber: transparentpixel
    //---------------------------------------------------
    [PluginActionId("com.barraider.obstools.sourceanimation")]
    public class SourceAnimationAction : ActionBase
    {
        protected class SourceAnimationActionSettings : PluginSettingsBase
        {
            public OTI.Shared.AnimationUserSettings ToAnimationUserSettings()
            {
                return new AnimationUserSettings()
                {
                    Version = this.Version,
                    SourceName = this.SourceName,
                    AnimationPhases = this.AnimationPhases,
                    SelectedPhase = this.SelectedPhase,
                    AnimationLength = this.AnimationLength,
                    Steps = this.Steps,
                    ResetDefaults = this.ResetDefaults,
                    PhaseName = this.PhaseName
                };
            }
            public static SourceAnimationActionSettings CreateDefaultSettings()
            {
                SourceAnimationActionSettings instance = new SourceAnimationActionSettings
                {
                    ServerInfoExists = false,
                    Version = CURRENT_VERSION,
                    SourceName = String.Empty,
                    AnimationPhases = null,
                    SelectedPhase = String.Empty,
                    AnimationLength = AnimationPhaseSettings.DEFAULT_ANIMATION_LENGTH_MS.ToString(),
                    Steps = AnimationPhaseSettings.DEFAULT_STEPS_NUM.ToString(),
                    ResetDefaults = true,
                    PhaseName = String.Empty,
                    HideSourceAtStart = false,
                    HideSourceAtEnd = false,
                    RemoveFilterAtEnd = false,
                    IsRecording = false,
                    RepeatAmount = DEFAULT_REPEAT_AMOUNT.ToString()
                };
                return instance;
            }

            [JsonProperty(PropertyName = "version")]
            public String Version { get; set; }

            [JsonProperty(PropertyName = "sourceName")]
            public String SourceName { get; set; }

            [JsonProperty(PropertyName = "animationPhases")]
            public List<AnimationPhaseSettings> AnimationPhases;

            [JsonProperty(PropertyName = "selectedPhase")]
            public String SelectedPhase { get; set; }

            [JsonProperty(PropertyName = "phaseName")]
            public String PhaseName { get; set; }

            [JsonProperty(PropertyName = "animationLength")]
            public String AnimationLength { get; set; }

            [JsonProperty(PropertyName = "steps")]
            public String Steps { get; set; }

            [JsonProperty(PropertyName = "resetDefaults")]
            public bool ResetDefaults { get; set; }

            [JsonProperty(PropertyName = "hideSourceAtStart")]
            public bool HideSourceAtStart { get; set; }

            [JsonProperty(PropertyName = "hideSourceAtEnd")]
            public bool HideSourceAtEnd { get; set; }

            [JsonProperty(PropertyName = "removeFilterAtEnd")]
            public bool RemoveFilterAtEnd { get; set; }

            [JsonProperty(PropertyName = "isRecording")]
            public bool IsRecording { get; set; }

            [JsonProperty(PropertyName = "repeatAmount")]
            public String RepeatAmount { get; set; }
        }

        protected SourceAnimationActionSettings Settings
        {
            get
            {
                var result = settings as SourceAnimationActionSettings;
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
        private const int DEFAULT_REPEAT_AMOUNT = 0;
        private const string CURRENT_VERSION = "1.0";
        private int selectedPhase = 0;
        private int loopAmount = DEFAULT_REPEAT_AMOUNT;
        private List<SourcePropertyAnimationConfiguration> recordingProperties;

        #endregion
        public SourceAnimationAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = SourceAnimationActionSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<SourceAnimationActionSettings>();
            }
            this.Settings.IsRecording = false;
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            OBSManager.Instance.Connect();
            CheckServerInfoExists();
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            base.Dispose();
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Key Pressed");
            if (String.IsNullOrEmpty(Settings.SourceName))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Key Pressed but source name is null");
                await Connection.ShowAlert();
                return;
            }

            if (Settings.AnimationPhases == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Key Pressed but AnimationPhases is null");
                await Connection.ShowAlert();
                return;
            }

            if (OBSManager.Instance.IsConnected)
            {
                try
                {
                    List<SourcePropertyAnimation> animationPhases = AnimationManager.Instance.ConvertUserSettingsToAnimations(Settings.ToAnimationUserSettings());
                    OBSManager.Instance.AnimateSource(animationPhases, loopAmount);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Keypress Animation Exception: {ex}");
                    await Connection.ShowAlert();
                }
            }
            else
            {
                await Connection.ShowAlert();
            }
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick()
        {
            baseHandledOnTick = false;
            base.OnTick();

            /*
            if (!baseHandledOnTick && !String.IsNullOrEmpty(Settings.TransitionName))
            {
                await Connection.SetTitleAsync($"{Settings.TransitionName}");
            }*/
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            var selectedPhase = Settings.SelectedPhase;
            Tools.AutoPopulateSettings(Settings, payload.Settings);
            if (Settings.SelectedPhase != selectedPhase) // Selected phase was changed
            {
                SetActionGeneralSettings();
            }
            InitializeSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        public override Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }

        private void InitializeSettings()
        {
            if (!Int32.TryParse(Settings.RepeatAmount, out loopAmount))
            {
                Settings.RepeatAmount = DEFAULT_REPEAT_AMOUNT.ToString();
            }

            if (Settings.AnimationPhases == null)
            {
                Settings.AnimationPhases = new List<AnimationPhaseSettings>();
            }

            if (Settings.AnimationPhases.Count == 0)
            {
                AddAnimationPhase(0);
            }

            if (!Int32.TryParse(Settings.SelectedPhase, out selectedPhase))
            {
                Settings.SelectedPhase = "0";
            }
            if (selectedPhase < 0 || selectedPhase >= Settings.AnimationPhases.Count)
            {
                Settings.SelectedPhase = "0";
            }
            SetAnimationPhaseGeneralSettings();

            foreach (var animationPhase in Settings.AnimationPhases)
            {
                if (!Int32.TryParse(animationPhase.AnimationLength, out _))
                {
                    animationPhase.AnimationLength = AnimationPhaseSettings.DEFAULT_ANIMATION_LENGTH_MS.ToString();
                }

                if (!Int32.TryParse(animationPhase.Steps, out _))
                {
                    animationPhase.Steps = AnimationPhaseSettings.DEFAULT_STEPS_NUM.ToString();
                }
            }
            SaveSettings();
        }


        private async void Connection_OnSendToPlugin(object sender, SdTools.Wrappers.SDEventReceivedEventArgs<SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                string fileName;
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "addanimationphasebelow": // Animation PHASE not Animation Property!
                    case "addanimationphaseabove":
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"addAnimationPhase called");
                        int phase = selectedPhase + 1;

                        if (payload["property_inspector"].ToString().ToLowerInvariant() == "addanimationphaseabove")
                        {
                            phase = selectedPhase;
                        }
                        AddAnimationPhase(phase);
                        Settings.SelectedPhase = phase.ToString();
                        SetActionGeneralSettings();
                        InitializeSettings();
                        break;
                    case "delanimationphase": // Animation PHASE not Animation Property!
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"delAnimationPhase called");
                        Settings.AnimationPhases.RemoveAt(selectedPhase);
                        Settings.SelectedPhase = "0";
                        SetActionGeneralSettings();
                        InitializeSettings();
                        break;
                    case "addanimation": // Animation Property NOT Animation Phase!
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"addAnimation called");
                        string propertyName = (string)payload["propertyName"];
                        string startValue = (string)payload["startValue"];
                        string endValue = (string)payload["endValue"];
                        TryAddNewAnimation(propertyName, startValue, endValue);
                        break;
                    case "removeanimation": // Animation Property NOT Animation Phase!
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"removeAnimation called");
                        string indexStr = (string)payload["removeIndex"];
                        if (!Int32.TryParse(indexStr, out int index) || index < 0)
                        {
                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"removeAnimation called with invalid index: {indexStr}");
                            await Connection.ShowAlert();
                            return;
                        }
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"removeAnimation removing item at index {indexStr}");
                        Settings.AnimationPhases[selectedPhase].AnimationActions.RemoveAt(index);
                        await SaveSettings();
                        break;
                    case "exportsettings":
                        fileName = PickersUtil.Pickers.SaveFilePicker("Export Animation Settings", null, "OBS Animation files (*.obsanim)|*.obsanim|All files (*.*)|*.*");
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO, $"Exporting settings to {fileName}");
                            File.WriteAllText(fileName, JsonConvert.SerializeObject(settings));
                            await Connection.ShowOk();
                        }
                        break;
                    case "importsettings":
                        fileName = PickersUtil.Pickers.OpenFilePicker("Import Animation Settings", null, "OBS Animation files (*.obsanim)|*.obsanim|All files (*.*)|*.*");
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            if (!File.Exists(fileName))
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ImportSettings called but file does not exist {fileName}");
                                await Connection.ShowAlert();
                                return;
                            }

                            try
                            {
                                Logger.Instance.LogMessage(TracingLevel.INFO, $"Importing settings from {fileName}");
                                string json = File.ReadAllText(fileName);
                                settings = JsonConvert.DeserializeObject<SourceAnimationActionSettings>(json);
                                await SaveSettings();
                                await Connection.ShowOk();
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ImportSettings exception:\n\t{ex}");
                                await Connection.ShowAlert();
                                return;
                            }
                        }
                        break;
                    case "startrecording":
                        Settings.IsRecording = true;
                        await SaveSettings();
                        StartSourceRecording();
                        break;
                    case "endrecording":
                        Settings.IsRecording = false;
                        await SaveSettings();
                        HandleRecording();
                        break;
                }
            }
        }

        private async void TryAddNewAnimation(string propertyName, string startValue, string endValue)
        {
            if (String.IsNullOrEmpty(propertyName))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TryAddNewAnimation failed - property name is null");
                await Connection.ShowAlert();
                return;
            }

            if (!double.TryParse(startValue, out double startValueDouble))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TryAddNewAnimation failed - invalid start value: {startValue}");
                await Connection.ShowAlert();
                return;
            }

            if (!double.TryParse(endValue, out double endValueDouble))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TryAddNewAnimation failed - invalid end value: {endValue}");
                await Connection.ShowAlert();
                return;
            }

            if (selectedPhase < 0 || selectedPhase >= Settings.AnimationPhases.Count)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TryAddNewAnimation failed - invalid selected phase: {selectedPhase}/{Settings.AnimationPhases.Count}");
                await Connection.ShowAlert();
                return;
            }


            // Create a new list of one doesn't already exist
            if (Settings.AnimationPhases[selectedPhase].AnimationActions == null)
            {
                Settings.AnimationPhases[selectedPhase].AnimationActions = new List<PropertyAnimationConfiguration>();
            }

            AnimationProperties property  = (AnimationProperties)Enum.Parse(typeof(AnimationProperties), propertyName);
            Settings.AnimationPhases[selectedPhase].AnimationActions.Add(new PropertyAnimationConfiguration(property, startValueDouble, endValueDouble));
            await SaveSettings();
        }

        private void AddAnimationPhase(int position)
        {
            if (Settings.AnimationPhases == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"AddAnimationPhase: AnimationPhases is null, creating new list");
                Settings.AnimationPhases = new List<AnimationPhaseSettings>();
            }

            var animationPhase = new AnimationPhaseSettings
            {
                AnimationLength = Settings.AnimationLength,
                Steps = Settings.Steps,
                ResetDefaults = Settings.AnimationPhases.Count == 0, // Only first stage should have reset on by default
                HideSourceAtStart = Settings.HideSourceAtStart,
                HideSourceAtEnd = Settings.HideSourceAtEnd,
                RemoveFilterAtEnd = Settings.RemoveFilterAtEnd,
                PhaseName = $"Phase {position}"
            };
            Settings.PhaseName = animationPhase.PhaseName;

            Settings.AnimationPhases.Insert(position, animationPhase);
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Created new Animation phase {animationPhase.PhaseName} at position {position} with Length: {animationPhase.AnimationLength} Steps: {animationPhase.Steps} ResetDefault: {animationPhase.ResetDefaults}");
        }

        private void SetAnimationPhaseGeneralSettings()
        {
            if (selectedPhase >= 0 && selectedPhase < Settings.AnimationPhases.Count)
            {
                Settings.AnimationPhases[selectedPhase].AnimationLength = Settings.AnimationLength;
                Settings.AnimationPhases[selectedPhase].Steps = Settings.Steps;
                Settings.AnimationPhases[selectedPhase].HideSourceAtStart = Settings.HideSourceAtStart;
                Settings.AnimationPhases[selectedPhase].HideSourceAtEnd = Settings.HideSourceAtEnd;
                Settings.AnimationPhases[selectedPhase].RemoveFilterAtEnd = Settings.RemoveFilterAtEnd;
                Settings.AnimationPhases[selectedPhase].ResetDefaults = Settings.ResetDefaults;
                Settings.AnimationPhases[selectedPhase].PhaseName = Settings.PhaseName;
            }
        }

        private void SetActionGeneralSettings()
        {
            if (Int32.TryParse(Settings.SelectedPhase, out int newSelectedPhase))
            { 
                if (newSelectedPhase >= 0 && newSelectedPhase < Settings.AnimationPhases.Count)
                {
                    Settings.AnimationLength = Settings.AnimationPhases[newSelectedPhase].AnimationLength;
                    Settings.Steps = Settings.AnimationPhases[newSelectedPhase].Steps;
                    Settings.HideSourceAtStart = Settings.AnimationPhases[newSelectedPhase].HideSourceAtStart;
                    Settings.HideSourceAtEnd = Settings.AnimationPhases[newSelectedPhase].HideSourceAtEnd;
                    Settings.RemoveFilterAtEnd = Settings.AnimationPhases[newSelectedPhase].RemoveFilterAtEnd;
                    Settings.ResetDefaults = Settings.AnimationPhases[newSelectedPhase].ResetDefaults;
                    Settings.PhaseName = Settings.AnimationPhases[newSelectedPhase].PhaseName;
                }
            }
        }

        private void StartSourceRecording()
        {
            recordingProperties = OBSManager.Instance.GetSourceProperties(Settings.SourceName, out string errorMessage);
            if (!String.IsNullOrEmpty(errorMessage))
            {
                Settings.IsRecording = false;
                SaveSettings();
                MessageBox.Show(errorMessage, "Source Animation Error");
            }
        }

        private void HandleRecording()
        {
            var newProperties = OBSManager.Instance.GetSourceProperties(Settings.SourceName, out string errorMessage);
            if (!String.IsNullOrEmpty(errorMessage))
            {
                MessageBox.Show(errorMessage, "Source Animation Error");
                return;
            }

            CompareRecordings(recordingProperties, newProperties);
        }

        private void CompareRecordings(List<SourcePropertyAnimationConfiguration> startProperties, List<SourcePropertyAnimationConfiguration> endProperties)
        {
            foreach (var propertyConfiguration in startProperties)
            {
                var endConfiguration = endProperties.Where(p => p.PropertyName == propertyConfiguration.PropertyName).FirstOrDefault();
                if (endConfiguration == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Missing end configuration from property {propertyConfiguration.PropertyName}");
                    continue;
                }

                // Setting has changed, add to animation list
                if (propertyConfiguration.StartValue != endConfiguration.StartValue)
                {
                    TryAddNewAnimation(propertyConfiguration.PropertyName.ToString(), propertyConfiguration.StartValue.ToString(), endConfiguration.StartValue.ToString());
                }
            }

            // Check if there are any endproperties that did NOT have a startProperty, these need to use the default value
            foreach (var endConfiguration in endProperties.Where(p => !startProperties.Any(s => s.PropertyName == p.PropertyName)).ToList())
            {
                string defaultPropertyValue = AnimationManager.Instance.GetPropertyDefaultValue(endConfiguration.PropertyName);
                if (!String.IsNullOrEmpty(defaultPropertyValue))
                {
                    TryAddNewAnimation(endConfiguration.PropertyName.ToString(), defaultPropertyValue, endConfiguration.StartValue.ToString());
                }
            }

            SaveSettings();
        }


        #endregion
    }
}
