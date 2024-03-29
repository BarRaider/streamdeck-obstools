﻿using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Properties;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using NLog.Time;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using OTI.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace BarRaider.ObsTools.Backend
{

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // CarstenPet - 1 Gifted Subs
    // tobitege - 5 Gifted Subs
    //---------------------------------------------------
    public class OBSManager
    {
        #region Private Members

        private const string CONNECTION_STRING = "ws://{0}:{1}";
        private const string REPLAY_ALREADY_ACTIVE_ERROR_MESSAGE = "replay buffer already active";
        private readonly Version MINIMUM_SUPPORTED_WEBSOCKET_VERSION = new Version("5.0");
        private readonly string[] AUDIO_INPUT_KINDS = new string[] { "browser_source", "dshow_input", "ffmpeg_source", "wasapi_input_capture", "wasapi_output_capture", "wasapi_process_output_capture" };


        private readonly SemaphoreSlim connectLock = new SemaphoreSlim(1, 1);
        private const int CONNECT_COOLDOWN_MS = 9500;
        private const int AUTO_CONNECT_SLEEP_MS = 10000;

        private static OBSManager instance = null;
        private static readonly object objLock = new object();
        private readonly OBSWebsocket obs;
        private readonly System.Timers.Timer tmrCheckStatus = new System.Timers.Timer();


        private DateTime lastConnectAttempt = DateTime.MinValue;
        private bool internalConnected = false;
        private bool disconnectCalled = false;
        private bool autoConnectRunning = false;
        private static readonly object autoConnectLock = new object();

        private string previousSceneBackupName = string.Empty;

        #endregion

        #region Constructors

        public static OBSManager Instance
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
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"OBS Manager Created");
                        instance = new OBSManager();
                    }
                    return instance;
                }
            }
        }

        private OBSManager()
        {
            internalConnected = false;
            IsValidVersion = true;
            obs = new OBSWebsocket();

            obs.Connected += Obs_Connected;
            obs.Disconnected += Obs_Disconnected;
            obs.CurrentProgramSceneChanged += Obs_CurrentProgramSceneChanged;
            obs.StreamStateChanged += Obs_StreamStateChanged;
            obs.RecordStateChanged += Obs_RecordStateChanged;
            obs.ReplayBufferStateChanged += Obs_ReplayBufferStateChanged;
            obs.CurrentPreviewSceneChanged += Obs_CurrentPreviewSceneChanged;
            obs.StudioModeStateChanged += Obs_StudioModeStateChanged;

            ServerManager.Instance.TokensChanged += Instance_TokensChanged;

            tmrCheckStatus.Interval = 1000;
            tmrCheckStatus.Elapsed += TmrCheckStatus_Elapsed;
            tmrCheckStatus.Start();

            InstantReplyStatus = OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED;
            Connect();
            InstantReplayWatcher.Instance.Initialize();

        }

        #endregion

        #region Public Methods

        public event EventHandler ObsConnectionChanged;
        public event EventHandler<Exception> ObsConnectionFailed;
        public event EventHandler<OutputStateChanged> StreamStatusChanged;
        public event EventHandler<OutputStateChanged> RecordingStatusChanged;
        public event EventHandler<SceneChangedEventArgs> SceneChanged;
        public event EventHandler<OutputStateChanged> ReplayBufferStateChanged;
        public event EventHandler<ObsStats> ObsStatsChanged;

        public bool IsConnected
        {
            get
            {
                return obs != null && obs.IsConnected && internalConnected;
            }
        }

        public bool IsValidVersion { get; private set; }

        public string CurrentSceneName { get; private set; }

        public string CurrentPreviewSceneName { get; private set; }

        public string PreviousSceneName { get; private set; }

        public string NextSceneName { get; private set; }

        public OutputState InstantReplyStatus { get; private set; }

        public bool IsStreaming
        {
            get
            {
                return IsConnected && LastStreamingStats != null && LastStreamingStats.IsActive;
            }
        }

        public bool IsRecording
        {
            get
            {
                return IsConnected && LastRecordingStats != null && LastRecordingStats.IsRecording;
            }
        }

        public OutputStatus LastStreamingStats { get; private set; }

        public RecordingStatus LastRecordingStats { get; private set; }

        public ObsStats LastObsStats { get; private set; }

        public async void Connect(bool autoConnect = false)
        {
            if (!autoConnect)
            {
                disconnectCalled = false;
            }

            if (!obs.IsConnected)
            {
                await connectLock.WaitAsync();
                try
                {
                    if (obs.IsConnected)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Connect: Already connected");
                        return;
                    }

                    if ((DateTime.Now - lastConnectAttempt).TotalMilliseconds < CONNECT_COOLDOWN_MS)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Connect: In cooldown");
                        return;
                    }

                    var serverInfo = ServerManager.Instance.ServerInfo;
                    if (serverInfo == null)
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"Connect: Server info missing");
                        return;
                    }

                    if (!autoConnect)  // Don't spam log
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Attempting to connect to {serverInfo.Ip}:{serverInfo.Port}");
                    }
                    lastConnectAttempt = DateTime.Now;
                    obs.WSTimeout = new TimeSpan(0, 0, 5);
                    obs.ConnectAsync(String.Format(CONNECTION_STRING, serverInfo.Ip, serverInfo.Port), serverInfo.Password);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Connection Exception: {ex}");
                    ObsConnectionFailed?.Invoke(this, ex);
                }
                finally
                {
                    connectLock.Release();
                }
            }
        }

        public void Disconnect()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Disconnect() function was called");
            disconnectCalled = true;
            if (obs.IsConnected)
            {
                obs.Disconnect();
            }
        }

        public bool ChangeScene(string sceneName)
        {
            if (IsConnected)
            {
                try
                {
                    obs.SetCurrentProgramScene(sceneName);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"ChangeScene Exception: {ex}");
                }
            }
            return false;
        }

        public bool StartInstantReplay()
        {
            if (IsConnected)
            {
                try
                {
                    obs.StartReplayBuffer();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"StartInstantReplay Exception: {ex}");
                    if (ex.Message == REPLAY_ALREADY_ACTIVE_ERROR_MESSAGE)
                    {
                        InstantReplyStatus = OutputState.OBS_WEBSOCKET_OUTPUT_STARTED;
                    }
                }
            }
            return false;
        }

        public bool StartRecording()
        {
            if (IsConnected)
            {
                try
                {
                    obs.StartRecord();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"StartRecording Exception: {ex}");
                }
            }
            return false;
        }

        public bool StopRecording()
        {
            if (IsConnected)
            {
                try
                {
                    obs.StopRecord();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"StopRecording Exception: {ex}");
                }
            }
            return false;
        }

        public bool PauseRecording()
        {
            if (IsConnected)
            {
                try
                {
                    obs.PauseRecord();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"PauseRecording Exception: {ex}");
                }
            }
            return false;
        }

        public bool ResumeRecording()
        {
            if (IsConnected)
            {
                try
                {
                    obs.ResumeRecord();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"ResumeRecording Exception: {ex}");
                }
            }
            return false;
        }

        public bool StartStreaming()
        {
            if (IsConnected)
            {
                try
                {
                    obs.StartStream();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"StartStreaming Exception: {ex}");
                }
            }
            return false;
        }

        public bool StopStreaming()
        {
            if (IsConnected)
            {
                try
                {
                    obs.StopStream();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"StopStreaming Exception: {ex}");
                }
            }
            return false;
        }

        public bool StartStudioMode()
        {
            if (IsConnected)
            {
                try
                {
                    obs.SetStudioModeEnabled(true);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"StartStudioMode Exception: {ex}");
                }
            }
            return false;
        }

        public bool StopStudioMode()
        {
            if (IsConnected)
            {
                try
                {
                    obs.SetStudioModeEnabled(false);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"StopStudioMode Exception: {ex}");
                }
            }
            return false;
        }

        public bool ToggleStudioMode()
        {
            if (IsConnected)
            {
                try
                {
                    bool studioEnabled = obs.GetStudioModeEnabled();
                    obs.SetStudioModeEnabled(!studioEnabled);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"ToggleStudioMode Exception: {ex}");
                }
            }
            return false;
        }

        public bool IsStudioModeEnabled()
        {
            if (IsConnected)
            {
                try
                {
                    return obs.GetStudioModeEnabled();
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"IsStudioModeEnabled Exception: {ex}");
                }
            }
            return false;
        }

        public bool StopInstantReplay()
        {
            if (IsConnected)
            {
                try
                {
                    obs.StopReplayBuffer();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"StopInstantReplay Exception: {ex}");
                }
            }
            return false;
        }

        public Task<bool> SaveInstantReplay()
        {
            return Task.Run(() =>
            {
                if (IsConnected)
                {
                    try
                    {
                        obs.SaveReplayBuffer();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"SaveInstantReplay Exception: {ex}");
                    }
                }
                return false;
            });
        }

        public Task<bool> ModifyBrowserInput(string sceneName, string inputName, string urlOrFile, bool localFile, int delayReplaySeconds, int hideReplaySeconds, bool muteSound)
        {
            return Task.Run(() =>
            {
                if (String.IsNullOrEmpty(inputName))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyBrowserInput failed. Missing Input name");
                    return false;
                }

                if (IsConnected)
                {
                    try
                    {
                        Thread.Sleep(delayReplaySeconds * 1000);

                        sceneName = GetValidSceneName(sceneName);
                        obs.SetInputMute(inputName, muteSound);
                        int itemId = obs.GetSceneItemId(sceneName, inputName, 0);
                        obs.SetSceneItemEnabled(sceneName, itemId, false);
                        var sourceSettings = obs.GetInputSettings(inputName);
                        var browserSettings = InputBrowserSourceSettings.FromInputSettings(sourceSettings);
                        browserSettings.IsLocalFile = localFile;
                        if (localFile)
                        {
                            browserSettings.LocalFile = urlOrFile;
                        }
                        else
                        {
                            browserSettings.URL = urlOrFile;
                        }
                        obs.SetInputSettings(inputName, JObject.FromObject(browserSettings));
                        Thread.Sleep(200);
                        obs.SetSceneItemEnabled(sceneName, itemId, true);

                        if (hideReplaySeconds > 0)
                        {
                            Task.Run(() =>
                            {
                                Thread.Sleep(hideReplaySeconds * 1000);
                                obs.SetSceneItemEnabled(sceneName, itemId, false);
                                Logger.Instance.LogMessage(TracingLevel.INFO, $"ModifyBrowserSource AutoHid source {inputName} after {hideReplaySeconds} seconds");
                            });
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyBrowserSource for url {urlOrFile} failed. Exception: {ex}");
                    }
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyBrowserSource for url {urlOrFile} failed. OBS is not connected");
                }
                return false;
            });
        }

        public Task<bool> ModifyImageSource(string sceneName, string inputName, string fileName, int autoHideSeconds)
        {
            return Task.Run(() =>
            {
                if (String.IsNullOrEmpty(inputName))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyImageSource failed. Missing source name");
                    return false;
                }

                if (IsConnected)
                {
                    try
                    {
                        sceneName = GetValidSceneName(sceneName);
                        int itemId = obs.GetSceneItemId(sceneName, inputName, 0);
                        obs.SetSceneItemEnabled(sceneName, itemId, false);
                        var sourceSettings = obs.GetInputSettings(inputName);
                        if (sourceSettings == null)
                        {
                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyImageSource: GetSourceSettings return null for source {inputName}");
                            return false;
                        }

                        if (sourceSettings.InputKind != "image_source")
                        {
                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyImageSource: Source {inputName} is not an image source: {sourceSettings.InputKind}");
                            return false;
                        }

                        sourceSettings.Settings["file"] = fileName;
                        obs.SetInputSettings(inputName, sourceSettings.Settings);
                        Thread.Sleep(200);
                        obs.SetSceneItemEnabled(sceneName, itemId, true);

                        if (autoHideSeconds > 0)
                        {
                            Task.Run(() =>
                            {
                                Thread.Sleep(autoHideSeconds * 1000);
                                obs.SetSceneItemEnabled(sceneName, itemId, false);
                                Logger.Instance.LogMessage(TracingLevel.INFO, $"ModifyImageSource AutoHid source {inputName} after {autoHideSeconds} seconds");
                            });
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyImageSource for image {fileName} failed. Exception: {ex}");
                    }
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyImageSource for image {fileName} failed. OBS is not connected");
                }
                return false;
            });
        }

        public Task<bool> PlayInstantReplay(SourcePropertyVideoPlayer settings, bool autoSwitchToScene)
        {
            if (IsConnected)
            {
                try
                {
                    string currentScene = obs.GetCurrentProgramScene();
                    if (String.IsNullOrEmpty(settings.SceneName) || settings.SceneName == Constants.ACTIVE_SCENE_CAPTION)
                    {
                        settings.SceneName = currentScene;
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"PlayInstantReplay using active scene {currentScene}");
                    }
                    else if (autoSwitchToScene && settings.SceneName != currentScene)
                    {
                        // Switch to scene
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"PlayInstantReplay auto switching to scene {settings.SceneName}");
                        obs.SetCurrentProgramScene(settings.SceneName);
                    }
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"(Additional logs in oti.log file)");
                    return AnimationManager.Instance.HandleMediaPlayer(obs, settings);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"PlayInstantReplay Exception: {ex}");
                }
            }
            return null;
        }

        public VolumeInfo GetInputVolume(string inputName)
        {
            if (IsConnected)
            {
                try
                {

                    return obs.GetInputVolume(inputName);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetInputVolume Exception Source:{inputName}: {ex}");
                }
            }
            return null;
        }

        public bool SetInputVolume(string inputName, float volume, bool volumeModeDB)
        {
            if (IsConnected)
            {
                try
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting volume for source {inputName} to {volume}");
                    obs.SetInputVolume(inputName, volume, volumeModeDB);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetSourceVolume Exception Source:{inputName}: {ex}");
                }
            }
            return false;
        }

        public bool SetPreviewScene(string sceneName)
        {
            if (IsConnected)
            {
                try
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting preview scene to {sceneName}");
                    obs.SetCurrentPreviewScene(sceneName);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetPreviewScene Exception Scene: {sceneName}: {ex}");
                }
            }
            return false;
        }

        public bool SetScene(string sceneName)
        {
            if (IsConnected)
            {
                try
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting current scene to {sceneName}");
                    obs.SetCurrentProgramScene(sceneName);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetScene Exception: {ex}");
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a base 64 snapshot image of the current source
        /// </summary>
        /// <param name="sourceName"></param>
        /// <returns></returns>
        public string GetSourceSnapshot(string sourceName)
        {
            if (IsConnected)
            {
                try
                {
                    return obs.GetSourceScreenshot(sourceName, "png", 144, 144);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetSourceSnapshot Exception for source {sourceName}: {ex}");
                }
            }
            return null;
        }

        public bool AnimateSource(List<SourcePropertyAnimation> animationPhases, int repeatAmount)
        {
            try
            {
                if (animationPhases == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"AnimateSource - animationPhases is null");
                    return false;
                }

                if (IsConnected)
                {
                    AnimationManager.Instance.HandleAnimation(obs, animationPhases, repeatAmount);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"AnimateSource Exception: {ex}");
            }
            return false;
        }

        public bool SetWindowCaptureWindow(string sourceName, string windowInfo)
        {
            try
            {
                if (IsConnected)
                {
                    var sourceInfo = obs.GetInputSettings(sourceName);
                    sourceInfo.Settings["window"] = windowInfo;
                    obs.SetInputSettings(sourceName, sourceInfo.Settings);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetWindowCaptureWindow Exception Source: {sourceName} Window {windowInfo}: {ex}");
            }
            return false;
        }

        public bool ToggleSourceVisibility(string sceneName, string sourceName)
        {
            try
            {
                if (IsConnected)
                {
                    var itemId = obs.GetSceneItemId(sceneName, sourceName, 0);
                    var isVisibile = obs.GetSceneItemEnabled(sceneName, itemId);
                    obs.SetSceneItemEnabled(sceneName, itemId, !isVisibile);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ToggleSourceVisibility Exception Source: {sourceName} Scene: {sceneName}: {ex}");
            }
            return false;
        }

        public bool IsSourceVisible(string sceneName, string sourceName)
        {
            try
            {
                if (IsConnected)
                {
                    var itemId = obs.GetSceneItemId(sceneName, sourceName, 0);
                    return obs.GetSceneItemEnabled(sceneName, itemId);
                }
            }
            catch (OBSWebsocketDotNet.ErrorResponseException) { }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"IsSourceVisible Exception Source: {sourceName} Scene: {sceneName}: {ex}");
            }
            return false;
        }
        public bool ToggleInputMute(string inputName)
        {
            try
            {
                if (IsConnected)
                {
                    obs.ToggleInputMute(inputName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ToggleInputeMute Exception Source: {inputName}: {ex}");
            }
            return false;
        }

        public bool IsInputMuted(string inputName)
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetInputMute(inputName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"IsInputMuted Exception Source: {inputName} {ex}");
            }
            return false;
        }

        public void ToggleFilterVisibility(string sourceName, string filterName, bool enableFilter)
        {
            try
            {
                if (IsConnected)
                {
                    obs.SetSourceFilterEnabled(sourceName, filterName, enableFilter);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ToggleFilterVisibility Exception for Source {sourceName} and Filter {filterName} {ex}");
            }
        }

        public bool? IsFilterEnabled(string sourceName, string filterName, out bool exceptionRaised)
        {
            exceptionRaised = false;
            try
            {
                if (IsConnected)
                {
                    var filterInfo = obs.GetSourceFilter(sourceName, filterName);
                    if (filterInfo != null)
                    {
                        return filterInfo.IsEnabled;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"IsFilterEnabled Exception for Source {sourceName} and Filter {filterName} {ex}");
                exceptionRaised = true;
            }
            return null;
        }

        public void ToggleVirtualCam()
        {
            try
            {
                if (IsConnected)
                {
                    obs.ToggleVirtualCam();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ToggleVirtualCam Exception {ex}");
            }
        }

        public bool? IsVirtualCamEnabled()
        {
            try
            {
                if (IsConnected)
                {
                    var camStatus = obs.GetVirtualCamStatus();
                    if (camStatus != null)
                    {
                        return camStatus.IsActive;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"IsVirtualCamEnabled Exception {ex}");
            }
            return null;
        }

        public List<FilterSettings> GetSourceFilters(string sourceName)
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetSourceFilterList(sourceName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetSourceFilters Exception {ex}");
            }
            return null;
        }

        public List<TransitionSettings> GetAllTransitions()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetSceneTransitionList().Transitions;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllTransitions Exception {ex}");
            }
            return null;
        }

        public GetSceneListInfo GetAllScenes()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetSceneList();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllScenes Exception {ex}");
            }
            return null;
        }
        public List<SceneItemDetails> GetAllSourcesForScene(string sceneName)
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetSceneItemList(sceneName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllSources Exception {ex}");
            }
            return null;
        }

        public List<InputBasicInfo> GetAllInputs()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetInputList();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllScenes Exception {ex}");
            }
            return null;
        }

        public List<InputBasicInfo> GetAudioInputs()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetInputList().Where(i => AUDIO_INPUT_KINDS.Contains(i.UnversionedKind)).ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllScenes Exception {ex}");
            }
            return null;

        }

        public List<SceneSourceInfo> GetAllSceneAndSceneItemNames()
        {
            try
            {
                if (IsConnected)
                {
                    List<SceneSourceInfo> list = new List<SceneSourceInfo>();

                    var scenes = GetAllScenes();
                    foreach (var scene in scenes.Scenes)
                    {
                        list.AddRange(GetAllSourcesForScene(scene.Name)?.Select(s => new SceneSourceInfo() { Name = s.SourceName }).ToList());
                    }
                    list.AddRange(scenes?.Scenes?.Select(s => new SceneSourceInfo() { Name = s.Name }).ToList());

                    // Distinct
                    return list.OrderBy(n => n.Name).GroupBy(g => g.Name).Select(grp => grp.FirstOrDefault()).ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllSceneAndSourceNames Exception {ex}");
            }
            return null;
        }

        public List<String> GetAllSceneCollections()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetSceneCollectionList();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllSceneCollections Exception: {ex}");
            }
            return null;
        }
        public List<SceneItemDetails> GetSceneSources(string sceneName)
        {
            try
            {
                if (IsConnected)
                {
                    sceneName = GetValidSceneName(sceneName);
                    return obs.GetSceneItemList(sceneName).ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetSceneSources Exception {ex}");
            }
            return null;
        }

        public string GetSceneCollection()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetCurrentSceneCollection();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetSceneCollection Exception: {ex}");
            }
            return null;
        }

        public void SetSceneCollection(string sceneCollectionName)
        {
            try
            {
                if (IsConnected)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting Scene Collection to: {sceneCollectionName}");
                    obs.SetCurrentSceneCollection(sceneCollectionName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetSceneCollection Exception: {ex}");
            }
        }

        public TransitionSettings GetCurrentTransition()
        {
            try
            {
                if (IsConnected)
                {
                    var transition = obs.GetCurrentSceneTransition();
                    return transition;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetTransition Exception: {ex}");
            }
            return null;
        }

        public void SetTransition(string transitionName)
        {
            try
            {
                if (IsConnected)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting Transition to: {transitionName}");
                    obs.SetCurrentSceneTransition(transitionName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetTransition Exception: {ex}");
            }
        }

        public void SetCurrentTransitionDuration(int duration)
        {
            try
            {
                if (IsConnected)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting Transition Duration to: {duration}");
                    obs.SetCurrentSceneTransitionDuration(duration);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetTransition Exception: {ex}");
            }
        }

        public List<String> GetAllProfiles()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetProfileList().Profiles;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllProfiles Exception: {ex}");
            }
            return null;
        }

        public void SetProfile(string profileName)
        {
            try
            {
                if (IsConnected)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting Profile to: {profileName}");
                    obs.SetCurrentProfile(profileName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetProfile Exception: {ex}");
            }
        }

        public string GetProfile()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetProfileList().CurrentProfileName;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetProfile Exception: {ex}");
            }
            return null;
        }

        public List<SourcePropertyAnimationConfiguration> GetSceneItemProperties(string sceneName, string sourceName, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                if (IsConnected)
                {
                    return AnimationManager.Instance.GetSceneItemProperties(obs, sceneName, sourceName, out errorMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetSourceProperties Exception: {ex}");
            }
            return null;
        }

        public bool IsReplayBufferEnabled()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetReplayBufferStatus();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"IsReplayBufferEnabled Exception: {ex}");
            }
            return false;
        }

        public RecordingStatus GetRecordingStatus()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetRecordStatus();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetRecordingStatus Exception: {ex}");
            }
            return null;
        }

        public bool TriggerHotkey(string obsKeyCode, bool ctrl, bool alt, bool shift, bool win)
        {
            try
            {
                if (IsConnected)
                {
                    KeyModifier keyModifier = KeyModifier.None;
                    if (ctrl)
                    {
                        keyModifier |= KeyModifier.Control;
                    }
                    if (alt)
                    {
                        keyModifier |= KeyModifier.Alt;
                    }
                    if (shift)
                    {
                        keyModifier |= KeyModifier.Shift;
                    }
                    if (win)
                    {
                        keyModifier |= KeyModifier.Command;
                    }

                    OBSHotkey hotkey = (OBSHotkey)Enum.Parse(typeof(OBSHotkey), obsKeyCode, true);
                    obs.TriggerHotkeyByKeySequence(hotkey, keyModifier);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TriggerHotkey Exception: {ex}");
            }
            return false;
        }

        public void SetInputAudioMonitorType(string inputName, MonitorTypes monitorType)
        {
            try
            {
                if (IsConnected)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting {inputName} MonitorType to: {monitorType}");
                    obs.SetInputAudioMonitorType(inputName, monitorType.ToStringEx());
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetAudioMonitorType Exception: {ex}");
            }
        }

        public MonitorTypes GetInputAudioMonitorType(string inputName)
        {
            try
            {
                if (IsConnected)
                {
                    string monitorType = obs.GetInputAudioMonitorType(inputName);
                    return monitorType.ToMonitorType();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAudioMonitorType Exception: {ex}");
            }
            return MonitorTypes.None;
        }

        #endregion

        #region Private Methods

        private void HandlePostConnectionSettings()
        {
            try
            {
                var version = obs.GetVersion();
                if (version != null)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"OBS Version: {version.OBSStudioVersion}");
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"WebSocket Version: {version.PluginVersion}");
                }

                LastStreamingStats = obs.GetStreamStatus();
                LastRecordingStats = obs.GetRecordStatus();
                CurrentSceneName = obs.GetCurrentProgramScene();

                CurrentPreviewSceneName = String.Empty;
                if (IsStudioModeEnabled())
                {
                    CurrentPreviewSceneName = obs.GetCurrentPreviewScene();
                }

                Task.Run(() => GetNextSceneName());
                Task.Run(() => ObsConnectionChanged?.Invoke(this, EventArgs.Empty));
                VerifyValidVersion(version);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"CONNECTION EXCEPTION! HandlePostConnectionSettings Exception: {ex}");
            }
        }


        private void GetNextSceneName()
        {
            NextSceneName = string.Empty;
            var scenes = GetAllScenes();
            int idx = scenes.Scenes.FindIndex(x => x.Name == scenes.CurrentProgramSceneName) - 1;
            if (idx >= 0 && idx < scenes.Scenes.Count)
            {
                NextSceneName = scenes.Scenes[idx].Name;
            }
        }
        private void CheckStatus()
        {
            try
            {
                if (IsConnected)
                {
                    // Todo: Limit this to every x seconds
                    FetchStatsFromOBS();

                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"CheckStatus Exception {ex}");
            }
        }

        private void FetchStatsFromOBS()
        {
            LastObsStats = null;
            if (IsConnected && ObsStatsChanged != null)
            {
                LastObsStats = obs.GetStats();
                ObsStatsChanged?.Invoke(this, LastObsStats);
            }
        }

        private void TmrCheckStatus_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckStatus();
        }
        private void VerifyValidVersion(ObsVersion obsVersion)
        {
            IsValidVersion = false;
            if (obsVersion == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "VerifyValidVersion - Version is null");
                Disconnect();
                return;
            }

            Version pluginVersion = new Version(obsVersion.PluginVersion);
            if (pluginVersion < MINIMUM_SUPPORTED_WEBSOCKET_VERSION)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"VerifyValidVersion - obs-websocket version is not up to date: {pluginVersion} expected {MINIMUM_SUPPORTED_WEBSOCKET_VERSION}");
                Disconnect();
                return;
            }

            IsValidVersion = true;
        }

        private void AutoConnectBackgroundWorker()
        {
            try
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} AutoConnect enabled");
                while (!obs.IsConnected && !disconnectCalled)
                {
                    Connect(true);
                    Thread.Sleep(AUTO_CONNECT_SLEEP_MS);
                }

                if (obs.IsConnected)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} AutoConnectBackgroundWorker stopped: OBS is connected!");
                }

                if (disconnectCalled)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} AutoConnectBackgroundWorker stopped: Disconnect was called!");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} AutoConnectBackgroundWorker Exception: {ex}");
            }
            finally
            {
                autoConnectRunning = false;
            }
        }

        private string GetValidSceneName(string inputedSceneName)
        {
            if (String.IsNullOrEmpty(inputedSceneName) || inputedSceneName == Constants.ACTIVE_SCENE_CAPTION)
            {
                if (IsConnected)
                {
                    string activeScene = obs.GetCurrentProgramScene();
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} GetValidSceneName using {activeScene}");
                    return activeScene;
                }
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} GetValidSceneName called but OBS isn't connected!");
            }
            return inputedSceneName;
        }

        #endregion

        #region Event Listeners

        private void Obs_Connected(object sender, EventArgs e)
        {
            try
            {
                internalConnected = true;
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Connected to OBS");

                Task.Run(() => HandlePostConnectionSettings());
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"CONNECTION EXCEPTION! [OBSManager] Exception: {ex}");
            }
        }

        private void Obs_Disconnected(object sender, OBSWebsocketDotNet.Communication.ObsDisconnectionInfo e)
        {
            internalConnected = false;

            if (e.ObsCloseCode == OBSWebsocketDotNet.Communication.ObsCloseCodes.AuthenticationFailed)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Invalid password, could not connect");
                ServerManager.Instance.InitTokens(null, null, null, DateTime.Now);
                ObsConnectionFailed?.Invoke(this, new AuthFailureException());
                return;
            }
            else if (e.WebsocketDisconnectionInfo?.CloseStatus == System.Net.WebSockets.WebSocketCloseStatus.ProtocolError ||
                     e.WebsocketDisconnectionInfo?.CloseStatus == System.Net.WebSockets.WebSocketCloseStatus.InternalServerError)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Websocket version! {e.WebsocketDisconnectionInfo?.CloseStatusDescription}");
                ObsConnectionFailed?.Invoke(this, new InvalidOperationException(e.WebsocketDisconnectionInfo?.CloseStatusDescription));
                return;
            }

            if (!autoConnectRunning) // Don't spam logs
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Disconnected from OBS. CloseCode: {e.ObsCloseCode} Reason: {e.DisconnectReason} Exception: {e.WebsocketDisconnectionInfo?.Exception}\nInner Exception: {e.WebsocketDisconnectionInfo?.Exception?.InnerException?.Message}");
            }
            ObsConnectionChanged?.Invoke(this, EventArgs.Empty);
            if (!disconnectCalled)
            {
                lock (autoConnectLock)
                {
                    if (!autoConnectRunning && ServerManager.Instance.ServerInfo != null)
                    {
                        autoConnectRunning = true;
                        Task.Run(() => AutoConnectBackgroundWorker());
                    }
                }
            }
        }

        private void Obs_RecordStateChanged(object sender, OBSWebsocketDotNet.Types.Events.RecordStateChangedEventArgs e)
        {
            Task.Run(() =>
            {
                LastRecordingStats = obs.GetRecordStatus();
                RecordingStatusChanged?.Invoke(this, e.OutputState);
            });
        }

        private void Obs_StreamStateChanged(object sender, OBSWebsocketDotNet.Types.Events.StreamStateChangedEventArgs e)
        {
            Task.Run(() =>
            {
                LastStreamingStats = obs.GetStreamStatus();
                StreamStatusChanged?.Invoke(this, e.OutputState);
            });
        }

        private void Obs_CurrentProgramSceneChanged(object sender, OBSWebsocketDotNet.Types.Events.ProgramSceneChangedEventArgs e)
        {

            // This can be caused by a race condition with the CheckStatus() function
            if (CurrentSceneName == e.SceneName && !String.IsNullOrEmpty(previousSceneBackupName))
            {
                PreviousSceneName = previousSceneBackupName;
                previousSceneBackupName = String.Empty;
            }
            else
            {
                PreviousSceneName = CurrentSceneName;
                CurrentSceneName = e.SceneName;
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"New scene received from OBS: {e.SceneName}");
            SceneChanged?.Invoke(this, new SceneChangedEventArgs(e.SceneName));
            Task.Run(() => GetNextSceneName());
        }

        private void Obs_CurrentPreviewSceneChanged(object sender, OBSWebsocketDotNet.Types.Events.CurrentPreviewSceneChangedEventArgs e)
        {
            CurrentPreviewSceneName = e.SceneName;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"New preview/studio scene received from OBS: {e.SceneName}");
        }

        private void Obs_StudioModeStateChanged(object sender, OBSWebsocketDotNet.Types.Events.StudioModeStateChangedEventArgs e)
        {
            if (e.StudioModeEnabled)
            {
                CurrentPreviewSceneName = obs.GetCurrentPreviewScene();
            }
            else
            {
                CurrentPreviewSceneName = String.Empty;
            }
        }

        private void Instance_TokensChanged(object sender, ServerInfoEventArgs e)
        {
            if (ServerManager.Instance.ServerInfoExists && !obs.IsConnected)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"OBSManager: ServerInfo Exists - Connecting");
                Connect();
            }
            else if (!ServerManager.Instance.ServerInfoExists)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"OBSManager: ServerInfo does not exist - Disconnecting");
                Disconnect();
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"OBSManager: Tokens changed - connection remains open");
            }
        }

        private void Obs_ReplayBufferStateChanged(object sender, OBSWebsocketDotNet.Types.Events.ReplayBufferStateChangedEventArgs e)
        {
            InstantReplyStatus = e.OutputState.State;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"New replay buffer state received from OBS: {e.OutputState.StateStr}");
            ReplayBufferStateChanged?.Invoke(this, e.OutputState);
        }

        #endregion

    }
}
