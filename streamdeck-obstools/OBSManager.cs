using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using OBSWebsocketDotNet;
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

        private static OBSManager instance = null;
        private static readonly object objLock = new object();
        private readonly OBSWebsocket obs;

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
            obs = new OBSWebsocket();

            obs.Connected += Obs_Connected;
            obs.Disconnected += Obs_Disconnected;
            obs.StreamStatus += Obs_StreamStatus;
            obs.SceneChanged += Obs_SceneChanged;
            obs.ReplayBufferStateChanged += Obs_ReplayBufferStateChanged;
            ServerManager.Instance.TokensChanged += Instance_TokensChanged;

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

        public string CurrentSceneName { get; private set; }

        public string PreviousSceneName { get; private set; }

        public OutputState InstantReplyStatus { get; private set; }

        public bool IsReplayBuffer { get; private set; }

        public bool IsStreaming { get; private set; }

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

        public Task<bool> PlayInstantReplay(string fileName, string sourceName, int hideReplaySeconds, bool muteSound)
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
                        var sourceSettings = obs.GetMediaSourceSettings(sourceName);
                        sourceSettings.Media.IsLocalFile = true;
                        sourceSettings.Media.LocalFile = fileName;
                        obs.SetMediaSourceSettings(sourceSettings);
                        obs.SetMute(sourceName, muteSound);
                        obs.SetSourceRender(sourceName, true);

                        if (hideReplaySeconds > 0)
                        {
                            Task.Run(() =>
                            {
                                Thread.Sleep(hideReplaySeconds * 1000);
                                obs.SetSourceRender(sourceName, false);
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
            Logger.Instance.LogMessage(TracingLevel.INFO, $"OBS Version: {obs.GetVersion().OBSStudioVersion}");
            IsStreaming = obs.GetStreamingStatus().IsStreaming;
            ObsConnectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Obs_Disconnected(object sender, EventArgs e)
        {
            IsConnected = false;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Disconnected from OBS");
            ObsConnectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Obs_StreamStatus(OBSWebsocket sender, StreamStatus status)
        {
            IsStreaming = status.Streaming;
            IsReplayBuffer = status.ReplayBufferActive;
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

        #endregion


    }
}
