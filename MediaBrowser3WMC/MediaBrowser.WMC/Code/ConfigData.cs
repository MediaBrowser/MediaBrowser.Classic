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
using MediaBrowser.Model.Updates;

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
        //this is now on server
        [SkipField]
        public string SupporterKey
        {
            get { return ""; }
            set {  }
        }

        [Hidden] 
        public bool InvalidateRecentLists = false;
        [Group("Playback")]
        [Comment(@"By default we track a videos position to support resume, this can be disabled by setting this for diagnostic purposes")]
        public bool EnableResumeSupport = true; 
        [Comment(@"Any folder named trailers will be ignored and treated as a folder containing trailers")]
        public bool EnableLocalTrailerSupport = true; 
        [Group("Display")]
        [Comment(@"If set to true when sorting by unwatched the unwatched folders will be sorted by name")]
        public bool SortUnwatchedByName = false;

        [Group("Display")]
        [Comment("Show now playing for default mode as text")]
        public bool ShowNowPlayingInText = false;

        [Group("Display")]
        [Comment("Show a menu to confirm exit")]
        public bool UseExitMenu = true;

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
        [Group("Display")]
        [Comment(@"Show collection of items marked as favorites.")]
        public bool ShowFavoritesCollection = true;
        [Group("Display")]
        [Comment(@"Show collection of movies by genre.")]
        public bool ShowMovieGenreCollection = true;
        [Comment(@"Show collection of Albums by genre.")]
        public bool ShowMusicGenreCollection = true;
        [Comment(@"Group albums by artist.")]
        public bool GroupAlbumsByArtist = true;
        [Comment(@"Show collection of Albums.")]
        public bool ShowMusicAlbumCollection = true;
        [Group("Display")]
        public bool ShowChannels = true;
        public bool GroupChannelsTogether = false;
        [Comment(@"Show notifications of new items.")]
        public bool ShowNewItemNotification = true;
        public bool ShowNewItemNotificationInPlayer = true;
        [Group("Display")]
        [Comment(@"Name of favorite collection folder.")]
        public string FavoriteFolderName = "Favorites";
        [Comment(@"Name of favorite collection folder.")]
        public string MusicGenreFolderName = "Music by Genre";
        [Group("Display")]
        [Comment(@"Name of album collection folder.")]
        public string MusicAlbumFolderName = "Music Albums";
        [Comment(@"Name of movie genre collection folder.")]
        public string MovieGenreFolderName = "Movies by Genre";
        [Group("Display")]
        [Comment(@"The default size posters will be shown in any new view.")]
        public Size DefaultPosterSize = new Size() {Width=220, Height=330};
        [Group("Display")]
        [Comment(@"The amount of space between posters in lists and grids.")]
        public Size GridSpacing = new Size();
        [Group("Display")]
        [Comment(@"Maximum amount a poster can be squeezed.")]
        public float MaximumAspectRatioDistortion = 0.2F;
        [Dangerous]
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
        [Group("Playback")]
        [Comment(@"The interval between images in the photo slide show.")]
        public int SlideShowInterval = 8;
        [Hidden]
        public string InitialFolder = ApplicationPaths.AppInitialDirPath;
        [Dangerous]
        [PresentationStyle("BrowseFolder")]
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
        [Comment(@"Show missing items.")]
        public bool ShowMissingItems = true;
        [Group("Display")]
        [Comment(@"Show unaired items.")]
        public bool ShowUnairedItems = true;
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
        public string DefaultViewType = ViewType.CoverFlow.ToString();
        [Group("Display")]
        [Comment(@"Show the titles of items in poster views.")]
        public bool DefaultShowLabels = false;
        [Group("Display")]
        [Comment(@"Scroll poster views vertically instead of horizontally.")]
        public bool DefaultVerticalScroll = false;
        [Group("Display")]
        [Comment(@"The number of seconds to wait for message boxes if not otherwise specified.")]
        public int DefaultMessageTimeout = 30;
        [Comment(@"The number of seconds to display new item notification.")]
        public int NewItemNotificationDisplayTime = 10;
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
        [Comment(@"Remember (and re-filter) the fildtered state of a folder so it will stay filtered on subsequent entries.")]
        public bool RememberFilters = false;
        [Dangerous]
        [Group("Display")]
        public string Theme = "Black";
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
        [Group("Display")]
        [Comment(@"Show fan art on views that support it.")]
        public bool ShowBackdrop = true;
        [Group("Display")]
        [Comment(@"Collapse movies into box sets if defined.")]
        public bool CollapseBoxSets = false;
        [Group("Display")]
        [Dangerous]
        public string ViewTheme = "Chocolate";
        [Group("Display")]
        public int AlphaBlending = 80;
        [Group("Display")]
        public bool ShowConfigButton = false;

        [Group("Display")]
        public bool EnableSyncViews = true;
        [Group("Display")]
        public bool ShowRootBackground = true;

        [Group("Display")]
        public bool HideFocusFrame = false;


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

        public bool RandomizeBackdrops = false;
        public bool RotateBackdrops = true;
        public int BackdropRotationInterval = 8; //Controls time delay, in seconds, between backdrops during rotation
        public float BackdropTransitionInterval = 1.5F; //Controls animation fade time, in seconds
        public int BackdropLoadDelay = 300; //Delays loading of the first backdrop on new item in milliseconds. Helps with performance

        public bool ProcessBanners = false; //hook to allow future processing of banners
        [Hidden]
        public bool ProcessBackdrops = false; //hook to allow future processing of backdrops

        public bool YearSortAsc = false; //true to sort years in ascending order

        public bool AutoScrollText = false; //Turn on/off Auto Scrolling Text (typically for Overviews)
        public int AutoScrollDelay = 8; //Delay to Start and Reset scrolling text
        public int AutoScrollSpeed = 1; //Scroll Speed for scrolling Text

        public bool TreatWatchedAsInProgress = false;

        public bool EnableScreenSaver = true; //enable default screen saver functionality
        public int ScreenSaverTimeOut = 10; //minutes of inactivity for screen saver to kick in

        public bool AskIncludeChildrenRefresh = true; //prompt to include children on a folder refresh
        public bool DefaultIncludeChildrenRefresh = true; //if we don't prompt, are children included?

        public bool UseCustomPlayerInterface = true; //use our custom player with overlays

        public int DefaultSkipSeconds = 30; //default number of seconds to skip ahead in custom player
        public int DefaultSkipBackSeconds = 10; //default number of seconds to skip back in custom player
        public int InputActivityTimeout = 8; //default number of seconds to wait before firing no input (mouse/keyboard) event

        

        [Comment("Cache all images in memory so navigation is faster, consumes a lot more memory")]
        public bool CacheAllImagesInMemory = false;

        [Comment("This is a hack until I can rewrite some file date processing")]
        public bool EnableShortcutDateHack = true;

        [Hidden]
        [Group("Display")]
        [Comment("Hide empty folders (and series and seasons)")]
        public bool HideEmptyFolders = false;


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


        
    }
}
