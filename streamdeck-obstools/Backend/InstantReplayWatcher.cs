using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Backend
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
            }

            InitializeDirectoryWatcher();
        }

        private void InitializeDirectoryWatcher()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "InitializeDirectoryWatcher Called");
            try
            {
                if (global == null || global.InstantReplaySettings == null )
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "InitializeDirectoryWatcher called but settings are null!");
                    watcher.EnableRaisingEvents = false;
                    return;
                }

                var settings = global.InstantReplaySettings;

                if (watcher.Path == settings.ReplayDirectory)
                {
                    if (watcher.EnableRaisingEvents != settings.AutoReplay)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"InitializeDirectoryWatcher mode changed to: {settings.AutoReplay}");
                    }
                    watcher.EnableRaisingEvents = settings.AutoReplay;
                    return;
                }

                // Stop watching.
                watcher.EnableRaisingEvents = false;

                // Valid directory
                if (settings.AutoReplay && Directory.Exists(settings.ReplayDirectory))
                {
                    watcher.Path = settings.ReplayDirectory;
                    watcher.EnableRaisingEvents = true;
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"InitializeDirectoryWatcher watching over directory {settings.ReplayDirectory}");
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"InitializeDirectoryWatcher Disabled. AutoReplay: {settings.AutoReplay} Directory: {settings.ReplayDirectory}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"InitializeDirectoryWatcher Exception: {ex}");
            }
        }

        private void FileCreated(object sender, FileSystemEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"InstantReplayWatcher: Starting Instant Replay for: {e.Name} ");
            if (global?.InstantReplaySettings == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "InstantReplayWatcher: FileCreated but settings are null!");
                return;
            }

            OBSManager.Instance.PlayInstantReplay(new OTI.Shared.SourcePropertyVideoPlayer()
            {
                VideoFileName = e.FullPath,
                SceneName = global.InstantReplaySettings.SceneName,
                InputName = global.InstantReplaySettings.InputName,
                DelayPlayStartSeconds = global.InstantReplaySettings.DelayReplaySeconds,
                HideReplaySeconds = global.InstantReplaySettings.HideReplaySeconds,
                MuteSound = global.InstantReplaySettings.MuteSound,
                PlaySpeedPercent = global.InstantReplaySettings.PlaySpeed
            }, global.InstantReplaySettings.AutoSwitch);
        }

        #endregion
    }
}
