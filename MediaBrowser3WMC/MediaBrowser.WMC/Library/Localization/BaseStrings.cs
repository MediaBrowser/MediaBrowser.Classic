using System;
using System.IO;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Library.Localization
{
    [Serializable]
    public class BaseStrings
    {
        const string VERSION = "1.0087";
        const string ENFILE = "strings-en.xml";

        public string Version = VERSION; //this is used to see if we have changed and need to re-save

        //these are our strings keyed by property name
        public string LoggingConfigDesc = "Write messages to a log file at run time. Please leave enabled.  It won't slow the program down.";
        public string EnableScreenSaverConfigDesc = "Enable Screen Saver functionality after defined time out.";
        public string SSTimeOutConfigDesc = "Minutes Before Screen Saver is Activated.";
        public string EnableInternetProvidersConfigDesc = "Search the Internet for Cover Art, Backdrops and Metadata.";
        public string AutomaticUpdatesConfigDesc = "Automatically Check for Updates to MediaBrowser and Plug-ins for Admin users.";
        public string BetaUpdatesConfigDesc = "Include Beta Versions in Automatic Updates";
        public string EnableEHSConfigDesc = "Enable the Enhanced Home Screen for Top-Level Items.";
        public string ShowClockConfigDesc = "Show the Current Time in MediaBrowser Screens.";
        public string DimUnselectedPostersConfigDesc = "Make Posters That are not Selected Slightly Darker.";
        public string HideFocusFrameConfigDesc = "Don't Show a Border Around Selected Posters in Poster Views.";
        public string PosterGridSpacingConfigDesc = "Number of Pixels to Put Between Each Item in a Grid of Posters.";
        public string ThumbWidthSplitConfigDesc = "Number of Pixels to Use as the Width of the Poster Area in Thumb View";
        public string GeneralConfigDesc = "General Configuration Items.";
        public string MediaOptionsConfigDesc = "Media Related Configuration Items.";
        public string ThemesConfigDesc = "Select the Visual Presentation Style of MediaBrowser.";
        public string ParentalControlConfigDesc = "Parental Control Configuration.  Requires PIN to Access.";
        public string RememberFolderIndexingConfigDesc = "If a Folder is Indexed, It Will Be Indexed Even at First Start-up.";
        public string RememberFolderFiltersConfigDesc = "If a Folder is Filtered, It Will Be Filtered Even at First Start-up.";
        public string ShowUnwatchedCountConfigDesc = "Show the Number of Unplayed Items in a Folder on the Folder Poster.";
        public string WatchedIndicatoronFoldersConfigDesc = "Show an Indicator if All Items Inside a Folder Have Been Played.";
        public string WatchedIndicatoronVideosConfigDesc = "Show an Indicator if an Item Has Been Marked Played.";
        public string WatchedIndicatorinDetailViewConfigDesc = "Show the Played Indicator in Lists as Well as Poster Views.";
        public string DefaultToFirstUnwatchedItemConfigDesc = "Scroll to the First Unplayed Item When Entering a Folder.";
        public string AllowNestedMovieFoldersConfigDesc = "Allow the Ability to Put Movie Folders Inside of Other Movie Folders.";
        public string TreatMultipleFilesAsSingleMovieConfigDesc = "If a Folder Contains More than One Playable Item, Play Them in Sequence. Turn this off if you are having trouble with small collections.";
        public string AutoEnterSingleFolderItemsConfigDesc = "If a Folder Contains Only One Item, Automatically Select and Either Play or Go to the Detail View for That Item.";
        public string MultipleFileSizeLimitConfigDesc = "The Maximum Number of Items that will Automatically Play in Sequence.";
        public string BreadcrumbCountConfigDesc = "The Number of Navigation Items to Show in the Trail of Items Entered.";
        public string VisualThemeConfigDesc = "The Basic Presentation Style for MediaBrowser Screens.";
        public string ColorSchemeConfigDesc = "The Style of Colors for Backgrounds, etc.  Won't Take Effect Until MediaBrowser is Restarted.";
        public string FontSizeConfigDesc = "The Size of the Fonts to Use in MediaBrowser.  Won't Take Effect Until MediaBrowser is Restarted.";
        public string ShowConfigButtonConfigDesc = "Show the config button on all screens.";
        public string AlphaBlendingConfigDesc = "The Level of Transparency to Use Behind Text Areas to Make Them More Readable.";
        public string AlwaysShowDetailsConfigDesc = "Always display the details page for media.";
        public string StartDetailsPageinMiniModeConfigDesc = "Default Media Details Page to Mini-Mode. [DIAMOND ONLY]";
        public string SecurityPINConfigDesc = "The 4-Digit Code For Access to Parental Controlled Items.";
        public string EnableParentalBlocksConfigDesc = "Enable Parental Control.  Items Over The Designated Rating Will Be Hidden or Require PIN.";
        public string BlockUnratedContentConfigDesc = "Treat Items With NO RATING INFO as Over the Limit.  Items Actually Rated 'Unrated' Will Behave Like NC-17.";
        public string MaxAllowedRatingConfigDesc = "The Maximum Rating that Should NOT be Blocked.";
        public string HideBlockedContentConfigDesc = "Hide All Items Over the Designated Rating.";
        public string UnlockonPINEntryConfigDesc = "Temporarily Unlock the Entire Library Whenever the Global PIN is Entered.";
        public string UnlockPeriodHoursConfigDesc = "The Amount of Time (in Hours) Before the Library Will Automatically Re-Lock.";
        public string EnterNewPINConfigDesc = "Change the Global Security Code.";
        public string ContinueConfigDesc = "Return to the Previous Screen.  (All Changes Are Saved Automatically)";
        public string ResetDefaultsConfigDesc = "Reset Configuration Items to Their Default Values.  USE WITH CAUTION - Settings Will Be Overwritten.";
        public string ClearCacheConfigDesc = "Delete the Internal Data Files MediaBrowser Uses and Cause Them to be Re-built.";
        public string UnlockConfigDesc = "Temporarily Disable Parental Control for the Entire Library.  Will Re-Lock Automatically.";
        public string AssumeWatchedIfOlderThanConfigDesc = "Mark All Items Older Than This as Played.";
        public string ShowThemeBackgroundConfigDesc = "Display Theme background. [TIER 3] Highest tier background effect takes precedence.";
        public string ShowInitialFolderBackgroundConfigDesc = "Display initial backdrop in all views. (backdrop.png or backdrop.jpg sourced from your initial folder) [TIER 2] Highest tier background effect takes precedence.";
        public string ShowFanArtonViewsConfigDesc = "Display fan art as a Background in views that support this capability. [TIER 1] Highest tier background effect takes precedence.";
        public string EnhancedMouseSupportConfigDesc = "Enable Better Scrolling Support with the Mouse.  Leave OFF if You Don't Use a Mouse.  Won't Take Effect Until MediaBrowser is Restarted.";
        public string ShowHDOverlayonPostersConfigDesc = "Show 'HD' or resolution overlay on Hi-def items in Poster Views.";
        public string ShowIcononRemoteContentConfigDesc = "Show an indicator on items from the web in Poster Views.";
        public string ExcludeRemoteContentInSearchesConfigDesc = "Don't show content from the web when searching entire library.";
        public string HighlightUnwatchedItemsConfigDesc = "Show a Highlight on Unplayed Content.";
        public string RandomizeBackdropConfigDesc = "Select random fan art from the available ones.";
        public string RotateBackdropConfigDesc = "Show all available fan art in a sequence (can be random).";
        public string UpdateLibraryConfigDesc = "Update information on the items in your library.";
        public string BackdropRotationIntervalConfigDesc = "Seconds between Backdrop Rotations.";
        public string BackdropTransitionIntervalConfigDesc = "Fade in seconds for the backdrop transition animation.";
        public string BackdropLoadDelayConfigDesc = "Time to wait before loading initial backdrop, in milliseconds.";
        public string AutoScrollTextConfigDesc = "Turn on Automatically Scrolling Overviews.";
        public string SortYearsAscConfigDesc = "When sorting by 'year' order from lowest to highest (default is highest to lowest).";
        public string AutoValidateConfigDesc = "Automatically validate and refresh while navigating. WARNING. Turning this off may cause the library to be inaccurate and you may need to manually refresh.";
        public string SaveLocalMetaConfigDesc = "Save images and xml data locally with the media.  Prevents having to re-retrieve from internet providers on refresh.  Delete files to re-fetch.";
        public string HideEmptyFoldersConfigDesc = "Hide items with no media in them in the interface.";
        public string SystemClassConfigDesc = "Level at which to subscribe to system updates.";
        public string PluginClassConfigDesc = "Level at which to subscribe to plug-in updates.";
        public string ShowFavoritesCollectionConfigDesc = "Show a top level folder with all your items marked as Favorite in it. Restart MBC to see changes.";
        public string ShowGenresCollectionConfigDesc = "Show a top level folder of all movies by genre. Restart MBC to see changes.";
        public string ShowMusicGenresCollectionConfigDesc = "Show a top level folder of all music by genre. Restart MBC to see changes.";
        public string ShowMusicAlbumCollectionConfigDesc = "Show a top level folder of all music by album. Restart MBC to see changes.";
        public string ShowNewItemNotificationConfigDesc = "Show a notification window when new items are added.";
        public string CheckForUpdateConfigDesc = "Check for updates to MB Classic.";
        public string TreatWatchedAsInProgressConfigDesc = "Treat the 'Watched' recent list as an In-Progress list instead.  Anything resumable will appear instead of fully-watched items.";
        public string SlideShowIntervalConfigDesc = "The number of seconds to show each item in a photo slide show";
        public string ShowUnairedItemsConfigDesc = "Enable to show TV episodes that have information but have yet to actually air";
        public string ShowMissingItemsConfigDesc = "Enable to show TV episodes that exist but are not actually in your collection";
        public string HttpTimeoutConfigDesc = "The maximum amount of time to wait for the server to respond to a request. 1000 = 1 second.";
        public string StartUpConfigDesc = "Whether to show the user profile screen or automatically log in at start up.";
        public string EnableDeleteConfigDesc = "Allow Admin users to delete media items. W A R N I N G - This will DELETE actual media files from your system.";
        public string ConnectionConfigDesc = "Either connect to this server each time or find an available server at start up.";
        public string PortConfigDesc = "The configured port for your server. Default 8096.";
        public string PluginsConfigDesc = "Manage Media Browser Classic Plug-ins.";
        public string WakeServerConfigDesc = "Try to wake up the last known server when MBC starts (in case it is in sleep mode).";
        public string CollapseBoxSetsConfigDesc = "Collapse movies defined into collections (box sets) into those collections in views.";
        public string SilentUpdatesConfigDesc = "Update plug-ins automatically in the background for all users.";
        public string ConfirmExitConfigDesc = "Present a menu when exiting the application to allow other options and prevent accidental exit";
        public string AutoLogoffConfigDesc = "Automatically log off MBC after the configured period of inactivity";
        public string AutoLogoffTimeOutConfigDesc = "The minutes of inactivity before MBC will automatically logoff";
        public string WarnOnStreamConfigDesc = "Show a warning message if content is being streamed/transcoded instead of accessed directly";
        public string LocalMaxBitrateConfigDesc = "The maximum bitrate to use when streaming content when attached to the same network as the server";
        public string RemoteMaxBitrateConfigDesc = "The maximum bitrate to use when streaming content when attached to server via a remote network";
        public string InputActivityTimeoutConfigDesc = "The number of seconds of no mouse or key activity to consider idle and hide the player overlay";
        public string DefaultSkipSecondsConfigDesc = "The number of seconds to skip ahead when FF is pressed without a specific number entered";
        public string DefaultSkipBackSecondsConfigDesc = "The number of seconds to skip back when Rew is pressed without a specific number entered";
        public string UseCustomPlayerConfigDesc = "Use a custom interface on the internal WMC player giving a more modern look and better control";
        public string DisableCustomPlayerForDvdConfigDesc = "Don't use custom interface for DVD Rips.  Enable this if your rips have DVD menus.";
        public string DisableMcConflictingOperationsConfigDesc = "Disable controls that conflict with the Media Control MCE Add-in";
        public string ShowNewItemNotificationInPlayerConfigDesc = "Allow a small new item notification on top of playing video";
        public string ShowChannelsConfigDesc = "Include 'Channel' Items in MBC.  Restart MBC to see changes.";
        public string GroupChannelsTogetherConfigDesc = "Group All Channels Under One Top-Level 'Channels' Item.  Restart MBC to see changes.";
        public string BottomConfigDesc = "Adust to account for blue borders around edge or screen too large for physical screen. Positive or Negative.";
        public string TopConfigDesc = "Adust to account for blue borders around edge or screen too large for physical screen. Positive or Negative.";
        public string LeftConfigDesc = "Adust to account for blue borders around edge or screen too large for physical screen. Positive or Negative.";
        public string RightConfigDesc = "Adust to account for blue borders around edge or screen too large for physical screen. Positive or Negative.";

        //Config Panel
        public string AdvancedConfig = "Advanced";
        public string LoginConfig = "Log In";
        public string StartUpConfig = "Start Up Behaviour";
        public string LibraryConfig = "Library";
        public string ConfigConfig = "Configuration";
        public string VersionConfig = "Version";
        public string MediaOptionsConfig = "Media";
        public string SlideShowIntervalConfig = "Photo Slide Show Interval";
        public string ThemesConfig = "Theme Options";
        public string ViewOptionsConfig = "View Options";
        public string TopLevelViewsConfig = "Top Level Views";
        public string IndicatorsConfig = "Indicators";
        public string SystemOptionsConfig = "System";
        public string ParentalControlConfig = "Parental Control";
        public string ContinueConfig = "Continue";
        public string ResetDefaultsConfig = "Reset Defaults";
        public string ClearCacheConfig = "Clear Cache";
        public string UnlockConfig = "Unlock";
        public string GeneralConfig = "General";
        public string GeneralDisplayConfig = "General Display";
        public string EnableScreenSaverConfig = "Screen Saver";
        public string SSTimeOutConfig = "Timeout (mins)";
        public string TrackingConfig = "Tracking";
        public string AssumeWatchedIfOlderThanConfig = "Assume Played If Older Than";
        public string MetadataConfig = "Metadata";
        public string EnableInternetProvidersConfig = "Allow Internet Providers";
        public string UpdatesConfig = "Updates";
        public string SilentUpdatesConfig = "Silently Update Plug-ins";
        public string AutomaticUpdatesConfig = "Automatically Check For Updates";
        public string LoggingConfig = "Logging";
        public string BetaUpdatesConfig = "Beta Updates";
        public string GlobalConfig = "Global";
        public string EnableEHSConfig = "Enable EHS";
        public string ShowClockConfig = "Show Clock";
        public string DimUnselectedPostersConfig = "Dim Unselected Posters";
        public string HideFocusFrameConfig = "Hide Focus Frame";
        public string AlwaysShowDetailsConfig = "Always Show Details";
        public string ExcludeRemoteContentInSearchesConfig = "Exclude Remote Content In Searches";
        public string EnhancedMouseSupportConfig = "Enhanced Mouse Support";
        public string ShowFavoritesCollectionConfig = "Show Favorites Folder";
        public string ShowGenresCollectionConfig = "Show Movie Genres Folder";
        public string ShowMusicGenresCollectionConfig = "Show Music Genres Folder";
        public string ShowMusicAlbumCollectionConfig = "Show Music Albums Folder";
        public string ShowUnairedItemsConfig = "Show Un-aired Items";
        public string ShowMissingItemsConfig = "Show Missing Items";
        public string GroupAlbumsByArtistConfig = "Group by Artist";
        public string ShowNewItemNotificationConfig = "Show New Item Notification";
        public string ViewsConfig = "Views";
        public string PosterGridSpacingConfig = "Poster Grid Spacing";
        public string ThumbWidthSplitConfig = "Thumb Width Split";
        public string BreadcrumbCountConfig = "Breadcrumb Count";
        public string ShowFanArtonViewsConfig = "Show Fan Art on Views";
        public string ShowInitialFolderBackgroundConfig = "Show Initial Folder Background";
        public string ShowThemeBackgroundConfig = "Show Theme Background";
        public string ShowHDOverlayonPostersConfig = "Show HD Overlay on Posters";
        public string ShowIcononRemoteContentConfig = "Show Icon on Remote Content";
        public string EnableAdvancedCmdsConfig = "Enable Advanced Commands";
        public string MediaTrackingConfig = "Media Tracking";
        public string RememberFolderIndexingConfig = "Remember Folder Indexing";
        public string RememberFolderFiltersConfig = "Remember Folder Filters";
        public string ShowUnwatchedCountConfig = "Show Unplayed Count";
        public string WatchedIndicatoronFoldersConfig = "Played Indicator on Folders";
        public string HighlightUnwatchedItemsConfig = "Highlight Unplayed Items";
        public string WatchedIndicatoronVideosConfig = "Played Indicator on Items";
        public string WatchedIndicatorinDetailViewConfig = "Played Indicator in Detail View";
        public string DefaultToFirstUnwatchedItemConfig = "Default To First Unplayed Item";
        public string GeneralBehaviorConfig = "General Behavior";
        public string AllowNestedMovieFoldersConfig = "Allow Nested Movie Folders";
        public string AutoEnterSingleFolderItemsConfig = "Auto Enter Single Folder Items";
        public string MultipleFileBehaviorConfig = "Multiple File Behavior";
        public string TreatMultipleFilesAsSingleMovieConfig = "Treat Multiple Files As Single Movie";
        public string MultipleFileSizeLimitConfig = "Multiple File Size Limit";
        public string MBThemeConfig = "Media Browser Theme";
        public string VisualThemeConfig = "Visual Theme";
        public string ColorSchemeConfig = "Color Scheme *";
        public string FontSizeConfig = "Font Size *";
        public string RequiresRestartConfig = "* Requires a restart to take effect.";
        public string ThemeSettingsConfig = "Theme Specific Settings";
        public string ShowConfigButtonConfig = "Show Config Button";
        public string AlphaBlendingConfig = "Alpha Blending";
        public string SecurityPINConfig = "Security PIN";
        public string PCUnlockedTxtConfig = "Parental Controls are Temporarily Unlocked.  You cannot change values unless you re-lock.";
        public string RelockBtnConfig = "Re-Lock";
        public string EnableParentalBlocksConfig = "Enable Parental Blocks";
        public string MaxAllowedRatingConfig = "Max Allowed Rating ";
        public string BlockUnratedContentConfig = "Block Unrated Content";
        public string HideBlockedContentConfig = "Hide Blocked Content";
        public string UnlockonPINEntryConfig = "Unlock on PIN Entry";
        public string UnlockPeriodHoursConfig = "Unlock Period (Hours)";
        public string EnterNewPINConfig = "Enter New PIN";
        public string RandomizeBackdropConfig = "Randomize";
        public string RotateBackdropConfig = "Rotate";
        public string UpdateLibraryConfig = "Update Library";
        public string BackdropSettingsConfig = "Backdrop Settings";
        public string BackdropRotationIntervalConfig = "Rotation Time";
        public string BackdropTransitionIntervalConfig = "Transition Time";
        public string BackdropLoadDelayConfig = "Load Delay";
        public string AutoScrollTextConfig = "Auto Scroll Overview";
        public string SortYearsAscConfig = "Sort by Year in Ascending Order";
        public string AutoValidateConfig = "Automatically Validate Items";
        public string SaveLocalMetaConfig = "Save Locally";
        public string HideEmptyFoldersConfig = "Hide Empty Folders";
        public string PluginClassConfig = "Plugin Update Level";
        public string SystemClassConfig = "System Update Level";
        public string CheckForUpdateConfig = "Check for Update";
        public string TreatWatchedAsInProgressConfig = "Make 'Watched' list in-progress";
        public string HttpTimeoutConfig = "Communication Timeout";
        public string EnableDeleteConfig = "Enable Media Delete";
        public string ServerConfig = "Server";
        public string ConnectionConfig = "Connection";
        public string PortConfig = "Port";
        public string WakeServerConfig = "Attempt to Wake";
        public string PluginsConfig = "Plug-ins";
        public string InstalledPluginsConfig = "Installed Plug-ins";
        public string CollapseBoxSetsConfig = "Collapse Movies into Collections";
        public string ConfirmExitConfig = "Confirm Exit";
        public string AutoLogoffConfig = "Enable Auto Logoff";
        public string AutoLogoffTimeOutConfig = "After (mins)";
        public string PlaybackConfig = "Playback";
        public string StreamingConfig = "Streaming";
        public string WarnOnStreamConfig = "Warn if Streaming";
        public string LocalMaxBitrateConfig = "Local Network Max Bitrate (Mb/s)";
        public string RemoteMaxBitrateConfig = "Remote Network Max Bitrate (Mb/s)";
        public string InputActivityTimeoutConfig = "Input Activity Timeout (seconds)";
        public string DefaultSkipSecondsConfig = "Default Skip Forward Amount (seconds)";
        public string DefaultSkipBackSecondsConfig = "Default Skip Back Amount (seconds)";
        public string UseCustomPlayerConfig = "Use Custom Player Interface";
        public string DisableCustomPlayerForDvdConfig = "Disable for DVD Rips";
        public string DisableMcConflictingOperationsConfig = "Disable Functions that Conflict with Media Control";
        public string PlayerConfig = "Player";
        public string ShowNewItemNotificationInPlayerConfig = "Allow New Item Notifications";
        public string ShowChannelsConfig = "Show Channels";
        public string GroupChannelsTogetherConfig = "Group All Together";
        public string OverUnderScanConfig = "Over/Underscan";
        public string BottomConfig = "Bottom";
        public string TopConfig = "Top";
        public string LeftConfig = "Left";
        public string RightConfig = "Right";

        //JIL
        public string ThisWeek = "This Week";
        public string WeekAgo = "A Week Ago";
        public string MonthAgo = "A Month Ago";
        public string SixMonthsAgo = "Six Months Ago";
        public string YearAgo = "A Year Ago";
        public string Earlier = "Earlier";
        public string ThisYear = "This Year";
        public string LastYear = "Last Year";
        public string FiveYearsAgo = "5 Years Ago";
        public string TenYearsAgo = "10 Years Ago";
        public string TwentyYearsAgo = "20 Years Ago";
        public string Longer = "Longer";

        //EHS        
        public string RecentlyWatchedEHS = "last played";
        public string InProgressEHS = "in progress";
        public string RecentlyAddedEHS = "last added";
        public string RecentlyAddedUnwatchedEHS = "last added unplayed";
        public string WatchedEHS = "Played";
        public string AddedEHS = "Added";
        public string UnwatchedEHS = "Unplayed";
        public string AddedOnEHS = "Added on";
        public string OnEHS = "on";
        public string OfEHS = "of";
        public string NoItemsEHS = "No Items To Show";
        public string VariousEHS = "(various)";

        //Context menu
        public string CloseCMenu = "Close";
        public string PlayMenuCMenu = "Play Menu";
        public string ItemMenuCMenu = "Item Menu";
        public string MultiMenuCMenu = "Select Part";
        public string UserMenuCMenu = "Switch User";
        public string PlayAllCMenu = "Play All";
        public string PlayAllFromHereCMenu = "Play All From Here";
        public string ResumeCMenu = "Resume";
        public string MarkUnwatchedCMenu = "Mark Unplayed";
        public string MarkWatchedCMenu = "Mark Played";
        public string AddFavoriteCMenu = "Add Favorite";
        public string RemoveFavoriteCMenu = "Remove Favorite";
        public string ShufflePlayCMenu = "Shuffle Play";
        public string UserMenu = "User Menu";

        //Shortcut Legend
        public string ShortcutsSC = "Remote/Keyboard Shortcuts";
        public string HomeSC = "Ctl-H";
        public string SearchSC = "(yellow) / Ctl-S";
        public string ContextSC = "* / Shft-8";
        public string PlaySC = "(play) / Ctl-P";
        public string WatchedSC = "(clear) Ctl-W";
        public string HomeSCDesc = "Return to Home Screen";
        public string SearchSCDesc = "Search";
        public string ContextSCDesc = "Context Menu for current item";
        public string PlaySCDesc = "Quick Play current item";
        public string WatchedSCDesc = "Toggle Watched Status for current item";

        //Media Detail Page
        public string GeneralDetail = "General";
        public string ActorsDetail = "Actors";
        public string ChaptersDetail = "Chapters";
        public string ArtistsDetail = "Artists";        
        public string PlayDetail = "Play";
        public string ResumeDetail = "Resume";
        public string RefreshDetail = "Refresh";
        public string PlayTrailersDetail = "Trailer";
        public string CacheDetail = "Cache 2 xml";
        public string DeleteDetail = "Delete";
        public string IMDBRatingDetail = "TMDb Rating";
        public string OutOfDetail = "out of";
        public string DirectorDetail = "Director";
        public string ComposerDetail = "Composer";
        public string HostDetail = "Host";
        public string RuntimeDetail = "Runtime";
        public string NextItemDetail = "Next";
        public string PreviousItemDetail = "Previous";
        public string FirstAiredDetail = "First aired";
        public string LastPlayedDetail = "Last played";
        public string TrackNumberDetail = "Track";

        public string DirectedByDetail = "Directed By: ";
        public string WrittenByDetail = "Written By: ";
        public string ComposedByDetail = "Composed By: ";

        //Display Prefs
        public string ViewDispPref = "View";
        public string ViewSearch = "Search";
        public string CoverFlowDispPref = "CoverFlow";
        public string DetailDispPref = "Detail";
        public string PosterDispPref = "Poster";
        public string ThumbDispPref = "Thumb";
        public string ThumbStripDispPref = "Thumb Strip";
        public string ShowLabelsDispPref = "Show Labels";
        public string VerticalScrollDispPref = "Vertical Scroll";
        public string UseBannersDispPref = "Use Banners";
        public string UseCoverflowDispPref = "Use Coverflow Style";
        public string ThumbSizeDispPref = "Thumb Size";
        public string NameDispPref = "Name";
        public string DateDispPref = "Date";
        public string RatingDispPref = "User Rating";
        public string OfficialRatingDispPref = "Rating";
        public string RuntimeDispPref = "Runtime";
        public string UnWatchedDispPref = "Unplayed";
        public string YearDispPref = "Year";
        public string NoneDispPref = "None";
        public string ActorDispPref = "Performer";
        public string GenreDispPref = "Genre";
        public string DirectorDispPref = "Director";
        public string StudioDispPref = "Studio";

        //Dialog boxes
        public string BrokenEnvironmentDial = "Application will now close due to broken MediaCenterEnvironment object, possibly due to 5 minutes of idle time and/or running with TVPack installed.";
        public string InitialConfigDial = "Initial configuration is complete, please restart Media Browser";
        public string DeleteMediaDial = "Are you sure you wish to delete this media item?";
        public string DeleteMediaCapDial = "Delete Confirmation";
        public string DelServerDial = "The item has been deleted on the server.  Changes should be reflected momentarily.";
        public string NotDeletedDial = "Item NOT Deleted.";
        public string NotDeletedCapDial = "Delete Cancelled by User";
        public string NotDelInvalidPathDial = "The selected media item cannot be deleted due to an invalid path. Or you may not have sufficient access rights to perform this command.";
        public string DelFailedDial = "Delete Failed";
        public string NotDelUnknownDial = "The selected media item cannot be deleted due to an unknown error.";
        public string NotDelTypeDial = "The selected media item cannot be deleted due to its Item-Type or you have not enabled this feature in the configuration file.";
        public string FirstTimeDial = "As this is the first time you have run Media Browser please setup the inital configuration";
        public string FirstTimeCapDial = "Configure";
        public string EntryPointErrorDial = "Media Browser could not launch.  Please be sure the server is running, awake and available on the network. ";
        public string EntryPointErrorCapDial = "Entrypoint Error";
        public string CriticalErrorDial = "Media Browser encountered a critical error and had to shut down: ";
        public string CriticalErrorCapDial = "Critical Error";
        public string ClearCacheErrorDial = "An error occured during the clearing of the cache, you may wish to manually clear it from {0} before restarting Media Browser";
        public string RestartMBDial = "Please restart Media Browser";
        public string ClearCacheDial = "Are you sure you wish to clear the cache?\nThis will erase all cached and downloaded information and images.";
        public string ClearCacheCapDial = "Clear Cache";
        public string CacheClearedDial = "Cache Cleared";
        public string ResetConfigDial = "Are you sure you wish to reset all configuration to defaults?";
        public string ResetConfigCapDial = "Reset Configuration";
        public string ConfigResetDial = "Configuration Reset";
        public string UpdateMBDial = "Please visit www.mediabrowser.tv/download to install the new version.";
        public string UpdateMBCapDial = "Update Available";
        public string UpdateMBExtDial = "There is an update available for Media Browser.  Please update Media Browser next time you are at your MediaCenter PC.";
        public string DLUpdateFailDial = "Media Browser will operate normally and prompt you again the next time you load it.";
        public string DLUpdateFailCapDial = "Update Download Failed";
        public string UpdateSuccessDial = "Media Browser must now exit to apply the update.  It will restart automatically when it is done";
        public string UpdateSuccessCapDial = "Update Downloaded";
        public string CustomErrorDial = "Customisation Error";
        public string ConfigErrorDial = "Reset to default?";
        public string ConfigErrorCapDial = "Error in configuration file";
        public string ContentErrorDial = "There was a problem playing the content. Check location exists";
        public string ContentErrorCapDial = "Content Error";
        public string CannotMaximizeDial = "We can not maximize the window! This is a known bug with Windows 7 and TV Pack, you will have to restart Media Browser!";
        public string IncorrectPINDial = "Incorrect PIN Entered";
        public string ContentProtected = "Content Protected";
        public string CantChangePINDial = "Cannot Change PIN";
        public string LibraryUnlockedDial = "Library Temporarily Unlocked.  Will Re-Lock in {0} Hour(s) or on Application Re-Start";
        public string LibraryUnlockedCapDial = "Unlock";
        public string PINChangedDial = "PIN Successfully Changed";
        public string PINChangedCapDial = "PIN Change";
        public string EnterPINToViewDial = "Please Enter PIN to View Protected Content";
        public string EnterPINToPlayDial = "Please Enter PIN to Play Protected Content";
        public string EnterCurrentPINDial = "Please Enter CURRENT PIN.";
        public string EnterNewPINDial = "Please Enter NEW PIN (exactly 4 digits).";
        public string EnterPINDial = "Please Enter PIN to Unlock Library";
        public string NoContentDial = "No Content that can be played in this context.";
        public string FontsMissingDial = "CustomFonts.mcml as been patched with missing values";
        public string StyleMissingDial = "{0} has been patched with missing values";
        public string ManualRefreshDial = "The server has begun a scan of your library. Changes will be reflected in MB Classic when it is finished.";
        public string ForcedRebuildDial = "Your library is currently being migrated by the service.  The service will re-start when it is finished and you may then run Media Browser.";
        public string ForcedRebuildCapDial = "Library Migration";
        public string RefreshFailedDial = "The last service refresh process failed.  Please run a manual refresh from the service.";
        public string RefreshFailedCapDial = "Service Refresh Failed";
        public string RebuildNecDial = "This version of Media Browser requires a re-build of your library.  It has started automatically in the service.  Some information may be incomplete until this process finishes.";
        public string MigrateNecDial = "This version of Media Browser requires a migration of your library.  It has started automatically in the service.  The service will restart when it is complete and you may then run Media Browser.";
        public string RebuildFailedDial = "There was an error attempting to tell the service to re-build your library.  Please run the service and do a manual refresh with the cache clear options selected.";
        public string MigrateFailedDial = "There was an error attempting to tell the service to re-build your library.  Please run the service and do a manual refresh with the cache clear options selected.";
        public string RefreshFolderDial = "Refresh all contents too?";
        public string RefreshFolderCapDial = "Refresh Folder";

        //Generic
        public string Restartstr = "Restart";
        public string Errorstr = "Error";
        public string Playstr = "Play";
        public string MinutesStr = "mins"; //Minutes abbreviation
        public string HoursStr = "hrs"; //Hours abbreviation
        public string EndsStr = "Ends"; 
        public string KBsStr = "Kbps";  //Kilobytes per second
        public string FrameRateStr = "fps";  //Frames per second
        public string AtStr = "at";  //x at y, e.g. 1920x1080 at 25 fps
        public string Rated = "Rated";
        public string Or = "Or ";
        public string Lower = "Lower";
        public string Higher = "Higher";
        public string Search = "Search";
        public string Cancel = "Cancel";
        public string TitleContains = "Title Contains ";
        public string Any = "Any";
        public string Season = "Season";
        public string Favorite = "Favorite";
        public string SimilarTo = "Similar to";
        public string PluginCatalog = "Plugin Catalog";
        public string Installed = "Installed";
        public string NotInstalled = "Not Installed";
        public string Close = "Close";
        public string Install = "Install";
        public string Remove = "Remove";
        public string Register = "Register";
        public string Rate = "Rate:";
        public string Stars = "Stars";
        public string Update = "Update";
        public string Recommend = "Recommend";
        public string UpgradeInfo = "Upgrade Info";
        public string LastUpdateInfo = "Last Version Info";
        public string UpdateAll = "Update All";
        public string PluginUpdatesAvailQ = "Some of your plug-ins have updates.  Go to Configuration?";
        public string Overview = "Overview";
        public string SwitchTo = "Switch To...";
        public string AlsoHere = "Also Here";
        public string Part = "Part";
        public string AllParts = "All Parts";
        public string ExitApplication = "Exit MB Classic";
        public string Logout = "Logout";
        public string SleepMachine = "Put Machine to Sleep";
        public string StartingLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public string ChannelsFolderName = "Channels";
        public string DirectSkipString = ">| or |< to skip seconds 'Play' to go to exact point";

        //Search
        public string IncludeNested = "Include Subfolders";
        public string UnwatchedOnly = "Include Only Unwatched";
        public string FilterByRated = "Filter by Rating";

        //Profiler
        public string WelcomeProf = "Welcome to Media Browser";
        public string ProfilerTimeProf = "{1} took {2} seconds.";
        public string RefreshProf = "Refresh";
        public string SetWatchedProf = "Set Played {0}";
        public string RefreshFolderProf = "Refresh Folder and all Contents of";
        public string ClearWatchedProf = "Clear Played {0}";
        public string FullRefreshProf = "Full Library Refresh";
        public string FullValidationProf = "Full Library Validation";
        public string FastRefreshProf = "Fast Metadata refresh";
        public string SlowRefresh = "Slow Metadata refresh";
        public string ImageRefresh = "Image refresh";
        public string PluginUpdateProf = "An update is available for plug-in {0}";
        public string NoPluginUpdateProf = "No Plugin Updates Currently Available.";
        public string LibraryUnLockedProf = "Library Temporarily UnLocked. Will Re-Lock in {0} Hour(s)";
        public string LibraryReLockedProf = "Library Re-Locked";

        //Messages
        public string FullRefreshMsg = "Updating Media Library...";
        public string FullRefreshFinishedMsg = "Library update complete";



        public BaseStrings() //for the serializer
        {
        }

        public static BaseStrings FromFile(string file)
        {
            BaseStrings s = new BaseStrings();
            XmlSettings<BaseStrings> settings = XmlSettings<BaseStrings>.Bind(s, file);

            Logger.ReportInfo("Using String Data from " + file);

            if (VERSION != s.Version && Path.GetFileName(file).ToLower() == ENFILE)
            {
                //only re-save the english version as that is the one defined internally
                File.Delete(file);
                s = new BaseStrings();
                settings = XmlSettings<BaseStrings>.Bind(s, file);
            }
            return s;
        }
    }
}
