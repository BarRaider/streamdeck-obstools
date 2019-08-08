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
    public class InstantReplayWatcher
    {
        #region Private Members

        private static InstantReplayWatcher instance = null;
        private static readonly object objLock = new object();

        private bool autoReplay = false;
        private bool muteSound = false;
        private string replayDirectory = null;
        private int hideReplaySeconds = 0;
        private string sourceName = String.Empty;
        private GlobalSettings global;
        FileSystemWatcher watcher;

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

            watcher = new FileSystemWatcher();

            // Watch for changes in LastAccess and LastWrite times, and
            // the renaming of files or directories.
            watcher.NotifyFilter = NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.CreationTime;


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
                sourceName = global.SourceName;
                muteSound = global.MuteSound;
            }

            InitalizeDirectoryWatcher();
        }

        private void InitalizeDirectoryWatcher()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "InitalizeDirectoryWatcher Called");

            // Stop watching.
            watcher.EnableRaisingEvents = false;

            // Valid directory
            if (autoReplay && Directory.Exists(replayDirectory))
            {
                watcher.Path = replayDirectory;
                watcher.EnableRaisingEvents = true;
                Logger.Instance.LogMessage(TracingLevel.INFO, $"InitalizeDirectoryWatcher watching over directory {replayDirectory}");
            }
        }

        private void FileCreated(object sender, FileSystemEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"InitalizeDirectoryWatcher new file created: {e.Name}");

            OBSManager.Instance.PlayInstantReplay(e.FullPath, sourceName, hideReplaySeconds, muteSound);
        }

        #endregion
    }
}
