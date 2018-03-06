using System;
using System.Collections.Generic;
using System.Reflection;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Updates;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Library;
using MediaBrowser.Attributes;
using MediaBrowser.Library.Logging;

namespace MediaBrowser
{

    public class Config : IModelItem
    {
        private ConfigData Data { get { return Kernel.Instance.ConfigData; } }
        private CommonConfigData CommonData { get { return Kernel.Instance.CommonConfigData; } }
        private ServerConfiguration ServerData { get { return Kernel.ServerConfig; }}

        public bool AlwaysShowDetailsPage
        {
            get { return this.Data.AlwaysShowDetailsPage; }
            set { if (this.Data.AlwaysShowDetailsPage != value) { this.Data.AlwaysShowDetailsPage = value; Save(); FireConfigPropertyChanged("AlwaysShowDetailsPage"); } }
        }

        //public int ParentalUnlockPeriod
        //{
        //    get { return this.data.ParentalUnlockPeriod; }
        //    set { if (this.data.ParentalUnlockPeriod != value) { this.data.ParentalUnlockPeriod = value; Save(); FireConfigPropertyChanged("ParentalUnlockPeriod"); } }
        //}

        public bool ParentalControlEnabled
        {
            get { return false; }

            set { }
        }
        public bool ParentalControlUnlocked
        {
            get { return false; }
            set {  }
        }
        public bool EnableRootPage
        {
            get { return this.Data.EnableRootPage; }
            set { if (this.Data.EnableRootPage != value) { this.Data.EnableRootPage = value; Save(); FireConfigPropertyChanged("EnableRootPage"); } }
        }

        public bool ProcessBanners
        {
            get { return this.Data.ProcessBanners; }
            set { if (this.Data.ProcessBanners != value) { this.Data.ProcessBanners = value; Save(); FireConfigPropertyChanged("ProcessBanners"); } }
        }

        public bool ProcessBackdrops
        {
            get { return this.Data.ProcessBackdrops; }
            set { if (this.Data.ProcessBackdrops != value) { this.Data.ProcessBackdrops = value; Save(); FireConfigPropertyChanged("ProcessBackdrops"); } }
        }

        public bool AskIncludeChildrenRefresh
        {
            get { return this.Data.AskIncludeChildrenRefresh; }
            set { if (this.Data.AskIncludeChildrenRefresh != value) { this.Data.AskIncludeChildrenRefresh = value; Save(); FireConfigPropertyChanged("AskIncludeChildrenRefresh"); } }
        }

        public bool DefaultIncludeChildrenRefresh
        {
            get { return this.Data.DefaultIncludeChildrenRefresh; }
            set { if (this.Data.DefaultIncludeChildrenRefresh != value) { this.Data.DefaultIncludeChildrenRefresh = value; Save(); FireConfigPropertyChanged("DefaultIncludeChildrenRefresh"); } }
        }

        public bool IsFirstRun
        {
            get { return this.CommonData.IsFirstRun; }
            set { if (this.CommonData.IsFirstRun != value) { this.CommonData.IsFirstRun = value; Save(); FireConfigPropertyChanged("HasBeenConfigured"); } }
        }

        public bool CacheAllImagesInMemory
        {
            get { return this.Data.CacheAllImagesInMemory; }
            set { if (this.Data.CacheAllImagesInMemory != value) { this.Data.CacheAllImagesInMemory = value; Save(); FireConfigPropertyChanged("CacheAllImagesInMemory"); } }
        }

        public bool HideEmptyFolders
        {
            get { return this.Data.HideEmptyFolders; }
            set { if (this.Data.HideEmptyFolders != value) { this.Data.HideEmptyFolders = value; Save(); FireConfigPropertyChanged("HideEmptyFolders"); } }
        }

        public bool UseExitMenu
        {
            get { return this.Data.UseExitMenu; }
            set { if (this.Data.UseExitMenu != value) { this.Data.UseExitMenu = value; Save(); FireConfigPropertyChanged("UseExitMenu"); } }
        }

        [Comment(@"The current version of MB - will be the last version te first time we run so we can do something")]
        public string MBVersion
        {
            get { return this.CommonData.MBVersion; }
            set { if (this.CommonData.MBVersion != value) { this.CommonData.MBVersion = value; Save(); FireConfigPropertyChanged("MBVersion"); } }
        }
        [Comment("Synchronize the view for similar folder types")]
        public bool EnableSyncViews
        {
            get { return this.Data.EnableSyncViews; }
            set { if (this.Data.EnableSyncViews != value) { this.Data.EnableSyncViews = value; Save(); FireConfigPropertyChanged("EnableSyncViews"); } }

        }

        public bool ShowFavoritesCollection
        {
            get { return this.Data.ShowFavoritesCollection; }
            set { if (this.Data.ShowFavoritesCollection != value) { this.Data.ShowFavoritesCollection = value; Save(); FireConfigPropertyChanged("ShowFavoritesCollection"); } }
        }

        public string FavoriteFolderName
        {
            get { return this.Data.FavoriteFolderName; }
            set { if (this.Data.FavoriteFolderName != value) { this.Data.FavoriteFolderName = value; Save(); FireConfigPropertyChanged("FavoriteFolderName"); } }
        }

        public bool ShowMovieGenreCollection
        {
            get { return this.Data.ShowMovieGenreCollection; }
            set { if (this.Data.ShowMovieGenreCollection != value) { this.Data.ShowMovieGenreCollection = value; Save(); FireConfigPropertyChanged("ShowMovieGenreCollection"); } }
        }

        public string MovieGenreFolderName
        {
            get { return this.Data.MovieGenreFolderName; }
            set { if (this.Data.MovieGenreFolderName != value) { this.Data.MovieGenreFolderName = value; Save(); FireConfigPropertyChanged("MovieGenreFolderName"); } }
        }

        public bool ShowMusicGenreCollection
        {
            get { return this.Data.ShowMusicGenreCollection; }
            set { if (this.Data.ShowMusicGenreCollection != value) { this.Data.ShowMusicGenreCollection = value; Save(); FireConfigPropertyChanged("ShowMusicGenreCollection"); } }
        }

        public bool ShowMusicAlbumCollection
        {
            get { return this.Data.ShowMusicAlbumCollection; }
            set { if (this.Data.ShowMusicAlbumCollection != value) { this.Data.ShowMusicAlbumCollection = value; Save(); FireConfigPropertyChanged("ShowMusicAlbumCollection"); } }
        }

        public string MusicGenreFolderName
        {
            get { return this.Data.MusicGenreFolderName; }
            set { if (this.Data.MusicGenreFolderName != value) { this.Data.MusicGenreFolderName = value; Save(); FireConfigPropertyChanged("MusicGenreFolderName"); } }
        }

        public bool GroupAlbumsByArtist
        {
            get { return this.Data.GroupAlbumsByArtist; }
            set { if (this.Data.GroupAlbumsByArtist != value) { this.Data.GroupAlbumsByArtist = value; Save(); FireConfigPropertyChanged("GroupAlbumsByArtist"); } }
        }

        public bool ShowChannels
        {
            get { return this.Data.ShowChannels; }
            set { if (this.Data.ShowChannels != value) { this.Data.ShowChannels = value; Save(); FireConfigPropertyChanged("ShowChannels"); } }
        }

        public bool GroupChannelsTogether
        {
            get { return this.Data.GroupChannelsTogether; }
            set { if (this.Data.GroupChannelsTogether != value) { this.Data.GroupChannelsTogether = value; Save(); FireConfigPropertyChanged("GroupChannelsTogether"); } }
        }

        public bool ShowNewItemNotification
        {
            get { return this.Data.ShowNewItemNotification; }
            set { if (this.Data.ShowNewItemNotification != value) { this.Data.ShowNewItemNotification = value; Save(); FireConfigPropertyChanged("ShowNewItemNotification"); } }
        }

        public bool ShowNewItemNotificationInPlayer
        {
            get { return this.Data.ShowNewItemNotificationInPlayer; }
            set { if (this.Data.ShowNewItemNotificationInPlayer != value) { this.Data.ShowNewItemNotificationInPlayer = value; Save(); FireConfigPropertyChanged("ShowNewItemNotificationInPlayer"); } }
        }

        [Comment("Dim all unselected posters in poster and thumbstrib views")]
        public bool DimUnselectedPosters
        {
            get { return this.Data.DimUnselectedPosters; }
            set { if (this.Data.DimUnselectedPosters != value) { this.Data.DimUnselectedPosters = value; Save(); FireConfigPropertyChanged("DimUnselectedPosters"); } }
        }

        //Leave for backward compat
        public string ImageByNameLocation
        {
            get { return ""; }
            set {  }
        }

        [Comment(@"Enables you to scan the display to cope with overscan issue, parameter should be of the for x,y,z scaling factors")]
        public Vector3 OverScanScaling
        {
            get { return this.CommonData.OverScanScaling.ToMediaCenterVector3(); }
            set
            {
                if (this.CommonData.OverScanScaling.ToMediaCenterVector3() != value)
                {
                    this.CommonData.OverScanScaling = MediaBrowser.Code.ShadowTypes.Vector3.FromMediaCenterVector3(value);
                    Save();
                    FireConfigPropertyChanged("OverScanScaling");
                }
            }
        }
        [Comment("Defines padding to apply round the edge of the screen to cope with overscan issues")]
        public Inset OverScanPadding
        {
            get { return this.CommonData.OverScanPadding.ToMediaCenterInset(); }
            set
            {
                if (this.CommonData.OverScanPadding.ToMediaCenterInset() != value)
                {
                    this.CommonData.OverScanPadding = MediaBrowser.Code.ShadowTypes.Inset.FromMediaCenterInset(value);
                    Save();
                    FireConfigPropertyChanged("OverScanPadding");
                }
            }
        }

        public int OverScanPaddingBottom
        {
            get { return OverScanPadding.Bottom; }
            set
            {
                var current = OverScanPadding;
                current.Bottom = value;
                OverScanPadding = current;
                FireConfigPropertyChanged("OverScanPaddingBottom");
            }
        }

        public int OverScanPaddingTop
        {
            get { return OverScanPadding.Top; }
            set
            {
                var current = OverScanPadding;
                current.Top = value;
                OverScanPadding = current;
                FireConfigPropertyChanged("OverScanPaddingTop");
            }
        }

        public int OverScanPaddingLeft
        {
            get { return OverScanPadding.Left; }
            set
            {
                var current = OverScanPadding;
                current.Left = value;
                OverScanPadding = current;
                FireConfigPropertyChanged("OverScanPaddingLeft");
            }
        }

        public int OverScanPaddingRight
        {
            get { return OverScanPadding.Right; }
            set
            {
                var current = OverScanPadding;
                current.Right = value;
                OverScanPadding = current;
                FireConfigPropertyChanged("OverScanPaddingRight");
            }
        }

        [Comment(@"Enables the writing of trace log files in a production environment to assist with problem solving")]
        public bool EnableTraceLogging
        {
            get { return this.CommonData.EnableTraceLogging; }
            set { if (this.CommonData.EnableTraceLogging != value) { this.CommonData.EnableTraceLogging = value; Save(); FireConfigPropertyChanged("EnableTraceLogging"); } }
        }
        [Comment(@"The default size of posters before change are made to the view settings")]
        public Size DefaultPosterSize
        {
            get
            {
                return this.Data.DefaultPosterSize.ToMediaCenterSize();
            }
            set
            {
                if (this.Data.DefaultPosterSize.ToMediaCenterSize() != value)
                {
                    this.Data.DefaultPosterSize = MediaBrowser.Code.ShadowTypes.Size.FromMediaCenterSize(value);
                    Save();
                    FireConfigPropertyChanged("DefaultPosterSize");
                }
            }
        }

        public int DefaultPosterSizeCfg
        {
            get { return this.DefaultPosterSize.Width; }
            set { this.DefaultPosterSize = new Size(value, value); }
        }

        public int SlideShowInterval
        {
            get { return this.Data.SlideShowInterval; }
            set { if (this.Data.SlideShowInterval != value) { this.Data.SlideShowInterval = value; Save(); FireConfigPropertyChanged("SlideShowInterval"); } }
        }

        [Comment("Controls the space between items in the poster and thumb strip views")]
        public Size GridSpacing
        {
            get { return this.Data.GridSpacing.ToMediaCenterSize(); }
            set
            {
                if (this.Data.GridSpacing.ToMediaCenterSize() != value)
                {
                    this.Data.GridSpacing = MediaBrowser.Code.ShadowTypes.Size.FromMediaCenterSize(value);
                    Save();
                    FireConfigPropertyChanged("GridSpacing");
                }
            }
        }

        public int GridSpacingCfg
        {
            get { return GridSpacing.Width; }
            set { this.GridSpacing = new Size(value, value); }
        }

        public int ThumbStripPosterWidth
        {
            get { return this.Data.ThumbStripPosterWidth; }
            set { if (this.Data.ThumbStripPosterWidth != value) { this.Data.ThumbStripPosterWidth = value; Save(); FireConfigPropertyChanged("ThumbStripPosterWidth"); } }
        }

        public bool RememberIndexing
        {
            get { return this.Data.RememberIndexing; }
            set { if (this.Data.RememberIndexing != value) { this.Data.RememberIndexing = value; Save(); FireConfigPropertyChanged("RememberIndexing"); } }
        }

        public bool TreatWatchedAsInProgress
        {
            get { return this.Data.TreatWatchedAsInProgress; }
            set { if (this.Data.TreatWatchedAsInProgress != value) { this.Data.TreatWatchedAsInProgress = value; Save(); Application.CurrentInstance.ClearAllQuickLists(); FireConfigPropertyChanged("TreatWatchedAsInProgress"); } }
        }

        public bool ShowIndexWarning
        {
            get { return this.Data.ShowIndexWarning; }
            set { if (this.Data.ShowIndexWarning != value) { this.Data.ShowIndexWarning = value; Save(); FireConfigPropertyChanged("ShowIndexWarning"); } }
        }

        public bool UseCustomStreamingUrl
        {
            get { return this.CommonData.UseCustomStreamingUrl; }
        }

        public string CustomStreamingUrl
        {
            get { return this.CommonData.CustomStreamingUrl; }
        }

        public double IndexWarningThreshold
        {
            get { return this.Data.IndexWarningThreshold; }
            set { if (this.Data.IndexWarningThreshold != value) { this.Data.IndexWarningThreshold = value; Save(); FireConfigPropertyChanged("IndexWarningThreshold"); } }
        }

        [Comment(@"Controls the maximum difference between the actual aspect ration of a poster image and the thumbnails being displayed to allow the application to stretch the image non-proportionally.
            x = Abs( (image width/ image height) - (display width / display height) )
            if x is less than the configured value the imae will be stretched non-proportionally to fit the display size")]
        public float MaximumAspectRatioDistortion
        {
            get { return this.Data.MaximumAspectRatioDistortion; }
            set { if (this.Data.MaximumAspectRatioDistortion != value) { this.Data.MaximumAspectRatioDistortion = value; Save(); FireConfigPropertyChanged("MaximumAspectRatioDistortion"); } }
        }
        [Comment(@"Enable transcode 360 support on extenders")]
        public bool EnableTranscode360
        {
            get { return this.CommonData.EnableTranscode360; }
            set { if (this.CommonData.EnableTranscode360 != value) { this.CommonData.EnableTranscode360 = value; Save(); FireConfigPropertyChanged("EnableTranscode360"); } }
        }
        [Comment(@"A lower case comma delimited list of types the extender supports natively. Example: .dvr-ms,.wmv")]
        public string ExtenderNativeTypes
        {
            get { return this.CommonData.ExtenderNativeTypes; }
            set { if (this.CommonData.ExtenderNativeTypes != value) { this.CommonData.ExtenderNativeTypes = value; Save(); FireConfigPropertyChanged("ExtenderNativeTypes"); } }
        }
        [Comment("ShowThemeBackground [Default Value - False]\n\tTrue: Enables transparent background.\n\tFalse: Use default Video Browser background.")]
        public bool ShowThemeBackground
        {
            get { return this.Data.ShowThemeBackground; }
            set { if (this.Data.ShowThemeBackground != value) { this.Data.ShowThemeBackground = value; Save(); FireConfigPropertyChanged("ShowThemeBackground"); } }
        }
        [Comment("Example. If set to true the following will be treated as a movie and an automatic playlist will be created.\n\tIndiana Jones / Disc 1 / a.avi\n\tIndiana Jones / Disc 2 / b.avi")]
        public bool EnableNestedMovieFolders
        {
            get { return this.Data.EnableNestedMovieFolders; }
            set { if (this.Data.EnableNestedMovieFolders != value) { this.Data.EnableNestedMovieFolders = value; Save(); FireConfigPropertyChanged("EnableNestedMovieFolders"); } }
        }
        [Comment("Example. If set to true the following will be treated as a movie and an automatic playlist will be created.\n\tIndiana Jones / a.avi\n\tIndiana Jones / b.avi (This only works for 2 videos (no more))\n**Setting this to false will override EnableNestedMovieFolders if that is enabled.**")]
        public bool EnableMoviePlaylists
        {
            get { return this.Data.EnableMoviePlaylists; }
            set { if (this.Data.EnableMoviePlaylists != value) { this.Data.EnableMoviePlaylists = value; Save(); FireConfigPropertyChanged("EnableMoviePlaylists"); } }
        }
        [Comment("Limit to the number of video files that willbe assumed to be a single movie and create a playlist for")]
        public int PlaylistLimit
        {
            get { return this.Data.PlaylistLimit; }
            set { if (this.Data.PlaylistLimit != value) { this.Data.PlaylistLimit = value; Save(); FireConfigPropertyChanged("PlaylistLimit"); } }
        }
        [Comment("The starting folder for video browser. By default its set to MyVideos.\nCan be set to a folder for example c:\\ or a virtual folder for example c:\\folder.vf")]
        public string InitialFolder
        {
            get { return this.Data.InitialFolder; }
            set { if (this.Data.InitialFolder != value) { this.Data.InitialFolder = value; Save(); FireConfigPropertyChanged("InitialFolder"); } }
        }
        [Comment(@"Flag for auto-updates.  True will auto-update, false will not.")]
        public bool EnableUpdates
        {
            get { return this.CommonData.EnableUpdates; }
            set { if (this.CommonData.EnableUpdates != value) { this.CommonData.EnableUpdates = value; Save(); FireConfigPropertyChanged("EnableUpdates"); } }
        }
        public bool EnableSilentUpdates
        {
            get { return this.CommonData.EnableSilentUpdates; }
            set { if (this.CommonData.EnableSilentUpdates != value) { this.CommonData.EnableSilentUpdates = value; Save(); FireConfigPropertyChanged("EnableSilentUpdates"); } }
        }
        public string SystemUpdateClass
        {
            get { return this.CommonData.SystemUpdateClass.ToString(); }
            set { if (this.CommonData.SystemUpdateClass.ToString() != value) { this.CommonData.SystemUpdateClass = (PackageVersionClass)Enum.Parse(typeof(PackageVersionClass), value); Save(); FireConfigPropertyChanged("SystemUpdateClass"); } }
        }
        public string PluginUpdateClass
        {
            get { return this.CommonData.PluginUpdateClass.ToString(); }
            set { if (this.CommonData.PluginUpdateClass.ToString() != value) { this.CommonData.PluginUpdateClass = (PackageVersionClass)Enum.Parse(typeof(PackageVersionClass), value); Save(); FireConfigPropertyChanged("PluginUpdateClass"); } }
        }
        [Comment(@"Set the location of the Daemon Tools binary..")]
        public string DaemonToolsLocation
        {
            get { return this.CommonData.DaemonToolsLocation; }
            set { if (this.CommonData.DaemonToolsLocation != value) { this.CommonData.DaemonToolsLocation = value; Save(); FireConfigPropertyChanged("DaemonToolsLocation"); } }
        }
        [Comment(@"The drive letter of the Daemon Tools virtual drive.")]
        public string DaemonToolsDrive
        {
            get { return this.CommonData.DaemonToolsDrive; }
            set { if (this.CommonData.DaemonToolsDrive != value) { this.CommonData.DaemonToolsDrive = value; Save(); FireConfigPropertyChanged("DaemonToolsDrive"); } }
        }
        [Comment("Flag for alphanumeric sorting.  True will use alphanumeric sorting, false will use alphabetic sorting.\nNote that the sorting algorithm is case insensitive.")]
        public bool EnableAlphanumericSorting
        {
            get { return this.Data.EnableAlphanumericSorting; }
            set { if (this.Data.EnableAlphanumericSorting != value) { this.Data.EnableAlphanumericSorting = value; Save(); FireConfigPropertyChanged("EnableAlphanumericSorting"); } }
        }
        [Comment(@"Enables the showing of tick in the list view for files that have been watched")]
        public bool EnableListViewTicks
        {
            get { return this.Data.EnableListViewTicks; }
            set { if (this.Data.EnableListViewTicks != value) { this.Data.EnableListViewTicks = value; Save(); FireConfigPropertyChanged("EnableListViewTicks"); } }
        }
        [Comment(@"Enables the showing of watched shows in a different color in the list view.")]
        public bool EnableListViewWatchedColor
        {
            get { return this.Data.EnableListViewWatchedColor; }
            set { if (this.Data.EnableListViewWatchedColor != value) { this.Data.EnableListViewWatchedColor = value; Save(); FireConfigPropertyChanged("EnableListViewWatchedColor"); } }
        }
         [Comment(@"Enables the showing of watched shows in a different color in the list view (Transparent disables it)")]
         public Colors ListViewWatchedColor
         {
             get
             {
                 return (Colors)(int)this.Data.ListViewWatchedColor;
             }
             set { if ((int)this.Data.ListViewWatchedColor != (int)value) { this.Data.ListViewWatchedColor = (MediaBrowser.Code.ShadowTypes.Colors)(int)value; Save(); FireConfigPropertyChanged("ListViewWatchedColor"); FireConfigPropertyChanged("ListViewWatchedColorMcml"); } }
         }
         public Color ListViewWatchedColorMcml
         {
             get { return new Color(this.ListViewWatchedColor); }
         }
        public bool ShowUnwatchedCount
        {
            get { return this.Data.ShowUnwatchedCount; }
            set { if (this.Data.ShowUnwatchedCount != value) { this.Data.ShowUnwatchedCount = value; Save(); FireConfigPropertyChanged("ShowUnwatchedCount"); } }
        }

        public bool ShowUnwatchedIndicator
        {
            get { return this.Data.ShowUnwatchedIndicator; }
            set { if (this.Data.ShowUnwatchedIndicator != value) { this.Data.ShowUnwatchedIndicator = value; Save(); FireConfigPropertyChanged("ShowUnwatchedIndicator"); } }
        }

        public bool ShowWatchedTickOnFolders
        {
            get { return this.Data.ShowWatchedTickOnFolders; }
            set { if (this.Data.ShowWatchedTickOnFolders != value) { this.Data.ShowWatchedTickOnFolders = value; Save(); FireConfigPropertyChanged("ShowWatchedTickOnFolders"); } }
        }

        public bool ShowWatchTickInPosterView
        {
            get { return this.Data.ShowWatchTickInPosterView; }
            set { if (this.Data.ShowWatchTickInPosterView != value) { this.Data.ShowWatchTickInPosterView = value; Save(); FireConfigPropertyChanged("ShowWatchTickInPosterView"); } }
        }

        public bool ShowHDIndicatorOnPosters
        {
            get { return this.Data.ShowHDIndicatorOnPosters; }
            set { if (this.Data.ShowHDIndicatorOnPosters != value) { this.Data.ShowHDIndicatorOnPosters = value; Save(); FireConfigPropertyChanged("ShowHDIndicatorOnPosters"); } }
        }

        public bool ShowRemoteIndicatorOnPosters
        {
            get { return this.Data.ShowRemoteIndicatorOnPosters; }
            set { if (this.Data.ShowRemoteIndicatorOnPosters != value) { this.Data.ShowRemoteIndicatorOnPosters = value; Save(); FireConfigPropertyChanged("ShowRemoteIndicatorOnPosters"); } }
        }

        public bool ExcludeRemoteContentInSearch
        {
            get { return this.Data.ExcludeRemoteContentInSearch; }
            set { if (this.Data.ExcludeRemoteContentInSearch != value) { this.Data.ExcludeRemoteContentInSearch = value; Save(); FireConfigPropertyChanged("ExcludeRemoteContentInSearch"); } }
        }

        [Comment("Enables the views to default to the first unwatched item in a folder of movies or tv shows")]
        public bool DefaultToFirstUnwatched
        {
            get { return this.Data.DefaultToFirstUnwatched; }
            set { if (this.Data.DefaultToFirstUnwatched != value) { this.Data.DefaultToFirstUnwatched = value; Save(); FireConfigPropertyChanged("DefaultToFirstUnwatched"); } }
        }
        [Comment("When navigating, if only a single folder exists, enter it.")]
        public bool AutoEnterSingleDirs
        {
            get { return this.Data.AutoEnterSingleDirs; }
            set { if (this.Data.AutoEnterSingleDirs != value) { this.Data.AutoEnterSingleDirs = value; Save(); FireConfigPropertyChanged("AutoEnterSingleDirs"); } }
        }
        [Comment(@"Indicates that files with a date stamp before this date should be assumed to have been watched for the purpose of ticking them off.")]
        public DateTime AssumeWatchedBefore
        {
            get { return this.Data.AssumeWatchedBefore; }
            set { if (this.Data.AssumeWatchedBefore != value) { this.Data.AssumeWatchedBefore = value; Save(); FireConfigPropertyChanged("AssumeWatchedBefore"); FireConfigPropertyChanged("AssumeWatchedBeforeStr"); } }
        }

        public bool UseLegacyFolders
        {
            get { return false; }
            set { if (this.Data.UseLegacyFolders != value) { this.Data.UseLegacyFolders = value; Save(); FireConfigPropertyChanged("UseLegacyFolders"); } }
        }

        public int ServerPort
        {
            get { return this.CommonData.ServerPort; }
            set { if (this.CommonData.ServerPort != value) { this.CommonData.ServerPort = value; Save(); FireConfigPropertyChanged("ServerPort"); } }
        }

        public bool FindServerAutomatically
        {
            get { return this.CommonData.FindServerAutomatically; }
            set { if (this.CommonData.FindServerAutomatically != value) { this.CommonData.FindServerAutomatically = value;
                CommonData.ServerAddress = Kernel.ApiClient.ServerHostName; Save(); FireConfigPropertyChanged("FindServerAutomatically"); } }
        }

        public bool SavePassword
        {
            get { return this.CommonData.SavePassword; }
            set
            {
                if (this.CommonData.SavePassword != value)
                {
                    this.CommonData.SavePassword = value;
                    {
                        CommonData.AutoLogonPw =  value ? Kernel.CurrentUser.PwHash : null;
                        Save();
                        FireConfigPropertyChanged("SavePassword");
                    }
                }

            }
        }

        public bool WakeServer
        {
            get { return this.CommonData.WakeServer; }
            set
            {
                if (this.CommonData.WakeServer != value) { this.CommonData.WakeServer = value; Save(); FireConfigPropertyChanged("WakeServer"); }
            }
        }

        public bool UseCustomPlayerInterface
        {
            get { return this.Data.UseCustomPlayerInterface; }
            set
            {
                if (this.Data.UseCustomPlayerInterface != value) { this.Data.UseCustomPlayerInterface = value; Save(); FireConfigPropertyChanged("UseCustomPlayerInterface"); }
            }
        }

        public bool AllowCinemaMode
        {
            get { return this.Data.AllowCinemaMode; }
            set
            {
                if (this.Data.AllowCinemaMode != value) { this.Data.AllowCinemaMode = value; Save(); FireConfigPropertyChanged("AllowCinemaMode"); }
            }
        }

        public bool ShowPauseIndicator
        {
            get { return this.Data.ShowPauseIndicator; }
            set
            {
                if (this.Data.ShowPauseIndicator != value) { this.Data.ShowPauseIndicator = value; Save(); FireConfigPropertyChanged("ShowPauseIndicator"); }
            }
        }

        public bool DisableMcConflictingOperations
        {
            get { return this.CommonData.DisableMcConflictingOperations; }
            set
            {
                if (this.CommonData.DisableMcConflictingOperations != value) { this.CommonData.DisableMcConflictingOperations = value; Save(); FireConfigPropertyChanged("DisableMcConflictingOperations"); }
            }
        }

        public int InputActivityTimeout
        {
            get { return this.Data.InputActivityTimeout; }
            set
            {
                if (this.Data.InputActivityTimeout != value) { this.Data.InputActivityTimeout = value; Save();
                    Application.CurrentInstance.ActivityTimerInterval = value * 1000; FireConfigPropertyChanged("InputActivityTimeout"); }
            }
        }

        public int DefaultSkipSeconds
        {
            get { return this.Data.DefaultSkipSeconds; }
            set
            {
                if (this.Data.DefaultSkipSeconds != value) { this.Data.DefaultSkipSeconds = value; Save(); FireConfigPropertyChanged("DefaultSkipSeconds"); }
            }
        }

        public int DefaultSkipBackSeconds
        {
            get { return this.Data.DefaultSkipBackSeconds; }
            set
            {
                if (this.Data.DefaultSkipBackSeconds != value) { this.Data.DefaultSkipBackSeconds = value; Save(); FireConfigPropertyChanged("DefaultSkipBackSeconds"); }
            }
        }

        public bool DisableCustomPlayerForDvd
        {
            get { return this.CommonData.DisableCustomPlayerForDvd; }
            set { if (this.CommonData.DisableCustomPlayerForDvd != value) { this.CommonData.DisableCustomPlayerForDvd = value; Save(); FireConfigPropertyChanged("DisableCustomPlayerForDvd"); } }
        }

        public bool WarnOnStream
        {
            get { return this.CommonData.WarnOnStream; }
            set
            {
                if (this.CommonData.WarnOnStream != value) { this.CommonData.WarnOnStream = value; Save(); FireConfigPropertyChanged("WarnOnStream"); }
            }
        }

        public int LocalMaxBitrate
        {
            get { return this.CommonData.LocalMaxBitrate; }
            set { if (this.CommonData.LocalMaxBitrate != value) { this.CommonData.LocalMaxBitrate = value; Save(); FireConfigPropertyChanged("LocalMaxBitrate"); } }
        }

        public int RemoteMaxBitrate
        {
            get { return this.CommonData.RemoteMaxBitrate; }
            set { if (this.CommonData.RemoteMaxBitrate != value) { this.CommonData.RemoteMaxBitrate = value; Save(); FireConfigPropertyChanged("RemoteMaxBitrate"); } }
        }

        public string AssumeWatchedBeforeStr
        {
            get { return this.AssumeWatchedBefore.ToString("MMM yyyy"); }
        }

        public void IncrementAssumeWatched()
        {
            this.AssumeWatchedBefore = this.AssumeWatchedBefore.AddMonths(1);
        }

        public void DecrementAssumeWatched()
        {
            this.AssumeWatchedBefore = this.AssumeWatchedBefore.AddMonths(-1);
        }

        public bool InheritDefaultView
        {
            get { return this.Data.InheritDefaultView; }
            set { if (this.Data.InheritDefaultView != value) { this.Data.InheritDefaultView = value; Save(); FireConfigPropertyChanged("InheritDefaultView"); } }
        }

        public bool CollapseBoxSets
        {
            get { return this.Data.CollapseBoxSets; }
            set { if (this.Data.CollapseBoxSets != value) { this.Data.CollapseBoxSets = value; Save(); FireConfigPropertyChanged("CollapseBoxSets"); } }
        }

        [Comment("Changes the default view index for folders that have not yet been visited.\n\t[Detail|Poster|Thumb]")]
        public ViewType DefaultViewType
        {
            get
            {
                try
                {
                    return (ViewType)Enum.Parse(typeof(ViewType), this.Data.DefaultViewType);
                }
                catch
                {
                    return ViewType.CoverFlow;
                }
            }
            set { if (this.Data.DefaultViewType != value.ToString()) { this.Data.DefaultViewType = value.ToString(); Save(); FireConfigPropertyChanged("DefaultViewType"); } }
        }
        [Comment("Specifies whether the default Poster and Thumb views show labels")]
        public bool DefaultShowLabels
        {
            get { return this.Data.DefaultShowLabels; }
            set { if (this.Data.DefaultShowLabels != value) { this.Data.DefaultShowLabels = value; Save(); FireConfigPropertyChanged("DefaultShowLabels"); } }
        }
        [Comment("Specifies is the default for the Poster view is vertical scrolling")]
        public bool DefaultVerticalScroll
        {
            get { return this.Data.DefaultVerticalScroll; }
            set { if (this.Data.DefaultVerticalScroll != value) { this.Data.DefaultVerticalScroll = value; Save(); FireConfigPropertyChanged("DefaultVerticalScroll"); } }
        }
        [Comment(@"Limits the number of levels shown by the breadcrumbs.")]
        public int BreadcrumbCountLimit
        {
            get { return this.CommonData.BreadcrumbCountLimit; }
            set { if (this.CommonData.BreadcrumbCountLimit != value) { this.CommonData.BreadcrumbCountLimit = value; Save(); FireConfigPropertyChanged("BreadcrumbCountLimit"); } }
        }
        public int DefaultMessageTimeout
        {
            get { return this.CommonData.DefaultMessageTimeout; }
            set { if (this.CommonData.DefaultMessageTimeout != value) { this.CommonData.DefaultMessageTimeout = value; Save(); FireConfigPropertyChanged("DefaultMessageTimeout"); } }
        }
        public int HttpTimeout
        {
            get { return this.CommonData.HttpTimeout; }
            set { if (this.CommonData.HttpTimeout != value) { this.CommonData.HttpTimeout = value; Save(); FireConfigPropertyChanged("HttpTimeout"); } }
        }
        public int NewItemNotificationDisplayTime
        {
            get { return this.Data.NewItemNotificationDisplayTime; }
            set { if (this.Data.NewItemNotificationDisplayTime != value) { this.Data.NewItemNotificationDisplayTime = value; Save(); FireConfigPropertyChanged("NewItemNotificationDisplayTime"); } }
        }
        public bool AllowInternetMetadataProviders
        {
            get { return this.Data.AllowInternetMetadataProviders; }
            set { if (this.Data.AllowInternetMetadataProviders != value) { this.Data.AllowInternetMetadataProviders = value; Save(); FireConfigPropertyChanged("AllowInternetMetadataProviders"); } }
        }

        internal List<CommonConfigData.ExternalPlayer> ExternalPlayers
        {
            get { return this.CommonData.ExternalPlayers; }
            //set { if (this.data.ExternalPlayers != value) { this.data.ExternalPlayers = value; Save(); FireConfigPropertyChanged("ExternalPlayers"); } }
        }

        public bool UseAutoPlayForIso
        {
            get { return this.CommonData.UseAutoPlayForIso; }
            set { if (this.CommonData.UseAutoPlayForIso != value) { this.CommonData.UseAutoPlayForIso = value; Save(); FireConfigPropertyChanged("UseAutoPlayForIso"); } }
        }

        [Comment("List of characters to remove from titles for alphanumeric sorting.  Separate each character with a '|'.\nThis allows titles like '10,000.BC.2008.720p.BluRay.DTS.x264-hV.mkv' to be properly sorted.")]
        public string SortRemoveCharacters
        {
            get { return this.Data.SortRemoveCharacters; }
            set { if (this.Data.SortRemoveCharacters != value) { this.Data.SortRemoveCharacters = value; Save(); FireConfigPropertyChanged("SortRemoveCharacters"); } }
        }
        [Comment("List of characters to replace with a ' ' in titles for alphanumeric sorting.  Separate each character with a '|'.\nThis allows titles like 'Iron.Man.REPACK.720p.BluRay.x264-SEPTiC.mkv' to be properly sorted.")]
        public string SortReplaceCharacters
        {
            get { return this.Data.SortReplaceCharacters; }
            set { if (this.Data.SortReplaceCharacters != value) { this.Data.SortReplaceCharacters = value; Save(); FireConfigPropertyChanged("SortReplaceCharacters"); } }
        }
        [Comment(@"List of words to remove from alphanumeric sorting.  Separate each word with a '|'.  Note that the
        algorithm appends a ' ' to the end of each word during the search which means words found at the end
        of each title will not be removed.  This is generally not an issue since most people will only want
        articles removed and articles are rarely found at the end of media titles.  This, combined with SortReplaceCharacters,
        allows titles like 'The.Adventures.Of.Baron.Munchausen.1988.720p.BluRay.x264-SiNNERS.mkv' to be properly sorted.")]
        public string SortReplaceWords
        {
            get { return this.Data.SortReplaceWords; }
            set { if (this.Data.SortReplaceWords != value) { this.Data.SortReplaceWords = value; Save(); FireConfigPropertyChanged("SortReplaceWords"); } }
        }

        public string ViewTheme
        {
            get { return this.Data.ViewTheme; }
            set { if (this.Data.ViewTheme != value) { this.Data.ViewTheme = value; Save(); FireConfigPropertyChanged("ViewTheme"); } }
        }

        public string Theme
        {
            get { return Data != null ? this.Data.Theme : "Default"; }
            set { if (this.Data.Theme != value) { this.Data.Theme = value; Save(); FireConfigPropertyChanged("Theme"); } }
        }

        public string FontTheme
        {
            get { return this.Data.FontTheme; }
            set { if (this.Data.FontTheme != value) { this.Data.FontTheme = value; Save(); FireConfigPropertyChanged("FontTheme"); } }
        }

        [Comment(@"Enable clock onscreen.")]
        public bool ShowClock
        {
            get { return this.Data.ShowClock; }
            set { if (this.Data.ShowClock != value) { this.Data.ShowClock = value; Save(); FireConfigPropertyChanged("ShowClock"); } }
        }

        public bool EnableThemeBackgrounds
        {
            get { return this.Data.EnableThemeBackgrounds; }
            set { if (this.Data.EnableThemeBackgrounds != value) { this.Data.EnableThemeBackgrounds = value; Save(); FireConfigPropertyChanged("EnableThemeBackgrounds"); } }
        }

        public bool PlayTrailerAsBackground
        {
            get { return this.Data.PlayTrailerAsBackground; }
            set { if (this.Data.PlayTrailerAsBackground != value) { this.Data.PlayTrailerAsBackground = value; Save(); FireConfigPropertyChanged("PlayTrailerAsBackground"); } }
        }

        public bool NewViewsIntroShown
        {
            get { return this.Data.NewViewsIntroShown; }
            set { if (this.Data.NewViewsIntroShown != value) { this.Data.NewViewsIntroShown = value; Save(); FireConfigPropertyChanged("NewViewsIntroShown"); } }
        }

        public bool ShowMovieSubViews
        {
            get { return this.Data.ShowMovieSubViews; }
            set { if (this.Data.ShowMovieSubViews != value) { this.Data.ShowMovieSubViews = value; Save(); FireConfigPropertyChanged("ShowMovieSubViews"); } }
        }

        public bool ShowTvSubViews
        {
            get { return this.Data.ShowTvSubViews; }
            set { if (this.Data.ShowTvSubViews != value) { this.Data.ShowTvSubViews = value; Save(); FireConfigPropertyChanged("ShowTvSubViews"); } }
        }

        public int ThemeBackgroundRepeat
        {
            get { return this.Data.ThemeBackgroundRepeat; }
            set { if (this.Data.ThemeBackgroundRepeat != value) { this.Data.ThemeBackgroundRepeat = value; Save(); FireConfigPropertyChanged("ThemBackgroundRepeat"); } }
        }

        public int MaxPrimaryWidth
        {
            get { return this.CommonData.MaxPrimaryWidth; }
            set { if (this.CommonData.MaxPrimaryWidth != value) { this.CommonData.MaxPrimaryWidth = value; Save(); FireConfigPropertyChanged("MaxPrimaryWidth"); } }
        }

        public int MaxThumbWidth
        {
            get { return this.CommonData.MaxThumbWidth; }
            set { if (this.CommonData.MaxThumbWidth != value) { this.CommonData.MaxThumbWidth = value; Save(); FireConfigPropertyChanged("MaxThumbWidth"); } }
        }

        public int MaxBackgroundWidth
        {
            get { return this.CommonData.MaxBackgroundWidth; }
            set { if (this.CommonData.MaxBackgroundWidth != value) { this.CommonData.MaxBackgroundWidth = value; Save(); FireConfigPropertyChanged("MaxBackgroundWidth"); } }
        }

        [Comment(@"Enable more advanced commands.")]
        //defunct
        public bool EnableAdvancedCmds
        {
            get { return true; }
        }

        [Comment(@"Advanced Command: Enable Delete")]
        public bool Advanced_EnableDelete
        {
            get { return Kernel.CurrentUser.Dto.Policy.EnableContentDeletion; }
            set { if (this.Data.Advanced_EnableDelete != value) { this.Data.Advanced_EnableDelete = value; Save(); FireConfigPropertyChanged("Advanced_EnableDelete"); } }
        }

        [Comment(@"Show backdrop on main views.")]
        public bool ShowBackdrop
        {
            get { return this.Data.ShowBackdrop; }
            set { if (this.Data.ShowBackdrop != value) { this.Data.ShowBackdrop = value; Save(); FireConfigPropertyChanged("ShowBackdrop"); } }
        }

        public bool ShowConfigButton
        {
            get { return this.Data.ShowConfigButton; }
            set { if (this.Data.ShowConfigButton != value) { this.Data.ShowConfigButton = value; Save(); FireConfigPropertyChanged("ShowConfigButton"); } }
        }

        public int AlphaBlending
        {
            get { return this.Data.AlphaBlending; }
            set { if (this.Data.AlphaBlending != value) { this.Data.AlphaBlending = value; Save(); FireConfigPropertyChanged("AlphaBlending"); } }
        }
        public string YahooWeatherFeed
        {
            get { return this.CommonData.WeatherLocation; }
            set { if (this.CommonData.WeatherLocation != value) { this.CommonData.WeatherLocation = value; Save(); FireConfigPropertyChanged("YahooWeatherFeed"); } }
        }
        public string YahooWeatherUnit
        {
            get { return string.IsNullOrEmpty(this.CommonData.WeatherUnit) ? "f" : this.CommonData.WeatherUnit; }
            set { if (this.CommonData.WeatherUnit != value) { this.CommonData.WeatherUnit = value; Save(); FireConfigPropertyChanged("YahooWeatherUnit"); } }
        }
        public string SupporterKey
        {
            get { return this.Data.SupporterKey; }
            set { if (this.Data.SupporterKey != value) { this.Data.SupporterKey = value; FireConfigPropertyChanged("SupporterKey"); } }
        }

        public bool HideFocusFrame
        {
            get { return this.Data.HideFocusFrame; }
            set { if (this.Data.HideFocusFrame != value) { this.Data.HideFocusFrame = value; Save(); FireConfigPropertyChanged("HideFocusFrame"); } }
        }

        public bool ShowRootBackground
        {
            get { return this.Data.ShowRootBackground; }
            set { if (this.Data.ShowRootBackground != value) { this.Data.ShowRootBackground = value; Save(); FireConfigPropertyChanged("ShowRootBackground"); } }
        }

        public bool EnableMouseHook
        {
            get
            {
                return this.CommonData.EnableMouseHook; 
            }
            set { if (this.CommonData.EnableMouseHook != value) { this.CommonData.EnableMouseHook = value; Save(); FireConfigPropertyChanged("EnableMouseHook"); } }
        }

        public int RecentItemCount
        {
            get { return this.Data.RecentItemCount; }
            set { if (this.Data.RecentItemCount != value) { this.Data.RecentItemCount = value; Save(); FireConfigPropertyChanged("RecentItemCount"); } }
        }

        public int RecentItemDays
        {
            get { return this.Data.RecentItemDays; }
            set { if (this.Data.RecentItemDays != value) { this.Data.RecentItemDays = value; Save(); FireConfigPropertyChanged("RecentItemDays"); } }
        }

        public int RecentItemCollapseThresh
        {
            get { return this.Data.RecentItemCollapseThresh; }
            set { if (this.Data.RecentItemCollapseThresh != value) { this.Data.RecentItemCollapseThresh = value; Save(); FireConfigPropertyChanged("RecentItemCollapseThresh"); } }
        }

        public string RecentItemOption
        {
            get { return this.Data.RecentItemOption; }
            set { if (this.Data.RecentItemOption != value) { this.Data.RecentItemOption = value; Save(); FireConfigPropertyChanged("RecentItemOption"); } }
        }

        public string StartupParms
        {
            get { return this.CommonData.StartupParms; }
            set { if (this.CommonData.StartupParms != value) { this.CommonData.StartupParms = value; Save(); FireConfigPropertyChanged("StartupParms"); } }
        }

        public List<string> PluginSources
        {
            get { return new List<string>(); }
            set {  }
        }
        
        public bool RandomizeBackdrops
        {
            get { return this.Data.RandomizeBackdrops; }
            set { if (this.Data.RandomizeBackdrops != value) { this.Data.RandomizeBackdrops = value; Save(); FireConfigPropertyChanged("RandomizeBackdrops"); } }
        }

        public bool RotateBackdrops
        {
            get { return this.Data.RotateBackdrops; }
            set { if (this.Data.RotateBackdrops != value) { this.Data.RotateBackdrops = value; Save(); FireConfigPropertyChanged("RotateBackdrops"); } }
        }

        public int BackdropRotationInterval
        {
            get { return this.Data.BackdropRotationInterval; }
            set { if (this.Data.BackdropRotationInterval != value) { this.Data.BackdropRotationInterval = value; Save(); FireConfigPropertyChanged("BackdropRotationInterval"); } }
        }

        public float BackdropTransitionInterval
        {
            get { return this.Data.BackdropTransitionInterval; }
            set { if (this.Data.BackdropTransitionInterval != value) { this.Data.BackdropTransitionInterval = (float)Math.Round(value, 1); Save(); FireConfigPropertyChanged("BackdropTransitionInterval"); } }
        }

        public int BackdropLoadDelay
        {
            get { return this.Data.BackdropLoadDelay; }
            set { if (this.Data.BackdropLoadDelay != value) { this.Data.BackdropLoadDelay = value; Save(); FireConfigPropertyChanged("BackdropLoadDelay"); } }
        }
        
        public int FullRefreshInterval
        {
            get { return 100; }
        }

        public bool ServiceRefreshFailed
        {
            get { return false; }
        }

        public DateTime LastFullRefresh
        {
            get { return DateTime.MaxValue;  }
        }

         public bool YearSortAsc
        {
            get { return this.Data.YearSortAsc; }
            set { if (this.Data.YearSortAsc != value) { this.Data.YearSortAsc = value; Save(); FireConfigPropertyChanged("YearSortAsc"); } }
        }

        public bool AutoScrollText
        {
            get { return this.Data.AutoScrollText; }
            set { if (this.Data.AutoScrollText != value) { this.Data.AutoScrollText = value; Save(); FireConfigPropertyChanged("AutoScrollText"); } }
        }
        public int AutoScrollDelay
        {
            get { return this.Data.AutoScrollDelay * 1000; } //Convert to milliseconds for MCML consumption
            set { if (this.Data.AutoScrollDelay != value) { this.Data.AutoScrollDelay = value; Save(); FireConfigPropertyChanged("AutoScrollDelay"); } }
        }
        public int AutoScrollSpeed
        {
            get { return this.Data.AutoScrollSpeed; }
            set { if (this.Data.AutoScrollSpeed != value) { this.Data.AutoScrollSpeed = value; Save(); FireConfigPropertyChanged("AutoScrollSpeed"); } }
        }

        public bool AutoValidate
        {
            get { return this.CommonData.AutoValidate; }
            set { if (this.CommonData.AutoValidate != value) { this.CommonData.AutoValidate = value; Save(); FireConfigPropertyChanged("AutoValidate"); } }
        }

        public LogSeverity MinLoggingSeverity
        {
            get { return this.CommonData.MinLoggingSeverity; }
            set { if (this.CommonData.MinLoggingSeverity != value) { CommonData.MinLoggingSeverity = value; Save(); FireConfigPropertyChanged("MinLoggingSeverity"); } }
        }

        [Comment(@"Enable screen Saver.")]
        public bool EnableScreenSaver
        {
            get { return this.Data.EnableScreenSaver; }
            set { if (this.Data.EnableScreenSaver != value) { this.Data.EnableScreenSaver = value; Save(); FireConfigPropertyChanged("EnableScreenSaver"); } }
        }

        public int ScreenSaverTimeOut
        {
            get { return this.Data.ScreenSaverTimeOut; }
            set { if (this.Data.ScreenSaverTimeOut != value) { this.Data.ScreenSaverTimeOut = value; Save(); FireConfigPropertyChanged("ScreenSaverTimeOut"); } }
        }

        public bool EnableAutoLogoff
        {
            get { return this.CommonData.EnableAutoLogoff; }
            set { if (this.CommonData.EnableAutoLogoff != value) { this.CommonData.EnableAutoLogoff = value; Save(); FireConfigPropertyChanged("EnableAutoLogoff"); } }
        }

        public int AutoLogoffTimeOut
        {
            get { return this.CommonData.AutoLogoffTimeOut; }
            set { if (this.CommonData.AutoLogoffTimeOut != value) { this.CommonData.AutoLogoffTimeOut = value; Save(); FireConfigPropertyChanged("AutoLogoffTimeOut"); } }
        }

        public Color UserTileColor
        {
            get
            {
                try
                {
                    return new Color((Colors)Enum.Parse(typeof(Colors), this.CommonData.UserTileColor));
                }
                catch (Exception)
                {
                    return new Color(Colors.DarkBlue);
                }
            }
        }

        public Color LoginBgColor
        {
            get
            {
                try
                {
                    return new Color((Colors)Enum.Parse(typeof(Colors), this.CommonData.LoginBgColor));
                }
                catch (Exception)
                {
                    return new Color(Colors.DarkGray);
                }
            }
        }

        public bool UseResizedImages
        {
            get { return this.Data.UseResizedImages; }
            set { if (this.Data.UseResizedImages != value) { this.Data.UseResizedImages = value; Save(); FireConfigPropertyChanged("UseResizedImages"); } }
        }

        /* End of app specific settings*/

        private string[] _SortRemoveCharactersArray;
        public string[] SortRemoveCharactersArray
        {
            get
            {
                _SortRemoveCharactersArray = _SortRemoveCharactersArray ?? SortRemoveCharacters.Split('|');
                return _SortRemoveCharactersArray;
            }
        }

        private string[] _SortReplaceCharactersArray;
        public string[] SortReplaceCharactersArray
        {
            get
            {
                _SortReplaceCharactersArray = _SortReplaceCharactersArray ?? SortReplaceCharacters.Split('|');
                return _SortReplaceCharactersArray;
            }
        }

        private string[] _SortReplaceWordsArray;
        public string[] SortReplaceWordsArray
        {
            get
            {
                _SortReplaceWordsArray = _SortReplaceWordsArray ?? SortReplaceWords.Split('|');
                return _SortReplaceWordsArray;
            }
        }

        public static void Reload()
        {
            _instance = new Config();
        }

        private static Config _instance;
        public static Config Instance
        {
            get
            {
                return _instance ?? (_instance = new Config());
            }
        }

        bool isValid;
        private Config()
        {
            isValid = CommonData != null;
        }

        public bool IsValid
        {
            get
            {
                return isValid;
            }
        }

        private void Save()
        {
            lock (this)
            {
                if (Data != null) Data.Save();
                CommonData.Save();
            }
        }

        private string GetComment(MemberInfo field)
        {
            string comment = "";
            var attribs = field.GetCustomAttributes(typeof(CommentAttribute), false);
            if (attribs != null && attribs.Length > 0)
            {
                comment = ((CommentAttribute)attribs[0]).Comment;
            }
            return comment;
        }


        #region IModelItem Members

        public string Description
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public bool Selected
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public Guid UniqueId
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        #region IPropertyObject Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region IModelItemOwner Members

        protected void FireConfigPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, property);
            }
        }

        List<ModelItem> items = new List<ModelItem>();

        public void RegisterObject(ModelItem modelItem)
        {
            items.Add(modelItem);
        }

        public void UnregisterObject(ModelItem modelItem)
        {
            if (items.Exists((i) => i == modelItem))
            {
                // TODO : Invoke on the UI thread
                modelItem.Dispose();
            }
        }

        #endregion
    }
}
