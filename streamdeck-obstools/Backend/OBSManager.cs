using BarRaider.ObsTools.Backend;
using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using NLog.Time;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using OTI.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

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
        private readonly Version MINIMUM_SUPPORTED_WEBSOCKET_VERSION = new Version("4.9");
        private readonly SemaphoreSlim connectLock = new SemaphoreSlim(1, 1);
        private const int CONNECT_COOLDOWN_MS = 10000;
        private const int AUTO_CONNECT_SLEEP_MS = 10000;
        private const int INVALID_WEBSOCKET_VERSION_ERROR_CODE = 4009;

        private static OBSManager instance = null;
        private static readonly object objLock = new object();
        private readonly OBSWebsocket obs;
        private DateTime lastStreamStatus;
        private readonly System.Timers.Timer tmrCheckStatus = new System.Timers.Timer();
        private DateTime lastConnectAttempt = DateTime.MinValue;
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
            IsConnected = false;
            IsValidVersion = true;
            obs = new OBSWebsocket();

            obs.Connected += Obs_Connected;
            obs.Disconnected += Obs_Disconnected;
            obs.StreamStatus += Obs_StreamStatus;
            obs.SceneChanged += Obs_SceneChanged;
            obs.ReplayBufferStateChanged += Obs_ReplayBufferStateChanged;
            obs.PreviewSceneChanged += Obs_PreviewSceneChanged;


            ServerManager.Instance.TokensChanged += Instance_TokensChanged;

            tmrCheckStatus.Interval = 5000;
            tmrCheckStatus.Elapsed += TmrCheckStatus_Elapsed;
            tmrCheckStatus.Start();

            InstantReplyStatus = OutputState.Stopped;
            Connect();
            InstantReplayWatcher.Instance.Initialize();

        }

        #endregion

        #region Public Methods

        public event EventHandler ObsConnectionChanged;
        public event EventHandler<Exception> ObsConnectionFailed;
        public event EventHandler<StreamStatusEventArgs> StreamStatusChanged;
        public event EventHandler<SceneChangedEventArgs> SceneChanged;
        public event EventHandler<OutputState> ReplayBufferStateChanged;

        public bool IsConnected { get; private set; }

        public bool IsValidVersion { get; private set; }

        public string CurrentSceneName { get; private set; }

        public string CurrentPreviewSceneName { get; private set; }

        public string PreviousSceneName { get; private set; }

        public string NextSceneName { get; private set; }

        public OutputState InstantReplyStatus { get; private set; }

        public bool IsStreaming { get; private set; }

        public bool IsRecording { get; private set; }

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

                    if (!autoConnect) // Don't spam log
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Attempting to connect");
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
                    obs.WSTimeout = new TimeSpan(0, 0, 3);
                    obs.Connect(String.Format(CONNECTION_STRING, serverInfo.Ip, serverInfo.Port), serverInfo.Password);
                }
                catch (AuthFailureException afe)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Invalid password, could not connect");
                    ServerManager.Instance.InitTokens(null, null, null, DateTime.Now);
                    ObsConnectionFailed?.Invoke(this, afe);
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
                    obs.SetCurrentScene(sceneName);
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
                        InstantReplyStatus = OutputState.Started;
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
                    obs.StartRecording();
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
                    obs.StopRecording();
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
                    obs.PauseRecording();
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
                    obs.ResumeRecording();
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
                    obs.StartStreaming();
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
                    obs.StopStreaming();
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
                    obs.EnableStudioMode();
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
                    obs.DisableStudioMode();
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
                    obs.ToggleStudioMode();
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
                    return obs.GetStudioModeStatus();
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

        public Task<bool> ModifyBrowserSource(string urlOrFile, bool localFile, string sourceName, int delayReplaySeconds, int hideReplaySeconds, bool muteSound)
        {
            return Task.Run(() =>
            {
                if (String.IsNullOrEmpty(sourceName))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyBrowserSource failed. Missing source name");
                    return false;
                }

                if (IsConnected)
                {
                    try
                    {
                        Thread.Sleep(delayReplaySeconds * 1000);
                        obs.SetMute(sourceName, muteSound);
                        obs.SetSourceRender(sourceName, false);
                        var sourceSettings = obs.GetBrowserSourceProperties(sourceName);
                        sourceSettings.URL = urlOrFile;
                        sourceSettings.IsLocalFile = localFile;
                        obs.SetBrowserSourceProperties(sourceName, sourceSettings);
                        Thread.Sleep(200);
                        obs.SetSourceRender(sourceName, true);

                        if (hideReplaySeconds > 0)
                        {
                            Task.Run(() =>
                            {
                                Thread.Sleep(hideReplaySeconds * 1000);
                                obs.SetSourceRender(sourceName, false);
                                Logger.Instance.LogMessage(TracingLevel.INFO, $"ModifyBrowserSource AutoHid source {sourceName} after {hideReplaySeconds} seconds");
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

        public Task<bool> ModifyImageSource(string sourceName, string fileName, int autoHideSeconds)
        {
            return Task.Run(() =>
            {
                if (String.IsNullOrEmpty(sourceName))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyImageSource failed. Missing source name");
                    return false;
                }

                if (IsConnected)
                {
                    try
                    {
                        obs.SetSourceRender(sourceName, false);
                        SourceSettings sourceSettings = obs.GetSourceSettings(sourceName);
                        if (sourceSettings == null)
                        {
                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyImageSource: GetSourceSettings return null for source {sourceName}");
                            return false;
                        }

                        if (sourceSettings.SourceType != "image_source")
                        {
                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyImageSource: Source {sourceName} is not an image source: {sourceSettings.SourceType}");
                            return false;
                        }

                        sourceSettings.Settings["file"] = fileName;
                        obs.SetSourceSettings(sourceName, sourceSettings.Settings);
                        Thread.Sleep(200);
                        obs.SetSourceRender(sourceName, true);

                        if (autoHideSeconds > 0)
                        {
                            Task.Run(() =>
                            {
                                Thread.Sleep(autoHideSeconds * 1000);
                                obs.SetSourceRender(sourceName, false);
                                Logger.Instance.LogMessage(TracingLevel.INFO, $"ModifyImageSource AutoHid source {sourceName} after {autoHideSeconds} seconds");
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

        public Task<bool> PlayInstantReplay(SourcePropertyVideoPlayer settings)
        {
            return AnimationManager.Instance.HandleMediaPlayer(obs, settings);
        }

        public VolumeInfo GetSourceVolume(string sourceName)
        {
            if (IsConnected)
            {
                try
                {
                    return obs.GetVolume(sourceName, true);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetSourceVolume Exception Source:{sourceName}: {ex}");
                }
            }
            return null;
        }

        public bool SetSourceVolume(string sourceName, float volume)
        {
            if (IsConnected)
            {
                try
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting volume for source {sourceName} to {volume}");
                    obs.SetVolume(sourceName, volume, true);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetSourceVolume Exception Source:{sourceName}: {ex}");
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
                    obs.SetPreviewScene(sceneName);
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
                    obs.SetCurrentScene(sceneName);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetScene Exception: {ex}");
                }
            }
            return false;
        }

        public SourceScreenshotResponse GetSourceSnapshot(string sourceName)
        {
            if (IsConnected)
            {
                try
                {
                    return obs.TakeSourceScreenshot(sourceName, "png", null, 144);
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
                    var sourceInfo = obs.GetSourceSettings(sourceName);
                    sourceInfo.Settings["window"] = windowInfo;
                    obs.SetSourceSettings(sourceName, sourceInfo.Settings);
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
                    var item = obs.GetSceneItemProperties(sourceName, sceneName);
                    if (item != null)
                    {
                        // Toggle visibility
                        item.Visible = !item.Visible;
                    }
                    obs.SetSceneItemProperties(item, sceneName);
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
                    var item = obs.GetSceneItemProperties(sourceName, sceneName);
                    if (item == null)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"IsSourceVisible Item is null for Source {sourceName}");
                        return false;
                    }
                    return item.Visible;
                }
            }
            catch (OBSWebsocketDotNet.ErrorResponseException) { }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"IsSourceVisible Exception Source: {sourceName} Scene: {sceneName}: {ex}");
            }
            return false;
        }
        public bool ToggleSourceMute(string sourceName)
        {
            try
            {
                if (IsConnected)
                {
                    obs.ToggleMute(sourceName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ToggleSourceMute Exception Source: {sourceName}: {ex}");
            }
            return false;
        }

        public bool IsSourceMuted(string sourceName)
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetMute(sourceName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"IsSourceMute Exception Source: {sourceName} {ex}");
            }
            return false;
        }

        public void ToggleFilterVisibility(string sourceName, string filterName, bool enableFilter)
        {
            try
            {
                if (IsConnected)
                {
                    obs.SetSourceFilterVisibility(sourceName, filterName, enableFilter);
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
                    var filterInfo = obs.GetSourceFilterInfo(sourceName, filterName);
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
                    return obs.GetSourceFilters(sourceName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetSourceFilters Exception {ex}");
            }
            return null;
        }

        public List<String> GetAllTransitions()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.ListTransitions();
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
        public List<SourceInfo> GetAllSources()
        {
            try
            {
                if (IsConnected)
                {
                    return obs.GetSourcesList();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllSources Exception {ex}");
            }
            return null;
        }

        public List<SceneSourceInfo> GetAllSceneAndSourceNames()
        {
            try
            {
                if (IsConnected)
                {
                    var sources = GetAllSources();
                    var scenes = GetAllScenes();
                    List<SceneSourceInfo> list = new List<SceneSourceInfo>();
                    list.AddRange(sources?.Select(s => new SceneSourceInfo() { Name = s.Name }).ToList());
                    list.AddRange(scenes?.Scenes?.Select(s => new SceneSourceInfo() { Name = s.Name }).ToList());

                    return list.OrderBy(n => n.Name).ToList();
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
                    return obs.ListSceneCollections();
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

        public TransitionSettings GetTransition()
        {
            try
            {
                if (IsConnected)
                {
                    var transition = obs.GetCurrentTransition();
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
                    obs.SetCurrentTransition(transitionName);
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
                    obs.SetTransitionDuration(duration);
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
                    return obs.ListProfiles();
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
                    return obs.GetCurrentProfile();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetProfile Exception: {ex}");
            }
            return null;
        }

        public List<SourcePropertyAnimationConfiguration> GetSourceProperties(string sourceName, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                if (IsConnected)
                {
                    return AnimationManager.Instance.GetSourceProperties(obs, sourceName, out errorMessage);
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
                    return obs.GetRecordingStatus();
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
                    obs.TriggerHotkeyBySequence(hotkey, keyModifier);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"TriggerHotkey Exception: {ex}");
            }
            return false;
        }

        public void SetAudioMonitorType(string sourceName, MonitorTypes monitorType)
        {
            try
            {
                if (IsConnected)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting {sourceName} MonitorType to: {monitorType}");
                    obs.SetAudioMonitorType(sourceName, monitorType.ToStringEx());
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetAudioMonitorType Exception: {ex}");
            }
        }

        public MonitorTypes GetAudioMonitorType(string sourceName)
        {
            try
            {
                if (IsConnected)
                {
                    string monitorType = obs.GetAudioMonitorType(sourceName);
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

        private void Obs_Connected(object sender, EventArgs e)
        {
            IsConnected = true;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Connected to OBS");
            var version = obs.GetVersion();
            if (version != null)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"OBS Version: {version.OBSStudioVersion}");
                Logger.Instance.LogMessage(TracingLevel.INFO, $"WebSocket Version: {version.PluginVersion}");
            }

            IsStreaming = obs.GetStreamingStatus().IsStreaming;
            IsRecording = obs.GetStreamingStatus().IsRecording;
            CurrentSceneName = obs.GetCurrentScene()?.Name;
            Task.Run(() => GetNextSceneName());

            if (IsStudioModeEnabled())
            {
                CurrentPreviewSceneName = obs.GetPreviewScene()?.Name;
            }
            ObsConnectionChanged?.Invoke(this, EventArgs.Empty);
            VerifyValidVersion(version);
        }

        private void Obs_Disconnected(object sender, EventArgs e)
        {
            IsConnected = false;

            if (e is WebSocketSharp.CloseEventArgs cea)
            {
                if (cea.Code == INVALID_WEBSOCKET_VERSION_ERROR_CODE)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Websocket version! {cea.Reason}");
                    ObsConnectionFailed?.Invoke(this, new InvalidOperationException(cea.Reason));
                    return;
                }
            }

            if (!autoConnectRunning) // Don't spam logs
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Disconnected from OBS");
            }
            ObsConnectionChanged?.Invoke(this, EventArgs.Empty);
            if (!disconnectCalled)
            {
                lock (autoConnectLock)
                {
                    if (!autoConnectRunning)
                    {
                        autoConnectRunning = true;
                        Task.Run(() => AutoConnectBackgroundWorker());
                    }
                }

            }
        }

        private void Obs_StreamStatus(OBSWebsocket sender, StreamStatus status)
        {
            lastStreamStatus = DateTime.Now;
            IsStreaming = status.Streaming;
            IsRecording = status.Recording;
            StreamStatusChanged?.Invoke(this, new StreamStatusEventArgs(status));
        }

        private void Obs_SceneChanged(OBSWebsocket sender, string newSceneName)
        {

            // This can be caused by a race condition with the CheckStatus() function
            if (CurrentSceneName == newSceneName && !String.IsNullOrEmpty(previousSceneBackupName))
            {
                PreviousSceneName = previousSceneBackupName;
                previousSceneBackupName = String.Empty;
            }
            else
            {
                PreviousSceneName = CurrentSceneName;
                CurrentSceneName = newSceneName;
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"New scene received from OBS: {newSceneName}");
            SceneChanged?.Invoke(this, new SceneChangedEventArgs(newSceneName));
            Task.Run(() => GetNextSceneName());
        }

        private void Obs_PreviewSceneChanged(OBSWebsocket sender, string newSceneName)
        {
            CurrentPreviewSceneName = newSceneName;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"New preview/studio scene received from OBS: {newSceneName}");
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

        private void Obs_ReplayBufferStateChanged(OBSWebsocket sender, OutputState type)
        {
            InstantReplyStatus = type;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"New replay buffer state received from OBS: {type}");
            ReplayBufferStateChanged?.Invoke(this, type);
        }

        private void GetNextSceneName()
        {
            NextSceneName = string.Empty;
            var scenes = GetAllScenes();
            int idx = scenes.Scenes.FindIndex(x => x.Name == scenes.CurrentScene) + 1;
            if (idx > 0 && idx < scenes.Scenes.Count)
            {
                NextSceneName = scenes.Scenes[idx].Name;
            }
        }

        private void CheckStatus()
        {
            try
            {
                if (obs.IsConnected)
                {
                    string backup = CurrentSceneName;
                    CurrentSceneName = obs.GetCurrentScene()?.Name;

                    // We changed the scene name since last refresh
                    if (CurrentSceneName != backup)
                    {
                        previousSceneBackupName = backup;
                    }

                    if (IsStudioModeEnabled())
                    {
                        CurrentPreviewSceneName = obs.GetPreviewScene()?.Name;
                    }
                    else
                    {
                        CurrentPreviewSceneName = String.Empty;
                    }

                    if ((DateTime.Now - lastStreamStatus).TotalMilliseconds > 5000)
                    {
                        var status = obs.GetStreamingStatus();
                        if (status != null)
                        {
                            IsRecording = status.IsRecording;
                            IsStreaming = status.IsStreaming;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"CheckStatus Exception {ex}");
            }
        }

        private void TmrCheckStatus_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckStatus();
        }
        private void VerifyValidVersion(OBSVersion obsVersion)
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

        #endregion
    }
}
