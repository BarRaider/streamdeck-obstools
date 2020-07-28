using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools
{
    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: CyberlightGames x3
    // 5001 Bits: nubby_ninja
    // 1 Bits: inclaved
    //---------------------------------------------------
    public class InstantReplayWatcher
    {
        #region Private Members

        private static InstantReplayWatcher instance = null;
        private static readonly object objLock = new object();

        private bool autoReplay = false;
        private bool muteSound = false;
        private string replayDirectory = null;
        private int hideReplaySeconds = 0;
        private int delayReplaySeconds = 0;
        private int playSpeed = 100;
        private string sourceName = String.Empty;
        private GlobalSettings global;
        readonly FileSystemWatcher watcher;

        #endregion

        #region Constructors

        public static InstantReplayWatcher Instance
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
                        instance = new InstantReplayWatcher();
                    }
                    return instance;
                }
            }
        }

        public void Initialize()
        {
        }

        private InstantReplayWatcher()
        {
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += Instance_OnReceivedGlobalSettings;
            GlobalSettingsManager.Instance.RequestGlobalSettings();

            watcher = new FileSystemWatcher
            {
                // Watch for changes in LastAccess and LastWrite times, and
                // the renaming of files or directories.
                NotifyFilter = NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.CreationTime
            };


            // Add event handlers.
            watcher.Created += FileCreated;
        }

        private void Instance_OnReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload payload)
        {
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                global = payload.Settings.ToObject<GlobalSettings>();
                autoReplay = global.AutoReplay;
                replayDirectory = global.ReplayDirectory;
                hideReplaySeconds = global.HideReplaySeconds;
                delayReplaySeconds = global.DelayReplaySeconds;
                sourceName = global.SourceName;
                muteSound = global.MuteSound;
                playSpeed = global.PlaySpeed;
            }

            InitializeDirectoryWatcher();
        }

        private void InitializeDirectoryWatcher()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "InitializeDirectoryWatcher Called");

            // Stop watching.
            watcher.EnableRaisingEvents = false;

            // Valid directory
            if (autoReplay && Directory.Exists(replayDirectory))
            {
                watcher.Path = replayDirectory;
                watcher.EnableRaisingEvents = true;
                Logger.Instance.LogMessage(TracingLevel.INFO, $"InitializeDirectoryWatcher watching over directory {replayDirectory}");
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"InitializeDirectoryWatcher Disabled. AutoReplay: {autoReplay} Directory: {replayDirectory}");
            }
        }

        private void FileCreated(object sender, FileSystemEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"InitializeDirectoryWatcher new file created: {e.Name}");



            OBSManager.Instance.PlayInstantReplay(new OTI.Shared.SourcePropertyVideoPlayer()
            {
                VideoFileName = e.FullPath,
                SourceName = sourceName,
                DelayPlayStartSeconds = delayReplaySeconds,
                HideReplaySeconds = hideReplaySeconds,
                MuteSound = muteSound,
                PlaySpeedPercent = playSpeed
            });
        }

        #endregion
    }
}
