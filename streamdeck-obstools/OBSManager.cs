using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BarRaider.ObsTools
{
    public class OBSManager
    {
        #region Private Members

        private const string CONNECTION_STRING = "ws://{0}:{1}";
        private const string REPLAY_ALREADY_ACTIVE_ERROR_MESSAGE = "replay buffer already active";
        private readonly Version MINIMUM_SUPPORTED_WEBSOCKET_VERSION = new Version("4.7");

        private static OBSManager instance = null;
        private static readonly object objLock = new object();
        private readonly OBSWebsocket obs;
        private DateTime lastStreamStatus;
        private readonly System.Timers.Timer tmrCheckStatus = new System.Timers.Timer();

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
        public event EventHandler<StreamStatusEventArgs> StreamStatusChanged;
        public event EventHandler<SceneChangedEventArgs> SceneChanged;
        public event EventHandler<OutputState> ReplayBufferStateChanged;

        public bool IsConnected { get; private set; }

        public bool IsValidVersion { get; private set; }

        public string CurrentSceneName { get; private set; }

        public string PreviousSceneName { get; private set; }

        public OutputState InstantReplyStatus { get; private set; }

        public bool IsReplayBufferActive { get; private set; }

        public bool IsStreaming { get; private set; }

        public bool IsRecording { get; private set; }

        public void Connect()
        {
            if (!obs.IsConnected)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Attempting to connect");
                var serverInfo = ServerManager.Instance.ServerInfo;

                if (serverInfo == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Cannot connect, Server info missing");
                    return;
                }

                Task.Run(() =>
                {
                    try
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Attempting to connect to {serverInfo.Ip}:{serverInfo.Port}");
                        obs.WSTimeout = new TimeSpan(0, 0, 10);
                        obs.Connect(String.Format(CONNECTION_STRING, serverInfo.Ip, serverInfo.Port), serverInfo.Password);
                    }
                    catch (AuthFailureException)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"Invalid password, could not connect");
                        ServerManager.Instance.InitTokens(null, null, null, DateTime.Now);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"Connection Exception: {ex}");
                    }
                });
            }
        }

        public void Disconnect()
        {
            if (!obs.IsConnected)
            {
                obs.Disconnect();
            }
        }

        public bool ChangeScene(string sceneName)
        {
            if (obs.IsConnected)
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
            if (obs.IsConnected)
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
            if (obs.IsConnected)
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
            if (obs.IsConnected)
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

        public bool StartStreaming()
        {
            if (obs.IsConnected)
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
            if (obs.IsConnected)
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
            if (obs.IsConnected)
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
            if (obs.IsConnected)
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
            if (obs.IsConnected)
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
            if (obs.IsConnected)
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
            if (obs.IsConnected)
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
                if (obs.IsConnected)
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

                if (obs.IsConnected)
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

        public Task<bool> PlayInstantReplay(string fileName, string sourceName, int delayReplaySeconds, int hideReplaySeconds, bool muteSound)
        {
            return Task.Run(() =>
            {
                if (String.IsNullOrEmpty(sourceName))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"PlayInstantReplay for file {fileName} failed. Missing source name");
                    return false;
                }

                if (!File.Exists(fileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"PlayInstantReplay for file {fileName} failed. File does not exist");
                    return false;
                }

                if (obs.IsConnected)
                {
                    try
                    {
                        Thread.Sleep(delayReplaySeconds * 1000);
                        obs.SetMute(sourceName, muteSound);
                        obs.SetSourceRender(sourceName, false);
                        var sourceSettings = obs.GetMediaSourceSettings(sourceName);
                        sourceSettings.Media.IsLocalFile = true;
                        sourceSettings.Media.LocalFile = fileName;
                        obs.SetMediaSourceSettings(sourceSettings);
                        Thread.Sleep(200);
                        obs.SetSourceRender(sourceName, true);

                        if (hideReplaySeconds > 0)
                        {
                            Task.Run(() =>
                            {
                                Thread.Sleep(hideReplaySeconds * 1000);
                                obs.SetSourceRender(sourceName, false);
                                Logger.Instance.LogMessage(TracingLevel.INFO, $"PlayInstantReplay AutoHid source {sourceName} after {hideReplaySeconds} seconds");
                            });
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"PlayInstantReplay for file {fileName} failed. Exception: {ex}");
                    }
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"PlayInstantReplay for file {fileName} failed. OBS is not connected");
                }
                return false;
            });
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
            ObsConnectionChanged?.Invoke(this, EventArgs.Empty);
            VerifyValidVersion(version);
        }

        private void Obs_Disconnected(object sender, EventArgs e)
        {
            IsConnected = false;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Disconnected from OBS");
            ObsConnectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Obs_StreamStatus(OBSWebsocket sender, StreamStatus status)
        {
            lastStreamStatus = DateTime.Now;
            IsStreaming = status.Streaming;
            IsRecording = status.Recording;
            IsReplayBufferActive = status.ReplayBufferActive;
            StreamStatusChanged?.Invoke(this, new StreamStatusEventArgs(status));
        }

        private void Obs_SceneChanged(OBSWebsocket sender, string newSceneName)
        {
            PreviousSceneName = CurrentSceneName;
            CurrentSceneName = newSceneName;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"New scene received from OBS: {newSceneName}");
            SceneChanged?.Invoke(this, new SceneChangedEventArgs(newSceneName));
        }
        private void Instance_TokensChanged(object sender, ServerInfoEventArgs e)
        {
            if (ServerManager.Instance.ServerInfoExists && !obs.IsConnected)
            {
                Connect();
            }
            else if (!ServerManager.Instance.ServerInfoExists)
            {
                Disconnect();
            }
        }

        private void Obs_ReplayBufferStateChanged(OBSWebsocket sender, OutputState type)
        {
            InstantReplyStatus = type;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"New replay buffer state received from OBS: {type}");
            ReplayBufferStateChanged?.Invoke(this, type);
        }

        private void CheckStatus()
        {
            if (obs.IsConnected && (DateTime.Now - lastStreamStatus).TotalMilliseconds > 5000)
            {
                var status = obs.GetStreamingStatus();
                IsRecording = status.IsRecording;
                IsStreaming = status.IsStreaming;
            }            
        }

        private void TmrCheckStatus_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckStatus();
        }

        public void ToggleFilterVisibility(string sourceName, string filterName, bool enableFilter)
        {
            if (IsConnected)
            {
                obs.SetSourceFilterVisibility(sourceName, filterName, enableFilter);
            }
        }

        public List<String> GetAllTransitions()
        {
            if (IsConnected)
            {
                return obs.ListTransitions();
            }
            return null;
        }

        public void SetTransition(string transitionName)
        {
            if (IsConnected)
            {
                obs.SetCurrentTransition(transitionName);
            }
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
                Logger.Instance.LogMessage(TracingLevel.WARN, $"VerifyValidVersion - obs-websocket version is not up to date: {pluginVersion.ToString()} expected {MINIMUM_SUPPORTED_WEBSOCKET_VERSION.ToString()}");
                Disconnect();
                return;
            }

            IsValidVersion = true;
        }

        #endregion
    }
}
