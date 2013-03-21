using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using MediaBrowser.Library.Events;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Playables.ExternalPlayer;
using MediaBrowser.LibraryManagement;

namespace MediaBrowser.Library.Playables.TMT5
{
    public class TMT5PlaybackController : ConfigurableExternalPlaybackController
    {
        // All of these hold state about what's being played. They're all reset when playback starts
        private bool _HasStartedPlaying = false;
        private FileSystemWatcher _StatusFileWatcher;
        private string _CurrentPlayState = string.Empty;
        protected bool _HasStopped = false;

        // Protect against really aggressive event handling
        private DateTime _LastFileSystemUpdate = DateTime.Now;

        /// <summary>
        /// Gets arguments to be passed to the command line.
        /// </summary>
        protected override List<string> GetCommandArgumentsList(PlayableItem playbackInfo)
        {
            List<string> args = new List<string>();

            args.Add("{0}");

            return args;
        }

        protected override void ResetPlaybackProperties()
        {
            base.ResetPlaybackProperties();

            _HasStartedPlaying = false;
            _CurrentPlayState = string.Empty;
            _HasStopped = false;

            DisposeFileSystemWatcher();
        }

        protected override void OnExternalPlayerLaunched(PlayableItem playbackInfo)
        {
            base.OnExternalPlayerLaunched(playbackInfo);

            // If the playstate directory exists, start watching it
            if (Directory.Exists(PlayStateDirectory))
            {
                StartWatchingStatusFile();
            }
        }

        private void StartWatchingStatusFile()
        {
            Logging.Logger.ReportVerbose("Watching TMT folder: " + PlayStateDirectory);
            _StatusFileWatcher = new FileSystemWatcher(PlayStateDirectory, "*.set");

            // Need to include subdirectories since there are subfolders undearneath this with the TMT version #.
            _StatusFileWatcher.IncludeSubdirectories = true;

            _StatusFileWatcher.Changed += _StatusFileWatcher_Changed;
            _StatusFileWatcher.EnableRaisingEvents = true;
        }

        void _StatusFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            NameValueCollection values;

            try
            {
                values = Helper.ParseIniFile(e.FullPath);
            }
            catch (IOException)
            {
                // This can happen if the file is being written to at the exact moment we're trying to access it
                // Unfortunately we kind of have to just eat it
                return;
            }

            string tmtPlayState = values["State"].ToLower();

            _CurrentPlayState = tmtPlayState;

            if (tmtPlayState == "play")
            {
                // Playback just started
                _HasStartedPlaying = true;

                // Protect against really agressive calls
                var diff = (DateTime.Now - _LastFileSystemUpdate).TotalMilliseconds;

                if (diff < 1000 && diff >= 0)
                {
                    return;
                }

                _LastFileSystemUpdate = DateTime.Now;
            }

            // If playback has previously started...
            // First notify the Progress event handler
            // Then check if playback has stopped
            if (_HasStartedPlaying)
            {
                TimeSpan currentDuration = TimeSpan.FromTicks(0);
                TimeSpan currentPosition = TimeSpan.FromTicks(0);

                TimeSpan.TryParse(values["TotalTime"], out currentDuration);
                TimeSpan.TryParse(values["CurTime"], out currentPosition);

                PlaybackStateEventArgs state = GetPlaybackState(currentPosition.Ticks, currentDuration.Ticks);

                OnProgress(state);

                // Playback has stopped
                if (tmtPlayState == "stop")
                {
                    Logger.ReportVerbose(ControllerName + " playstate changed to stopped");

                    if (!_HasStopped)
                    {
                        _HasStopped = true;

                        DisposeFileSystemWatcher();

                        HandleStoppedState(state);
                    }
                }
            }
        }

        protected virtual void HandleStoppedState(PlaybackStateEventArgs args)
        {
            ClosePlayer();
        }

        /// <summary>
        /// Constructs a PlaybackStateEventArgs based on current playback properties
        /// </summary>
        protected PlaybackStateEventArgs GetPlaybackState(long positionTicks, long durationTicks)
        {
            PlaybackStateEventArgs state = new PlaybackStateEventArgs() { Item = GetCurrentPlayableItem() };

            state.DurationFromPlayer = durationTicks;
            state.Position = positionTicks;

            state.CurrentFileIndex = 0;

            return state;
        }

        private void DisposeFileSystemWatcher()
        {
            if (_StatusFileWatcher != null)
            {
                _StatusFileWatcher.EnableRaisingEvents = false;
                _StatusFileWatcher.Changed -= _StatusFileWatcher_Changed;
                _StatusFileWatcher.Dispose();
                _StatusFileWatcher = null;
            }
        }

        /// <summary>
        /// Sends an arbitrary command to the TMT MMC console
        /// </summary>
        protected void SendCommandToMMC(string command)
        {
            string directory = new FileInfo(ExternalPlayerConfiguration.Command).DirectoryName;
            string exe = Path.Combine(directory, "MMCEDT5.exe");

            // Best we can do for now
            ProcessStartInfo processInfo = new ProcessStartInfo(exe, command);
            processInfo.CreateNoWindow = true;

            using (Process process = Process.Start(processInfo))
            {
            }
        }

        private string PlayStateDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArcSoft");
            }
        }

        protected override void PauseInternal()
        {
            SendCommandToMMC("-pause");
        }

        protected override void StopInternal()
        {
            SendCommandToMMC("-stop");
        }

        protected override void UnPauseInternal()
        {
            SendCommandToMMC("-play");
        }

        /// <summary>
        /// Sends a command to the MMC console to close the player.
        /// Do not use this for the WMC add-in because it will close WMC
        /// </summary>
        protected override void ClosePlayer()
        {
            SendCommandToMMC("-close");
        }

        public override bool IsPaused
        {
            get
            {
                return _CurrentPlayState == "pause";
            }
        }

        public override bool CanPause
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }
    }
}
