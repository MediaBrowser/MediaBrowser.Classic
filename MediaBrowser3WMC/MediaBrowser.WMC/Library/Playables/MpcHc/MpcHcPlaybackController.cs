using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Events;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Playables.ExternalPlayer;
using System.IO;

namespace MediaBrowser.Library.Playables.MpcHc
{
    /// <summary>
    /// http://www.autohotkey.com/community/viewtopic.php?f=13&t=78964&hilit=vlc
    /// </summary>
    public class MpcHcPlaybackController : ConfigurableExternalPlaybackController
    {
        private const int ProgressInterval = 1000;

        // All of these hold state about what's being played. They're all reset when playback starts
        private bool _MonitorPlayback = false;
        private string _CurrentPlayState = string.Empty;
        private int _ConsecutiveFailedHttpRequests = 0;
        private bool _HasStartedPlaying = false;

        // This will get the current file position
        private WebClient _StatusRequestClient;
        private Thread _StatusRequestThread;

        private WebClient _CommandClient;

        /// <summary>
        /// Starts monitoring playstate using the player's Http interface
        /// </summary>
        protected override void OnExternalPlayerLaunched(PlayableItem playbackInfo)
        {
            base.OnExternalPlayerLaunched(playbackInfo);

            if (_StatusRequestClient == null)
            {
                _StatusRequestClient = new WebClient();

                // Start up the thread that will perform the monitoring
                _StatusRequestThread = new Thread(MonitorStatus);
                _StatusRequestThread.IsBackground = true;
                _StatusRequestThread.Start();
            }

            _StatusRequestClient.DownloadStringCompleted -= statusRequestCompleted;
            _StatusRequestClient.DownloadStringCompleted += statusRequestCompleted;
            
            _MonitorPlayback = true;
        }

        protected override void ResetPlaybackProperties()
        {
            base.ResetPlaybackProperties();

            // Reset these fields since they hold state
            _MonitorPlayback = false;
            _CurrentPlayState = string.Empty;
            _ConsecutiveFailedHttpRequests = 0;
            _HasStartedPlaying = false;

            if (_StatusRequestClient != null)
            {
                _StatusRequestClient.DownloadStringCompleted -= statusRequestCompleted;
            }
        }

        /// <summary>
        /// Sends out requests to the player's Http interface
        /// </summary>
        private void MonitorStatus()
        {
            Uri statusUri = new Uri(StatusUrl);

            while (!IsDisposing)
            {
                if (_MonitorPlayback)
                {
                    try
                    {
                        _StatusRequestClient.DownloadStringAsync(statusUri);
                    }
                    catch (Exception ex)
                    {
                        _ConsecutiveFailedHttpRequests++;
                        Logger.ReportException("Error connecting to MPC-HC web interface at: " + StatusUrl, ex);

                        // Try to detect MPC hanging after closing
                        // If there are several failed consecutive requests then kill the process
                        // But only do so if we have had at least one request succeed that way we don't kill the process if the user has the web interface disabled.
                        if (_ConsecutiveFailedHttpRequests > 5 && !string.IsNullOrEmpty(_CurrentPlayState))
                        {
                            KillPlayer();
                        }
                    }
                }

                Thread.Sleep(ProgressInterval); 
            }
        }

        void statusRequestCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            // If playback just finished, or if there was some type of error, skip it
            if (!_MonitorPlayback || e.Cancelled || e.Error != null || string.IsNullOrEmpty(e.Result))
            {
                return;
            }

            _ConsecutiveFailedHttpRequests = 0;

            string result = e.Result;

            // Logger.ReportVerbose("Response received from MPC-HC web interface: " + result);

            try
            {
                // Sample result
                // OnStatus('test.avi', 'Playing', 5292, '00:00:05', 1203090, '00:20:03', 0, 100, 'C:\test.avi')
                // 5292 = position in ms
                // 00:00:05 = position
                // 1203090 = duration in ms
                // 00:20:03 = duration

                char quoteChar = result.IndexOf(", \"") == -1 ? '\'' : '\"';

                // Strip off the leading "OnStatus(" and the trailing ")"
                result = result.Substring(result.IndexOf(quoteChar));
                result = result.Substring(0, result.LastIndexOf(quoteChar));

                // Strip off the filename at the beginning
                result = result.Substring(result.IndexOf(string.Format("{0}, {0}", quoteChar)) + 3);

                // Find the last index of ", '" so that we can extract and then strip off the file path at the end.
                int lastIndexOfSeparator = result.LastIndexOf(", " + quoteChar);

                // Get the current playing file path
                string currentPlayingFile = result.Substring(lastIndexOfSeparator + 2).Trim(quoteChar);

                // Strip off the current playing file path
                result = result.Substring(0, lastIndexOfSeparator);

                IEnumerable<string> values = result.Split(',').Select(v => v.Trim().Trim(quoteChar));

                long currentPositionTicks = TimeSpan.FromMilliseconds(double.Parse(values.ElementAt(1))).Ticks;
                long currentDurationTicks = TimeSpan.FromMilliseconds(double.Parse(values.ElementAt(3))).Ticks;

                string playstate = values.ElementAt(0).ToLower();
                _CurrentPlayState = playstate;

                if (playstate == "stopped")
                {
                    if (_HasStartedPlaying)
                    {
                        ClosePlayer();
                    }
                }
                else
                {
                    if (playstate == "playing")
                    {
                        _HasStartedPlaying = true;
                    }

                    if (_HasStartedPlaying)
                    {
                        OnProgress(GetPlaybackState(currentPositionTicks, currentDurationTicks, currentPlayingFile));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ReportException("Unrecognized response in MPC-HC web interface.", ex);
            }

        }

        protected override void OnPlaybackFinished(PlaybackStateEventArgs args)
        {
            _MonitorPlayback = false;

            base.OnPlaybackFinished(args);
        }

        /// <summary>
        /// Takes a Media object and returns the list of files that will be sent to the player
        /// </summary>
        internal override IEnumerable<string> GetPlayableFiles(Media media)
        {
            return base.GetPlayableFiles(media).Select(i => {

                string path = i;

                Video video = media as Video;

                if (video != null & video.MediaType == MediaType.DVD)
                {
                    // Point directly to the video_ts path
                    // Otherwise mpc will play any other media files that might exist in the dvd top folder (e.g. video backdrops).
                    string videoTsPath = Path.Combine(path, "video_ts");

                    if (Directory.Exists(videoTsPath))
                    {
                        path = videoTsPath;
                    }
                }

                return FormatPath(path);
            
            });
        }

        /// <summary>
        /// Formats a path to send to the player
        /// </summary>
        private string FormatPath(string path)
        {
            if (path.EndsWith(":\\"))
            {
                path = path.TrimEnd('\\');
            }

            return path;
        }

        /// <summary>
        /// When playback is based purely on files, this will take the files that were supplied to the PlayableItem,
        /// and create the actual paths that will be sent to the player
        /// </summary>
        internal override IEnumerable<string> GetPlayableFiles(IEnumerable<string> files)
        {
            return base.GetPlayableFiles(files).Select(i => FormatPath(i));
        }

        /// <summary>
        /// Gets arguments to be passed to the command line.
        /// </summary>
        protected override List<string> GetCommandArgumentsList(PlayableItem playInfo)
        {
            List<string> args = new List<string>();

            args.Add("{0}");
            args.Add("/play");
            args.Add("/close");
            args.Add("/fullscreen");

            // Be explicit about start time, to avoid any possible player auto-resume settings
            double startTimeInMs = new TimeSpan(playInfo.StartPositionTicks).TotalMilliseconds;

            args.Add("/start " + startTimeInMs);

            return args;
        }

        /// <summary>
        /// Constructs a PlaybackStateEventArgs based on current playback properties
        /// </summary>
        private PlaybackStateEventArgs GetPlaybackState(long positionTicks, long durationTicks, string currentFilename)
        {
            PlaybackStateEventArgs args = new PlaybackStateEventArgs() { Item = GetCurrentPlayableItem() };

            args.DurationFromPlayer = durationTicks;
            args.Position = positionTicks;

            if (args.Item != null)
            {
                if (args.Item.HasMediaItems)
                {
                    int currentFileIndex;
                    args.CurrentMediaIndex = GetCurrentPlayingMediaIndex(args.Item, currentFilename, out currentFileIndex);
                    args.CurrentFileIndex = currentFileIndex;
                }
                else
                {
                    args.CurrentFileIndex = GetCurrentPlayingFileIndex(args.Item, currentFilename);
                }
            }

            return args;
        }

        private int GetCurrentPlayingFileIndex(PlayableItem playable, string currentFilename)
        {
            return GetIndexOfFile(playable.FilesFormattedForPlayer, currentFilename);
        }

        private int GetCurrentPlayingMediaIndex(PlayableItem playable, string currentFilename, out int currentPlayingFileIndex)
        {
            currentPlayingFileIndex = -1;

            int numMediaItems = playable.MediaItems.Count();
            int totalFileCount = 0;

            for (int i = 0; i < numMediaItems; i++)
            {
                IEnumerable<string> mediaFiles = GetPlayableFiles(playable.MediaItems.ElementAt(i));

                int fileIndex = GetIndexOfFile(mediaFiles, currentFilename);

                if (fileIndex != -1)
                {
                    currentPlayingFileIndex = totalFileCount + fileIndex;
                    return i;
                }

                totalFileCount += mediaFiles.Count();
            }

            return -1;
        }

        private int GetIndexOfFile(IEnumerable<string> files, string file)
        {
            int numFiles = files.Count();

            for (int i = 0; i < numFiles; i++)
            {
                if (file.StartsWith(files.ElementAt(i).ToLower()))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets the server name that the http interface will be running on
        /// </summary>
        private string HttpServer
        {
            get
            {
                return "localhost";
            }
        }

        /// <summary>
        /// Gets the port that the web interface will be running on
        /// </summary>
        public static string HttpPort
        {
            get
            {
                return "13579";
            }
        }

        /// <summary>
        /// Gets the url of that will be called to for status
        /// </summary>
        private string StatusUrl
        {
            get
            {
                return "http://" + HttpServer + ":" + HttpPort + "/status.html";
            }
        }

        /// <summary>
        /// Gets the url of that will be called to send commands
        /// </summary>
        private string CommandUrl
        {
            get
            {
                return "http://" + HttpServer + ":" + HttpPort + "/command.html";
            }
        }

        protected override void PauseInternal()
        {
            SendCommandToPlayer("888", new Dictionary<string, string>());
        }

        protected override void UnPauseInternal()
        {
            SendCommandToPlayer("887", new Dictionary<string, string>());
        }

        protected override void StopInternal()
        {
            SendCommandToPlayer("890", new Dictionary<string, string>());
        }

        protected override void ClosePlayer()
        {
            SendCommandToPlayer("816", new Dictionary<string, string>());
        }

        protected override void SeekInternal(long position)
        {
            Dictionary<string, string> additionalParams = new Dictionary<string, string>();

            TimeSpan time = TimeSpan.FromTicks(position);

            string timeString = time.Hours + ":" + time.Minutes + ":" + time.Seconds;

            additionalParams.Add("position", timeString);

            SendCommandToPlayer("-1", additionalParams);
        }

        /// <summary>
        /// Sends a command to MPC using the HTTP interface
        /// http://www.autohotkey.net/~specter333/MPC/HTTP%20Commands.txt
        /// </summary>
        private void SendCommandToPlayer(string commandNumber, Dictionary<string, string> additionalParams)
        {
            string url = CommandUrl + "?wm_command=" + commandNumber;

            foreach (string name in additionalParams.Keys)
            {
                url += "&" + name + "=" + additionalParams[name];
            }

            if (_CommandClient == null)
            {
                _CommandClient = new WebClient();
                _CommandClient.DownloadStringCompleted += _CommandClient_DownloadStringCompleted;
            }

            Logger.ReportVerbose("Sending command to MPC: " + url);

            try
            {
                _CommandClient.DownloadStringAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error connecting to MPC command url", ex);
            }
        }

        void _CommandClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            
        }

        public override bool IsPaused
        {
            get
            {
                return _CurrentPlayState == "paused";
            }
        }

        public override bool CanPause
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }
    }
}
