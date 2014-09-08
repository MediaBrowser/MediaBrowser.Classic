using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MediaBrowser.Code;
using MediaBrowser.Library;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities;
using MediaBrowser.Model.Updates;
using Microsoft.MediaCenter.UI;
using Inset = MediaBrowser.Code.ShadowTypes.Inset;
using Vector3 = MediaBrowser.Code.ShadowTypes.Vector3;

namespace MediaBrowser
{

    [Serializable]
    public class CommonConfigData
    {
        public bool FindServerAutomatically = true;
        public string ServerAddress = "";
        public int ServerPort = 8096;
        public string LastServerMacAddress;

        public bool LogonAutomatically = false;
        public string AutoLogonUserName;
        public string AutoLogonPw;

        public bool WakeServer = true;

        public bool EnableAutoLogoff = false; //enable auto logoff functionality
        public int AutoLogoffTimeOut = 180; //minutes of inactivity for system to auto logoff

        public bool WarnOnStream = true; //show warning if streaming instead of direct playing
        public int LocalMaxBitrate = 30; //max bitrate (Mb/s) when attached locally
        public int RemoteMaxBitrate = 2; //max bitrate (Mb/s) when attached remotely

        public bool DisableMcConflictingOperations = false; // disable some items in player interface that conflict with Media Control
        public bool DisableCustomPlayerForDvd = false; // disable custom player for DVD rips (because menus don't work)

        public string WeatherLocation = "";
        public string WeatherUnit = "f";

        public DateTime LastNagDate = DateTime.MinValue;

        [Group("Updates")]
        [Comment(@"Enable the automatic checking for updates (both MB and plugins).")]
        public bool EnableUpdates = true;
        [Group("Updates")]
        [Comment(@"Enable the automatic checking for updates (both MB and plugins).")]
        public bool EnableSilentUpdates = true;
        [Group("Updates")]
        [Comment(@"The class of updates to check (Dev/Beta/Release).")]
        public PackageVersionClass SystemUpdateClass = PackageVersionClass.Release;
        [Group("Updates")]
        [Comment(@"The class of updates to check (Dev/Beta/Release).")]
        public PackageVersionClass PluginUpdateClass = PackageVersionClass.Beta;
        [Hidden]
        public List<ExternalPlayer> ExternalPlayers = new List<ExternalPlayer>();
        [Dangerous]
        [Group("Advanced")]
        [Comment(@"The directory where MB was installed. Filled in at install time and used to call components.")]
        public string MBInstallDir = "";

        [Group("Playback")]
        [Comment(@"The extensions of file types that the Xbox 360 can play natively (without transcoding).")]
        public string ExtenderNativeTypes = ".dvr-ms,.wmv";
        [Group("Playback")]
        [Comment(@"Instead of directly mounting an ISO allow the 'autoplay' settings in windows to handle it.")]
        public bool UseAutoPlayForIso = false;
        [Group("Playback")]
        [Comment(@"Enable transcoding for 360 extender.")]
        public bool EnableTranscode360 = false;
        [Dangerous]
        [Comment(@"The version is used to determine if this is the first time a particular version has been run")]
        public string MBVersion = "1.0.0.0"; //default value will tell us if it is a brand new install

        [Dangerous]
        [Comment(@"Identifies if this is the very first time MB has been run.  Causes an initial setup routine to be performed.")]
        public bool IsFirstRun = true;

        public bool EnableMouseHook = true;

        public bool AutoValidate = true; //automatically validate and refresh items as we access them

        [Group("Display")]
        [Comment(@"Scale factor to account for monitor over/underscan. Format: 0,0,0.")]
        public Vector3 OverScanScaling = new Vector3() { X = 1, Y = 1, Z = 1 };
        [Group("Display")]
        [Comment(@"Extra space to account for monitor over/underscan. Format: 0,0.")]
        public Inset OverScanPadding = new Inset();
        [Group("Display")]
        [Comment(@"The number of history items to show in the 'breadcrumbs' (how deep in the structure you are) .")]
        public int BreadcrumbCountLimit = 2;

        [Group("Playback")]
        [Comment(@"The location of your ISO mounting program.")]
        public string DaemonToolsLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DAEMON Tools Lite\\daemon.exe");
        [Group("Playback")]
        [Comment(@"The drive letter to use when mounting an ISO.")]
        public string DaemonToolsDrive = "E";
        [Comment(@"Turns on logging for all MB components. Recommended you leave this on as it doesn't slow things down and is very helpful in troubleshooting.")]
        public bool EnableTraceLogging = true;
        public LogSeverity MinLoggingSeverity = LogSeverity.Debug;

        [Comment("The number of days to retain log files.  Files older than this will be deleted periodically")]
        public int LogFileRetentionDays = 30;
        public int CacheFileRetentionDays = 45;

        public DateTime LastFileCleanup = DateTime.MinValue;

        public string UserTileColor = "DarkBlue";
        public string LoginBgColor = "DarkGray";

        public int MaxPrimaryWidth = 350;
        public int MaxThumbWidth = 500;
        public int MaxBannerWidth = 500;
        public int MaxBackgroundWidth = 1920;
        public int MaxLogoWidth = 400;
        public int MaxArtWidth = 400;
        public int MaxDiscWidth = 300;
        public int JpgImageQuality = 90;

        public bool UseCustomStreamingUrl;
        public string CustomStreamingUrl = "http://{0}/mediabrowser/Videos/{1}/stream?Static=True";

        public string StartupParms;

        public int HttpTimeout = 40000;

        public enum ExternalPlayerLaunchType
        {
            CommandLine = 0,

            WMCNavigate = 1
        }

        public class ExternalPlayer
        {
            /// <summary>
            /// Determines if the external player can play multiple files without having to first generate a playlist
            /// </summary>
            public bool SupportsMultiFileCommandArguments { get; set; }

            /// <summary>
            /// Determines if playlist files are supported
            /// </summary>
            public bool SupportsPlaylists { get; set; }

            public ExternalPlayerLaunchType LaunchType { get; set; }
            public string ExternalPlayerName { get; set; }
            public List<MediaType> MediaTypes { get; set; }
            public List<VideoFormat> VideoFormats { get; set; }
            public string Command { get; set; }

            public string Args { get; set; }
            public bool MinimizeMCE { get; set; } //whether or not to minimize MCE when starting external player
            public bool ShowSplashScreen { get; set; } //whether or not to show the MB splash screen
            public bool HideTaskbar { get; set; }

            public ExternalPlayer()
            {
                MediaTypes = new List<MediaType>();

                foreach (MediaType val in Enum.GetValues(typeof(MediaType)))
                {
                    MediaTypes.Add(val);
                }

                VideoFormats = new List<VideoFormat>();

                foreach (VideoFormat val in Enum.GetValues(typeof(VideoFormat)))
                {
                    VideoFormats.Add(val);
                }
            }

            public string CommandFileName
            {
                get
                {
                    return string.IsNullOrEmpty(Command) ? string.Empty : Path.GetFileName(Command);
                }
            }

            public string MediaTypesFriendlyString
            {
                get
                {
                    if (MediaTypes.Count == Enum.GetNames(typeof(MediaType)).Count())
                    {
                        return "All";
                    }

                    return string.Join(",", MediaTypes.Select(i => i.ToString()).ToArray());
                }
            }
        }

        // for our reset routine
        public CommonConfigData ()
	    {
            try
            {
                File.Delete(ApplicationPaths.CommonConfigFile);
            }
            catch (Exception e)
            {
                Logger.ReportException("Unable to delete config file " + ApplicationPaths.CommonConfigFile, e);
            }
            //continue anyway
            this.file = ApplicationPaths.CommonConfigFile;
            this.settings = XmlSettings<CommonConfigData>.Bind(this, file);
	    }


        public CommonConfigData(string file)
        {
            this.file = file;
            this.settings = XmlSettings<CommonConfigData>.Bind(this, file);
        }

        [SkipField]
        string file;

        [SkipField]
        XmlSettings<CommonConfigData> settings;


        public static CommonConfigData FromFile(string file)
        {
            return new CommonConfigData(file);  
        }

        public void Save() {
            this.settings.Write();
            //notify of the change
            MediaBrowser.Library.Threading.Async.Queue("Config notify", () => Kernel.Instance.NotifyConfigChange());
        }

        /// <summary>
        /// Determines if a given external player configuration is configured to play a list of files
        /// </summary>
        public static bool CanPlay(CommonConfigData.ExternalPlayer player, IEnumerable<string> files)
        {
            IEnumerable<MediaType> types = files.Select(f => MediaTypeResolver.DetermineType(f));

            // See if there's a configured player matching the ExternalPlayerType and MediaType. 
            // We're not able to evaluate VideoFormat in this scenario
            // If none is found it will return null
            return CanPlay(player, types, new List<VideoFormat>(), files.Count() > 1);
        }

        /// <summary>
        /// Determines if a given external player configuration is configured to play a list of files
        /// </summary>
        public static bool CanPlay(CommonConfigData.ExternalPlayer player, IEnumerable<Media> mediaList)
        {
            var types = new List<MediaType>();
            var formats = new List<VideoFormat>();

            foreach (var media in mediaList)
            {
                var video = media as Video;

                if (video != null)
                {
                    if (!string.IsNullOrEmpty(video.VideoFormat))
                    {
                        var format = (VideoFormat)Enum.Parse(typeof(VideoFormat), video.VideoFormat);
                        formats.Add(format);
                    }

                }

                types.Add(media.MediaType);
            }

            bool isMultiFile = mediaList.Count() == 1 ? (mediaList.First().Files.Count() > 1) : (mediaList.Count() > 1);

            return CanPlay(player, types, formats, isMultiFile);
        }

        /// <summary>
        /// Detmines if a given external player configuration is configured to play:
        /// - ALL of MediaTypes supplied. This filter is ignored if an empty list is provided.
        /// - All of the VideoFormats supplied. This filter is ignored if an empty list is provided.
        /// - And is able to play the number of files requested
        /// </summary>
        public static bool CanPlay(CommonConfigData.ExternalPlayer externalPlayer, IEnumerable<MediaType> mediaTypes, IEnumerable<VideoFormat> videoFormats, bool isMultiFile)
        {
            // Check options to see if this is not a match
            if (Application.RunningOnExtender)
            {
                return false;
            }

            // If it's not even capable of playing multiple files in sequence, it's no good
            if (isMultiFile && !externalPlayer.SupportsMultiFileCommandArguments && !externalPlayer.SupportsPlaylists)
            {
                return false;
            }

            // If configuration wants specific MediaTypes, check that here
            // If no MediaTypes are specified, proceed
            foreach (MediaType mediaType in mediaTypes)
            {
                if (!externalPlayer.MediaTypes.Contains(mediaType))
                {
                    return false;
                }
            }

            // If configuration wants specific VideoFormats, check that here
            // If no VideoFormats are specified, proceed
            foreach (VideoFormat format in videoFormats)
            {
                if (!externalPlayer.VideoFormats.Contains(format))
                {
                    return false;
                }
            }

            return true;
        }

        
    }
}
