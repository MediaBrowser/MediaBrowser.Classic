using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MediaBrowser.Code;
using MediaBrowser.Code.ShadowTypes;
using MediaBrowser.Library;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities;

namespace MediaBrowser
{
    /// <summary>
    /// Used for the expert config explorer
    /// </summary>
    public class ConfigMember
    {
        public MemberInfo Info { get; set; }
        public string Group { get; set; }
        public string Comment { get; set; }
        public string Name { get; set; }
        public string PresentationStyle { get; set; }
        public bool IsDangerous { get; set; }
        public Type Type { get; set; }
        private ConfigData data;

        public ConfigMember(MemberInfo info, ConfigData data)
        {
            this.data = data;
            this.Info = info;
            this.Name = info.Name;
            this.Group = XmlSettings<ConfigData>.GetGroup(info);
            this.Comment = XmlSettings<ConfigData>.GetComment(info);
            this.PresentationStyle = XmlSettings<ConfigData>.GetPresentationStyle(info);
            this.IsDangerous = XmlSettings<ConfigData>.IsDangerous(info);
            if (info.MemberType == MemberTypes.Property)
            {
                this.Type = (info as PropertyInfo).PropertyType;
            }
            else if (info.MemberType == MemberTypes.Field)
            {
                this.Type = (info as FieldInfo).FieldType;
            }

        }

        public object Value
        {
            get
            {
                if (Info.MemberType == MemberTypes.Property)
                {
                    return (Info as PropertyInfo).GetValue(data, null);
                }
                else if (Info.MemberType == MemberTypes.Field)
                {
                    return (Info as FieldInfo).GetValue(data);
                }
                return null;
            }
            set
            {
                try
                {
                    if (Info.MemberType == MemberTypes.Property)
                    {
                        (Info as PropertyInfo).SetValue(data, value, null);
                    }
                    else if (Info.MemberType == MemberTypes.Field)
                    {
                        (Info as FieldInfo).SetValue(data, value);
                    }
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error attempting to assign value to member " + Info.Name, e);
                }
            }
        }


        public override string ToString()
        {
            return this.Name;
        }
    }

    [Serializable]
    public class ConfigData
    {
        //moved keyfile re-routing to here so can be accessed from service (outside MC) -ebr
        [SkipField]
        private KeyFile _keyFile;
        private KeyFile keyFile //only want to create this file if we try to access it
        {
            get
            {
                if (_keyFile == null)
                {
                    _keyFile = new KeyFile(Path.Combine(ApplicationPaths.AppConfigPath, "MB.lic"));
                }
                return _keyFile;
            }
        }

        //this is re-routed to a separate file
        [SkipField]
        public string SupporterKey
        {
            get { return this.keyFile.SupporterKey; }
            set { if (this.keyFile.SupporterKey != value) { this.keyFile.SupporterKey = value; this.keyFile.Save(); } }
        }

        [Dangerous]
        [Comment(@"The version is used to determine if this is the first time a particular version has been run")]
        public string MBVersion = "1.0.0.0"; //default value will tell us if it is a brand new install
        [Group("Playback")]
        [Comment(@"By default we track a videos position to support resume, this can be disabled by setting this for diagnostic purposes")]
        public bool EnableResumeSupport = true; 
        [Comment(@"Any folder named trailers will be ignored and treated as a folder containing trailers")]
        public bool EnableLocalTrailerSupport = true; 
        [Group("Updates")]
        [Comment(@"If you enable this MB will watch for changes in your file system and update the UI as it happens, may not work properly with SMB shares")]
        public bool EnableDirectoryWatchers = true;

        [Group("Display")]
        [Comment(@"If set to true when sorting by unwatched the unwatched folders will be sorted by name")]
        public bool SortUnwatchedByName = false;

        [Group("Display")]
        [Comment("Show now playing for default mode as text")]
        public bool ShowNowPlayingInText = false;

        [Group("Updates")]
        [Comment("The date auto update last checked for a new version")]
        public DateTime LastAutoUpdateCheck = DateTime.Today.AddYears(-1);

        [Group("Display")]
        [Comment(@"If disabled, some items (like TV Episodes or items without much metadata) will play instead of show a detail page when selected.")]
        public bool AlwaysShowDetailsPage = true;
        [Group("Depricated")]
        [Hidden]
        public bool EnableVistaStopPlayStopHack = true;
        [Group("Display")]
        [Comment(@"Show an 'Enhanced Home Screen' for the top level items in MB.")]
        public bool EnableRootPage = true;
        [Dangerous]
        [Comment(@"Identifies if this is the very first time MB has been run.  Causes an initial setup routine to be performed.")]
        public bool IsFirstRun = true;
        [Comment(@"Identifies where MB will look for the special 'IBN' items.  You can change this to point to a location that is shared by multiple machines.")]
        [PresentationStyle("BrowseFolder")]
        public string ImageByNameLocation = Path.Combine(ApplicationPaths.AppConfigPath, "ImagesByName");
        [Group("Display")]
        [Comment(@"Scale factor to account for monitor over/underscan. Format: 0,0,0.")]
        public Vector3 OverScanScaling = new Vector3() {X=1, Y=1, Z=1};
        [Group("Display")]
        [Comment(@"Extra space to account for monitor over/underscan. Format: 0,0.")]
        public Inset OverScanPadding = new Inset();
        [Comment(@"Turns on logging for all MB components. Recommended you leave this on as it doesn't slow things down and is very helpful in troubleshooting.")]
        public bool EnableTraceLogging = true;
        [Group("Display")]
        [Comment(@"The default size posters will be shown in any new view.")]
        public Size DefaultPosterSize = new Size() {Width=220, Height=330};
        [Group("Display")]
        [Comment(@"The amount of space between posters in lists and grids.")]
        public Size GridSpacing = new Size();
        [Group("Display")]
        [Comment(@"Maximum amount a poster can be squeezed.")]
        public float MaximumAspectRatioDistortion = 0.2F;
        [Group("Playback")]
        [Comment(@"Enable transcoding for 360 extender.")]
        public bool EnableTranscode360 = false;
        [Dangerous]
        [Group("Playback")]
        [Comment(@"The extensions of file types that the Xbox 360 can play natively (without transcoding).")]
        public string ExtenderNativeTypes = ".dvr-ms,.wmv";
        [Group("Display")]
        [Comment(@"Show main background for theme.  If false, Media Center background can show through.")]
        public bool ShowThemeBackground = true;
        [Group("Display")]
        [Comment(@"Apply a 'dimming' effect on non-selected posters leaving the active one appearing brighter.")]
        public bool DimUnselectedPosters = true;
        [Comment(@"Allow Movies to consist of nested folders with videos in them - this is trouble, keep it off...")]
        public bool EnableNestedMovieFolders = false;
        [Group("Playback")]
        [Comment(@"Treat multiple video files in a single folder as one movie.")]
        public bool EnableMoviePlaylists = false;
        [Group("Playback")]
        [Comment(@"The maximum number of files that will be treated as one movie if EnableMoviePlaylists is set to true.")]
        public int PlaylistLimit = 2;
        [Hidden]
        public string InitialFolder = ApplicationPaths.AppInitialDirPath;
        [Group("Updates")]
        [Comment(@"Enable the automatic checking for updates (both MB and plugins).")]
        public bool EnableUpdates = true;
        [Group("Updates")]
        [Comment(@"Look for beta versions of MB in the auto update check.")]
        public bool EnableBetas = false;
        [Dangerous]
        [PresentationStyle("BrowseFolder")]
        [Group("Playback")]
        [Comment(@"The location of your ISO mounting program.")]
        public string DaemonToolsLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"DAEMON Tools Lite\\daemon.exe");
        [Group("Playback")]
        [Comment(@"The drive letter to use when mounting an ISO.")]
        public string DaemonToolsDrive = "E";
        [Group("Display")]
        [Comment(@"Handle sorts with numbers in the names properly with respect to those numbers.")]
        public bool EnableAlphanumericSorting = true;
        [Group("Display")]
        [Comment(@"Show the played tick mark in list views.")]
        public bool EnableListViewTicks = false;
        [Group("Display")]
        [Comment(@"The color for played items in list views.")]
        public Colors ListViewWatchedColor = Colors.LightSkyBlue;
        [Group("Display")]
        [Comment(@"Use a different color for played items in list views.")]
        public bool EnableListViewWatchedColor = true;
        [Group("Display")]
        [Comment(@"Show the number of unplayed items inside a folder.")]
        public bool ShowUnwatchedCount = true;
        [Group("Display")]
        [Comment(@"Show an indicator if an entire folder has been played.")]
        public bool ShowWatchedTickOnFolders = true;
        [Group("Display")]
        [Comment(@"Show an indicator on the poster if an item has been played.")]
        public bool ShowWatchTickInPosterView = true;
        [Group("Display")]
        [Comment(@"Scroll to the first unplayed item when entering a view.")]
        public bool DefaultToFirstUnwatched = false;
        [Group("Display")]
        [Comment(@"If a folder contains only one item, automatically enter that item.")]
        public bool AutoEnterSingleDirs = false; 
        [Group("Display")]
        [Comment(@"Any item older than this will report it has been played.")]
        public DateTime AssumeWatchedBefore = DateTime.Today.AddYears(-1);
        [Group("Display")]
        [Comment(@"Sub-folders will inherit their parent's view if not set specifically.")]
        public bool InheritDefaultView = true;
        [Dangerous]
        [Group("Display")]
        [Comment(@"The default view type to use on new folders.")]
        public string DefaultViewType = ViewType.Poster.ToString();
        [Group("Display")]
        [Comment(@"Show the titles of items in poster views.")]
        public bool DefaultShowLabels = false;
        [Group("Display")]
        [Comment(@"Scroll poster views vertically instead of horizontally.")]
        public bool DefaultVerticalScroll = false;
        [Group("Display")]
        [Comment(@"The number of history items to show in the 'breadcrumbs' (how deep in the structure you are) .")]
        public int BreadcrumbCountLimit = 2;
        [Dangerous]
        [Group("Display")]
        [Comment(@"Characters to be ignored in sorting.")]
        public string SortRemoveCharacters = ",|&|-|{|}|'";
        [Group("Display")]
        [Comment("List of characters to replace with a ' ' in titles for alphanumeric sorting.  Separate each character with a '|'.\nThis allows titles like 'Iron.Man.REPACK.720p.BluRay.x264-SEPTiC.mkv' to be properly sorted.")]
        public string SortReplaceCharacters = ".|+|%";
        [Group("Display")]
        [Comment(@"List of words to remove from alphanumeric sorting.  Separate each word with a '|'.  Note that the
        algorithm appends a ' ' to the end of each word during the search which means words found at the end
        of each title will not be removed.  This is generally not an issue since most people will only want
        articles removed and articles are rarely found at the end of media titles.  This, combined with SortReplaceCharacters,
        allows titles like 'The.Adventures.Of.Baron.Munchausen.1988.720p.BluRay.x264-SiNNERS.mkv' to be properly sorted.")]
        public string SortReplaceWords = "the|a|an";
        [Comment(@"Allow metadata providers that search the internet (including the internal tmdb/tvdb fetchers).")]
        public bool AllowInternetMetadataProviders = true;
        [Group("Display")]
        [Comment(@"Width of the poster area in Thumb view (may only affect default and diamond themes).")]
        public int ThumbStripPosterWidth = 550;
        [Group("Display")]
        [Comment(@"Remember (and re-index) the indexing state of a folder so it will stay indexed on subsequent entries.")]
        public bool RememberIndexing = false;
        public bool ShowIndexWarning = true;
        public double IndexWarningThreshold = 0.1;
        [Comment(@"The two-character language code to use when retrieving metadata (en,fr,it, etc.).")]
        public string PreferredMetaDataLanguage = "en";
        [Hidden]
        public List<ExternalPlayer> ExternalPlayers = new List<ExternalPlayer>();
        [Dangerous]
        [Group("Display")]
        [Comment(@"The view theme to use.")]
        public string Theme = "Default";
        [Dangerous]
        [Group("Display")]
        [Comment(@"The set of fonts/colors to use.")]
        public string FontTheme = "Default";
        // I love the clock, but it keeps on crashing the app, so disabling it for now
        [Group("Display")]
        [Comment(@"Show the current time on some screens (may only affect some themes).")]
        public bool ShowClock = false;
        [Dangerous]
        [Group("Advanced")]
        [Comment(@"Enable the use of some dangerous features.")]
        public bool EnableAdvancedCmds = false;
        [Dangerous]
        [Group("Advanced")]
        [Comment(@"Enable deleting content within MB.")]
        public bool Advanced_EnableDelete = false;
        [Group("Playback")]
        [Comment(@"Instead of directly mounting an ISO allow the 'autoplay' settings in windows to handle it.")]
        public bool UseAutoPlayForIso = false;
        [Group("Display")]
        [Comment(@"Show fan art on views that support it.")]
        public bool ShowBackdrop = true;
        [Group("Display")]
        [Comment(@"The name of your top level item that will show in the 'breadcrumbs'.")]
        public string InitialBreadcrumbName = "Media";

        [PresentationStyle("BrowseFolder")]
        [Comment(@"Path to save display preferences and playstate information.  Change this to use a shared location for multiple machines.")]
        public string UserSettingsPath = null;
        [Group("Display")]
        [Dangerous]
        public string ViewTheme = "Default";
        [Group("Display")]
        public int AlphaBlending = 80;
        [Group("Display")]
        public bool ShowConfigButton = false;

        [Group("Display")]
        public bool EnableSyncViews = true;
        public string YahooWeatherFeed = "UKXX0085";
        public string YahooWeatherUnit = "c";
        [Group("Display")]
        public bool ShowRootBackground = true;

        [Dangerous]
        [Group("Advanced")]
        [Comment(@"The directory where MB was installed. Filled in at install time and used to call components.")]
        public string MBInstallDir = "";

        [PresentationStyle("BrowseFolder")]
        public string PodcastHome = ApplicationPaths.DefaultPodcastPath;
        [Group("Display")]
        public bool HideFocusFrame = false;

        [Group("Depricated")]
        [Hidden]
        public bool EnableProxyLikeCaching = false;
        public int MetadataCheckForUpdateAge = 14;

        [Group("Parental Control")]
        public int ParentalUnlockPeriod = 3;
        [Group("Parental Control")]
        public bool HideParentalDisAllowed = true; 
        [Group("Parental Control")]
        public bool ParentalBlockUnrated = false;
        [Group("Parental Control")]
        public bool UnlockOnPinEntry = true;
        [Group("Parental Control")]
        public bool ParentalControlEnabled = false;
        [Group("Parental Control")]
        [Hidden]
        public string ParentalPIN = "0000";
        [Group("Parental Control")]
        public int MaxParentalLevel = 3;

        public bool EnableMouseHook = false;

        [Group("Display")]
        public int RecentItemCount = 20;
        [Group("Display")]
        public int RecentItemContainerCount = 50;
        [Group("Display")]
        public int RecentItemDays = 60;
        [Group("Display")]
        [Dangerous]
        public string RecentItemOption = "added";
        [Group("Display")]
        public int RecentItemCollapseThresh = 2;

        public bool ShowHDIndicatorOnPosters = false;
        public bool ShowRemoteIndicatorOnPosters = true;
        public bool ExcludeRemoteContentInSearch = true;

        public bool ShowUnwatchedIndicator = false;
        public bool PNGTakesPrecedence = false;

        public bool RandomizeBackdrops = false;
        public bool RotateBackdrops = true;
        public int BackdropRotationInterval = 8; //Controls time delay, in seconds, between backdrops during rotation
        public float BackdropTransitionInterval = 1.5F; //Controls animation fade time, in seconds
        public int BackdropLoadDelay = 300; //Delays loading of the first backdrop on new item in milliseconds. Helps with performance

        public bool ProcessBanners = false; //hook to allow future processing of banners
        [Hidden]
        public bool ProcessBackdrops = false; //hook to allow future processing of backdrops

        public int MinResumeDuration = 0; //minimum duration of video to have resume functionality
        public int MinResumePct = 1; //if this far or less into video, don't resume
        public int MaxResumePct = 95; //if this far or more into video, don't resume

        public bool YearSortAsc = false; //true to sort years in ascending order

        public bool AutoScrollText = false; //Turn on/off Auto Scrolling Text (typically for Overviews)
        public int AutoScrollDelay = 8; //Delay to Start and Reset scrolling text
        public int AutoScrollSpeed = 1; //Scroll Speed for scrolling Text

        public bool AutoValidate = true; //automatically validate and refresh items as we access them

        public LogSeverity MinLoggingSeverity = LogSeverity.Info;

        public bool EnableScreenSaver = true; //enable default screen saver functionality
        public int ScreenSaverTimeOut = 10; //minutes of inactivity for screen saver to kick in

        public bool AskIncludeChildrenRefresh = true; //prompt to include children on a folder refresh
        public bool DefaultIncludeChildrenRefresh = true; //if we don't prompt, are children included?
        
        [Hidden]
        public int NetworkAvailableTimeOut = 5000; //milliseconds to wait for network to be available on validations

        //public bool UseSQLImageCache = false; //switch to use the new SQLite image cache

        [Comment("Cache all images in memory so navigation is faster, consumes a lot more memory")]
        public bool CacheAllImagesInMemory = false;

        [Comment("The number of days to retain log files.  Files older than this will be deleted periodically")]
        public int LogFileRetentionDays = 30;

        [Comment("Whether to send os and memory stats during update check")]
        public bool SendStats = false;

        [Comment("Suppress the statistics nag msg")]
        public bool SuppressStatsNag = false;

        [Comment("This is a hack until I can rewrite some file date processing")]
        public bool EnableShortcutDateHack = true;

        [Hidden]
        [Group("Display")]
        [Comment("Hide empty folders (and series and seasons)")]
        public bool HideEmptyFolders = false;

        [Comment("Save metadata locally so it doesn't have to be re-fetched from the inet")]
        public bool SaveLocalMeta = false;

        [Comment("Save backdrops at the season level (if false will inherit from series)")]
        public bool SaveSeasonBackdrops = false;

        [Comment("Maximum number of backdrops to download from internet provider")]
        public int MaxBackdrops = 4; //maximum number of backdrops to be saved by the inet providers

        [Comment("Download people images to IBN")]
        public bool DownloadPeopleImages = true;

        [Comment("Refresh the images from TMDB when we fetch other meta.  This will replace local versions.")]
        public bool RefreshItemImages = true;

        [Comment("The size of posters to fetch from tmdb")]
        public string FetchedPosterSize = "w500"; //w500, w342, w185 or original

        [Comment("The size of backdrops to fetch from tmdb")]
        public string FetchedBackdropSize = "w1280"; //w1280, w780 or original

        [Comment("The size of people profile images to fetch from tmdb")]
        public string FetchedProfileSize = "w185"; //w185 w45 h632 or original

        [Comment("The country to fetch release date and certification for (ISO 3166.1 code - US, DE, GB, etc.)")]
        public string MetadataCountryCode = "US"; //ISO 3166.1 code

        [Comment("The base url for tmdb images")]
        public string TmdbImageUrl = "http://cf2.imgobject.com/t/p/"; 

        [Dangerous]
        public List<string> PluginSources = new List<string>() { "http://www.mediabrowser.tv/plugins/multi/plugin_info.xml" };

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
        public ConfigData ()
	    {
            try
            {
                File.Delete(ApplicationPaths.ConfigFile);
            }
            catch (Exception e)
            {
                MediaBrowser.Library.Logging.Logger.ReportException("Unable to delete config file " + ApplicationPaths.ConfigFile, e);
            }
            //continue anyway
            this.file = ApplicationPaths.ConfigFile;
            this.settings = XmlSettings<ConfigData>.Bind(this, file);
	    }


        public ConfigData(string file)
        {
            this.file = file;
            this.settings = XmlSettings<ConfigData>.Bind(this, file);
        }

        [SkipField]
        string file;

        [SkipField]
        XmlSettings<ConfigData> settings;


        public static ConfigData FromFile(string file)
        {
            return new ConfigData(file);  
        }

        public void Save() {
            this.settings.Write();
            //notify of the change
            MediaBrowser.Library.Threading.Async.Queue("Config notify", () => Kernel.Instance.NotifyConfigChange());
        }

        /// <summary>
        /// Determines if a given external player configuration is configured to play a list of files
        /// </summary>
        public static bool CanPlay(ConfigData.ExternalPlayer player, IEnumerable<string> files)
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
        public static bool CanPlay(ConfigData.ExternalPlayer player, IEnumerable<Media> mediaList)
        {
            List<MediaType> types = new List<MediaType>();
            List<VideoFormat> formats = new List<VideoFormat>();

            foreach (Media media in mediaList)
            {
                var video = media as Video;

                if (video != null)
                {
                    if (!string.IsNullOrEmpty(video.VideoFormat))
                    {
                        VideoFormat format = (VideoFormat)Enum.Parse(typeof(VideoFormat), video.VideoFormat);
                        formats.Add(format);
                    }

                    types.Add(video.MediaType);
                }
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
        public static bool CanPlay(ConfigData.ExternalPlayer externalPlayer, IEnumerable<MediaType> mediaTypes, IEnumerable<VideoFormat> videoFormats, bool isMultiFile)
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
