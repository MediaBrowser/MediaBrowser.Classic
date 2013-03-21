using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Events;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Playables.ExternalPlayer;

namespace MediaBrowser.Library.Playables.VLC2
{
    public class VLC2PlaybackController : ConfigurableExternalPlaybackController
    {
        private const int ProgressInterval = 1000;

        // All of these hold state about what's being played. They're all reset when playback starts
        private long _CurrentPositionTicks = 0;
        private bool _MonitorPlayback = false;
        private bool _HasStartedPlaying = false;
        private string _CurrentPlayState = string.Empty;

        // This will get the current file position
        private WebClient _StatusRequestClient;
        private Thread _StatusRequestThread;

        // This will get the current file index
        private WebClient _PlaylistRequestClient;

        /// <summary>
        /// Gets arguments to be passed to the command line.
        /// http://wiki.videolan.org/VLC_command-line_help
        /// </summary>
        protected override List<string> GetCommandArgumentsList(PlayableItem playInfo)
        {
            List<string> args = new List<string>();

            args.Add("{0}");

            // Be explicit about start time, to avoid any possible player auto-resume settings
            double startTimeInSeconds = new TimeSpan(playInfo.StartPositionTicks).TotalSeconds;

            args.Add("--start-time=" + startTimeInSeconds);

            // Play in fullscreen
            args.Add("--fullscreen");
            // Keep the window on top of others
            args.Add("--video-on-top");
            // Start a new instance
            args.Add("--no-one-instance");
            // Close the player when playback finishes
            args.Add("--play-and-exit");
            // Disable system screen saver during playback
            args.Add("--disable-screensaver");

            // Keep the ui minimal
            args.Add("--qt-minimal-view");
            args.Add("--no-video-deco");
            args.Add("--no-playlist-tree");

            // OSD marquee font
            args.Add("--freetype-outline-thickness=6");

            // Startup the Http interface so we can send out requests to monitor playstate
            args.Add("--extraintf=http");
            args.Add("--http-host=" + HttpServer);
            args.Add("--http-port=" + HttpPort);

            // Disable the new version notification for this session
            args.Add("--no-qt-updates-notif");

            // Map the stop button on the remote to close the player
            args.Add("--global-key-quit=\"Media Stop\"");

            args.Add("--global-key-play=\"Media Play\"");
            args.Add("--global-key-pause=\"Media Pause\"");
            args.Add("--global-key-play-pause=\"Media Play Pause\"");

            args.Add("--global-key-vol-down=\"Volume Down\"");
            args.Add("--global-key-vol-up=\"Volume Up\"");
            args.Add("--global-key-vol-mute=\"Mute\"");

            args.Add("--key-nav-up=\"Up\"");
            args.Add("--key-nav-down=\"Down\"");
            args.Add("--key-nav-left=\"Left\"");
            args.Add("--key-nav-right=\"Right\"");
            args.Add("--key-nav-activate=\"Enter\"");

            args.Add("--global-key-jump-long=\"Media Prev Track\"");
            args.Add("--global-key-jump+long=\"Media Next Track\"");

            return args;
        }

        /// <summary>
        /// Starts monitoring playstate using the VLC Http interface
        /// </summary>
        protected override void OnExternalPlayerLaunched(PlayableItem playbackInfo)
        {
            base.OnExternalPlayerLaunched(playbackInfo);

            if (_StatusRequestClient == null)
            {
                _StatusRequestClient = new WebClient();
                _PlaylistRequestClient = new WebClient();

                // Start up the thread that will perform the monitoring
                _StatusRequestThread = new Thread(MonitorStatus);
                _StatusRequestThread.IsBackground = true;
                _StatusRequestThread.Start();
            }

            _PlaylistRequestClient.DownloadStringCompleted -= playlistRequestCompleted;
            _StatusRequestClient.DownloadStringCompleted -= statusRequestCompleted;

            _PlaylistRequestClient.DownloadStringCompleted += playlistRequestCompleted;
            _StatusRequestClient.DownloadStringCompleted += statusRequestCompleted;

            _MonitorPlayback = true;
        }

        protected override void ResetPlaybackProperties()
        {
            base.ResetPlaybackProperties();

            // Reset these fields since they hold state
            _CurrentPositionTicks = 0;
            _HasStartedPlaying = false;
            _MonitorPlayback = false;
            _CurrentPlayState = string.Empty;

            // Cleanup events
            if (_PlaylistRequestClient != null)
            {
                _PlaylistRequestClient.DownloadStringCompleted -= playlistRequestCompleted;
            }

            if (_StatusRequestClient != null)
            {
                _StatusRequestClient.DownloadStringCompleted -= statusRequestCompleted;
            }
        }

        /// <summary>
        /// Sends out requests to VLC's Http interface
        /// </summary>
        private void MonitorStatus()
        {
            Uri statusUri = new Uri(StatusUrl);
            Uri playlistUri = new Uri(VlcPlaylistXmlUrl);

            while (!IsDisposing)
            {
                if (_MonitorPlayback)
                {
                    SendStatusRequest(statusUri);

                    try
                    {
                        _PlaylistRequestClient.DownloadStringAsync(playlistUri);
                    }
                    catch (Exception ex)
                    {
                        Logger.ReportException("Error connecting to VLC playlist url", ex);
                    }
                }

                Thread.Sleep(ProgressInterval);
            }
        }

        private void SendStatusRequest(Uri uri)
        {
            try
            {
                _StatusRequestClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error connecting to VLC status url", ex);
            }
        }

        void statusRequestCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            // If playback just finished, or if there was some type of error, skip it
            if (!_MonitorPlayback || e.Cancelled || e.Error != null || string.IsNullOrEmpty(e.Result))
            {
                return;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(e.Result);
            XmlElement docElement = doc.DocumentElement;

            XmlNode fileNameNode = docElement.SelectSingleNode("information/category[@name='meta']/info[@name='filename']");

            string playstate = docElement.SafeGetString("state", string.Empty).ToLower();

            // Check the filename node for null first, because if that's the case then it means nothing's currently playing.
            // This could happen after playback has stopped, but before the player has exited
            if (fileNameNode != null)
            {
                _CurrentPositionTicks = TimeSpan.FromSeconds(int.Parse(docElement.SelectSingleNode("time").InnerText)).Ticks;
            }

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
                _HasStartedPlaying = true;
            }
        }

        void playlistRequestCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            // If playback just finished, or if there was some type of error, skip it
            if (!_MonitorPlayback || e.Cancelled || e.Error != null || string.IsNullOrEmpty(e.Result))
            {
                return;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(e.Result);
            XmlElement docElement = doc.DocumentElement;

            XmlNode leafNode = docElement.SelectSingleNode("node/leaf[@current='current']");

            if (leafNode != null)
            {
                long currentDurationTicks = TimeSpan.FromSeconds(int.Parse(leafNode.Attributes["duration"].Value)).Ticks;

                int currrentPlayingFileIndex = IndexOfNode(leafNode.ParentNode.ChildNodes, leafNode);

                OnProgress(GetPlaybackState(_CurrentPositionTicks, currentDurationTicks, currrentPlayingFileIndex));
            }
        }

        protected override void OnPlaybackFinished(PlaybackStateEventArgs args)
        {
            _MonitorPlayback = false;

            base.OnPlaybackFinished(args);
        }

        private int IndexOfNode(XmlNodeList nodes, XmlNode node)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == node)
                {
                    return i;
                }
            }

            return -1;
        }

        private PlaybackStateEventArgs GetPlaybackState(long positionTicks, long durationTicks, int currentFileIndex)
        {
            PlaybackStateEventArgs state = new PlaybackStateEventArgs() { Item = GetCurrentPlayableItem() };

            state.DurationFromPlayer = durationTicks;
            state.Position = positionTicks;

            state.CurrentFileIndex = currentFileIndex;

            return state;
        }

        /// <summary>
        /// Gets the server name that VLC's Http interface will be running on
        /// </summary>
        private string HttpServer
        {
            get
            {
                return "localhost";
            }
        }

        /// <summary>
        /// Gets the port that VLC's Http interface will be running on
        /// </summary>
        private string HttpPort
        {
            get
            {
                return "8088";
            }
        }

        /// <summary>
        /// Gets the url of VLC's xml status file
        /// </summary>
        private string StatusUrl
        {
            get
            {
                return "http://" + HttpServer + ":" + HttpPort + "/requests/status.xml";
            }
        }

        /// <summary>
        /// Gets the url of VLC's xml status file
        /// </summary>
        private string VlcPlaylistXmlUrl
        {
            get
            {
                return "http://" + HttpServer + ":" + HttpPort + "/requests/playlist.xml";
            }
        }

        /// <summary>
        /// Takes a Media object and returns the list of files that will be sent to the player
        /// </summary>
        internal override IEnumerable<string> GetPlayableFiles(Media media)
        {
            IEnumerable<string> files = base.GetPlayableFiles(media);

            Video video = media as Video;

            if (video != null)
            {
                files = files.Select(i => FormatPath(i, video.MediaType));
            }

            return files;
        }

        /// <summary>
        /// Formats a path to send to the player
        /// </summary>
        private string FormatPath(string path, Library.MediaType mediaType)
        {
            if (path.EndsWith(":\\"))
            {
                path = path.TrimEnd('\\');
            }

            if (mediaType == MediaType.DVD)
            {
                path = "dvd:///" + path;
            }

            return path;
        }

        /// <summary>
        /// When playback is based purely on files, this will take the files that were supplied to the PlayableItem,
        /// and create the actual paths that will be sent to the player
        /// </summary>
        internal override IEnumerable<string> GetPlayableFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                MediaBrowser.Library.MediaType mediaType = MediaBrowser.Library.MediaTypeResolver.DetermineType(file);

                yield return FormatPath(file, mediaType);
            }
        }

        protected override void PauseInternal()
        {
            SendStatusRequest(new Uri(StatusUrl + "?command=pl_pause"));
        }

        protected override void UnPauseInternal()
        {
            // It uses the same command by toggling
            PauseInternal();
        }

        protected override void StopInternal()
        {
            SendStatusRequest(new Uri(StatusUrl + "?command=pl_stop"));
        }

        protected override void SeekInternal(long position)
        {
            TimeSpan time = TimeSpan.FromTicks(position);

            SendStatusRequest(new Uri(StatusUrl + "?command=seek&val=" + Convert.ToInt32(time.TotalSeconds)));
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
