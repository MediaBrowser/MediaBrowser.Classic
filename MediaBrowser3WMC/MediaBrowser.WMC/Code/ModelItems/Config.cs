using System;
using System.Collections.Generic;
using System.Reflection;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Updates;
using MediaBrowser.Model.Weather;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Library;
using MediaBrowser.Attributes;
using Microsoft.MediaCenter;
using MediaBrowser.Library.Configuration;
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
            set { if (this.Data.AlwaysShowDetailsPage != value) { this.Data.AlwaysShowDetailsPage = value; Save(); FirePropertyChanged("AlwaysShowDetailsPage"); } }
        }

        //public int ParentalUnlockPeriod
        //{
        //    get { return this.data.ParentalUnlockPeriod; }
        //    set { if (this.data.ParentalUnlockPeriod != value) { this.data.ParentalUnlockPeriod = value; Save(); FirePropertyChanged("ParentalUnlockPeriod"); } }
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
            set { if (this.Data.EnableRootPage != value) { this.Data.EnableRootPage = value; Save(); FirePropertyChanged("EnableRootPage"); } }
        }

        public bool ProcessBanners
        {
            get { return this.Data.ProcessBanners; }
            set { if (this.Data.ProcessBanners != value) { this.Data.ProcessBanners = value; Save(); FirePropertyChanged("ProcessBanners"); } }
        }

        public bool ProcessBackdrops
        {
            get { return this.Data.ProcessBackdrops; }
            set { if (this.Data.ProcessBackdrops != value) { this.Data.ProcessBackdrops = value; Save(); FirePropertyChanged("ProcessBackdrops"); } }
        }

        public bool AskIncludeChildrenRefresh
        {
            get { return this.Data.AskIncludeChildrenRefresh; }
            set { if (this.Data.AskIncludeChildrenRefresh != value) { this.Data.AskIncludeChildrenRefresh = value; Save(); FirePropertyChanged("AskIncludeChildrenRefresh"); } }
        }

        public bool DefaultIncludeChildrenRefresh
        {
            get { return this.Data.DefaultIncludeChildrenRefresh; }
            set { if (this.Data.DefaultIncludeChildrenRefresh != value) { this.Data.DefaultIncludeChildrenRefresh = value; Save(); FirePropertyChanged("DefaultIncludeChildrenRefresh"); } }
        }

        public bool IsFirstRun
        {
            get { return this.CommonData.IsFirstRun; }
            set { if (this.CommonData.IsFirstRun != value) { this.CommonData.IsFirstRun = value; Save(); FirePropertyChanged("HasBeenConfigured"); } }
        }

        public bool CacheAllImagesInMemory
        {
            get { return this.Data.CacheAllImagesInMemory; }
            set { if (this.Data.CacheAllImagesInMemory != value) { this.Data.CacheAllImagesInMemory = value; Save(); FirePropertyChanged("CacheAllImagesInMemory"); } }
        }

        public bool HideEmptyFolders
        {
            get { return this.Data.HideEmptyFolders; }
            set { if (this.Data.HideEmptyFolders != value) { this.Data.HideEmptyFolders = value; Save(); FirePropertyChanged("HideEmptyFolders"); } }
        }

        [Comment(@"The current version of MB - will be the last version te first time we run so we can do something")]
        public string MBVersion
        {
            get { return this.CommonData.MBVersion; }
            set { if (this.CommonData.MBVersion != value) { this.CommonData.MBVersion = value; Save(); FirePropertyChanged("MBVersion"); } }
        }
        [Comment("Synchronize the view for similar folder types")]
        public bool EnableSyncViews
        {
            get { return this.Data.EnableSyncViews; }
            set { if (this.Data.EnableSyncViews != value) { this.Data.EnableSyncViews = value; Save(); FirePropertyChanged("EnableSyncViews"); } }

        }

        public bool ShowFavoritesCollection
        {
            get { return this.Data.ShowFavoritesCollection; }
            set { if (this.Data.ShowFavoritesCollection != value) { this.Data.ShowFavoritesCollection = value; Save(); FirePropertyChanged("ShowFavoritesCollection"); } }
        }

        public string FavoriteFolderName
        {
            get { return this.Data.FavoriteFolderName; }
            set { if (this.Data.FavoriteFolderName != value) { this.Data.FavoriteFolderName = value; Save(); FirePropertyChanged("FavoriteFolderName"); } }
        }

        public bool ShowMovieGenreCollection
        {
            get { return this.Data.ShowMovieGenreCollection; }
            set { if (this.Data.ShowMovieGenreCollection != value) { this.Data.ShowMovieGenreCollection = value; Save(); FirePropertyChanged("ShowMovieGenreCollection"); } }
        }

        public string MovieGenreFolderName
        {
            get { return this.Data.MovieGenreFolderName; }
            set { if (this.Data.MovieGenreFolderName != value) { this.Data.MovieGenreFolderName = value; Save(); FirePropertyChanged("MovieGenreFolderName"); } }
        }

        public bool ShowMusicGenreCollection
        {
            get { return this.Data.ShowMusicGenreCollection; }
            set { if (this.Data.ShowMusicGenreCollection != value) { this.Data.ShowMusicGenreCollection = value; Save(); FirePropertyChanged("ShowMusicGenreCollection"); } }
        }

        public bool ShowMusicAlbumCollection
        {
            get { return this.Data.ShowMusicAlbumCollection; }
            set { if (this.Data.ShowMusicAlbumCollection != value) { this.Data.ShowMusicAlbumCollection = value; Save(); FirePropertyChanged("ShowMusicAlbumCollection"); } }
        }

        public string MusicGenreFolderName
        {
            get { return this.Data.MusicGenreFolderName; }
            set { if (this.Data.MusicGenreFolderName != value) { this.Data.MusicGenreFolderName = value; Save(); FirePropertyChanged("MusicGenreFolderName"); } }
        }

        public bool GroupAlbumsByArtist
        {
            get { return this.Data.GroupAlbumsByArtist; }
            set { if (this.Data.GroupAlbumsByArtist != value) { this.Data.GroupAlbumsByArtist = value; Save(); FirePropertyChanged("GroupAlbumsByArtist"); } }
        }

        public bool ShowNewItemNotification
        {
            get { return this.Data.ShowNewItemNotification; }
            set { if (this.Data.ShowNewItemNotification != value) { this.Data.ShowNewItemNotification = value; Save(); FirePropertyChanged("ShowNewItemNotification"); } }
        }

        [Comment("Dim all unselected posters in poster and thumbstrib views")]
        public bool DimUnselectedPosters
        {
            get { return this.Data.DimUnselectedPosters; }
            set { if (this.Data.DimUnselectedPosters != value) { this.Data.DimUnselectedPosters = value; Save(); FirePropertyChanged("DimUnselectedPosters"); } }
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
                    FirePropertyChanged("OverScanScaling");
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
                    FirePropertyChanged("OverScanPadding");
                }
            }
        }
        [Comment(@"Enables the writing of trace log files in a production environment to assist with problem solving")]
        public bool EnableTraceLogging
        {
            get { return this.CommonData.EnableTraceLogging; }
            set { if (this.CommonData.EnableTraceLogging != value) { this.CommonData.EnableTraceLogging = value; Save(); FirePropertyChanged("EnableTraceLogging"); } }
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
                    FirePropertyChanged("DefaultPosterSize");
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
            set { if (this.Data.SlideShowInterval != value) { this.Data.SlideShowInterval = value; Save(); FirePropertyChanged("SlideShowInterval"); } }
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
                    FirePropertyChanged("GridSpacing");
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
            set { if (this.Data.ThumbStripPosterWidth != value) { this.Data.ThumbStripPosterWidth = value; Save(); FirePropertyChanged("ThumbStripPosterWidth"); } }
        }

        public bool RememberIndexing
        {
            get { return this.Data.RememberIndexing; }
            set { if (this.Data.RememberIndexing != value) { this.Data.RememberIndexing = value; Save(); FirePropertyChanged("RememberIndexing"); } }
        }

        public bool RememberFilters
        {
            get { return this.Data.RememberFilters; }
            set { if (this.Data.RememberFilters != value) { this.Data.RememberFilters = value; Save(); FirePropertyChanged("RememberFilters"); } }
        }

        public bool TreatWatchedAsInProgress
        {
            get { return this.Data.TreatWatchedAsInProgress; }
            set { if (this.Data.TreatWatchedAsInProgress != value) { this.Data.TreatWatchedAsInProgress = value; Save(); Application.CurrentInstance.ClearAllQuickLists(); FirePropertyChanged("TreatWatchedAsInProgress"); } }
        }

        public bool ShowIndexWarning
        {
            get { return this.Data.ShowIndexWarning; }
            set { if (this.Data.ShowIndexWarning != value) { this.Data.ShowIndexWarning = value; Save(); FirePropertyChanged("ShowIndexWarning"); } }
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
            set { if (this.Data.IndexWarningThreshold != value) { this.Data.IndexWarningThreshold = value; Save(); FirePropertyChanged("IndexWarningThreshold"); } }
        }

        [Comment(@"Controls the maximum difference between the actual aspect ration of a poster image and the thumbnails being displayed to allow the application to stretch the image non-proportionally.
            x = Abs( (image width/ image height) - (display width / display height) )
            if x is less than the configured value the imae will be stretched non-proportionally to fit the display size")]
        public float MaximumAspectRatioDistortion
        {
            get { return this.Data.MaximumAspectRatioDistortion; }
            set { if (this.Data.MaximumAspectRatioDistortion != value) { this.Data.MaximumAspectRatioDistortion = value; Save(); FirePropertyChanged("MaximumAspectRatioDistortion"); } }
        }
        [Comment(@"Enable transcode 360 support on extenders")]
        public bool EnableTranscode360
        {
            get { return this.CommonData.EnableTranscode360; }
            set { if (this.CommonData.EnableTranscode360 != value) { this.CommonData.EnableTranscode360 = value; Save(); FirePropertyChanged("EnableTranscode360"); } }
        }
        [Comment(@"A lower case comma delimited list of types the extender supports natively. Example: .dvr-ms,.wmv")]
        public string ExtenderNativeTypes
        {
            get { return this.CommonData.ExtenderNativeTypes; }
            set { if (this.CommonData.ExtenderNativeTypes != value) { this.CommonData.ExtenderNativeTypes = value; Save(); FirePropertyChanged("ExtenderNativeTypes"); } }
        }
        [Comment("ShowThemeBackground [Default Value - False]\n\tTrue: Enables transparent background.\n\tFalse: Use default Video Browser background.")]
        public bool ShowThemeBackground
        {
            get { return this.Data.ShowThemeBackground; }
            set { if (this.Data.ShowThemeBackground != value) { this.Data.ShowThemeBackground = value; Save(); FirePropertyChanged("ShowThemeBackground"); } }
        }
        [Comment("Example. If set to true the following will be treated as a movie and an automatic playlist will be created.\n\tIndiana Jones / Disc 1 / a.avi\n\tIndiana Jones / Disc 2 / b.avi")]
        public bool EnableNestedMovieFolders
        {
            get { return this.Data.EnableNestedMovieFolders; }
            set { if (this.Data.EnableNestedMovieFolders != value) { this.Data.EnableNestedMovieFolders = value; Save(); FirePropertyChanged("EnableNestedMovieFolders"); } }
        }
        [Comment("Example. If set to true the following will be treated as a movie and an automatic playlist will be created.\n\tIndiana Jones / a.avi\n\tIndiana Jones / b.avi (This only works for 2 videos (no more))\n**Setting this to false will override EnableNestedMovieFolders if that is enabled.**")]
        public bool EnableMoviePlaylists
        {
            get { return this.Data.EnableMoviePlaylists; }
            set { if (this.Data.EnableMoviePlaylists != value) { this.Data.EnableMoviePlaylists = value; Save(); FirePropertyChanged("EnableMoviePlaylists"); } }
        }
        [Comment("Limit to the number of video files that willbe assumed to be a single movie and create a playlist for")]
        public int PlaylistLimit
        {
            get { return this.Data.PlaylistLimit; }
            set { if (this.Data.PlaylistLimit != value) { this.Data.PlaylistLimit = value; Save(); FirePropertyChanged("PlaylistLimit"); } }
        }
        [Comment("The starting folder for video browser. By default its set to MyVideos.\nCan be set to a folder for example c:\\ or a virtual folder for example c:\\folder.vf")]
        public string InitialFolder
        {
            get { return this.Data.InitialFolder; }
            set { if (this.Data.InitialFolder != value) { this.Data.InitialFolder = value; Save(); FirePropertyChanged("InitialFolder"); } }
        }
        [Comment(@"Flag for auto-updates.  True will auto-update, false will not.")]
        public bool EnableUpdates
        {
            get { return this.CommonData.EnableUpdates; }
            set { if (this.CommonData.EnableUpdates != value) { this.CommonData.EnableUpdates = value; Save(); FirePropertyChanged("EnableUpdates"); } }
        }
        public string SystemUpdateClass
        {
            get { return this.CommonData.SystemUpdateClass.ToString(); }
            set { if (this.CommonData.SystemUpdateClass.ToString() != value) { this.CommonData.SystemUpdateClass = (PackageVersionClass)Enum.Parse(typeof(PackageVersionClass), value); Save(); FirePropertyChanged("SystemUpdateClass"); } }
        }
        public string PluginUpdateClass
        {
            get { return this.CommonData.PluginUpdateClass.ToString(); }
            set { if (this.CommonData.PluginUpdateClass.ToString() != value) { this.CommonData.PluginUpdateClass = (PackageVersionClass)Enum.Parse(typeof(PackageVersionClass), value); Save(); FirePropertyChanged("PluginUpdateClass"); } }
        }
        [Comment(@"Set the location of the Daemon Tools binary..")]
        public string DaemonToolsLocation
        {
            get { return this.CommonData.DaemonToolsLocation; }
            set { if (this.CommonData.DaemonToolsLocation != value) { this.CommonData.DaemonToolsLocation = value; Save(); FirePropertyChanged("DaemonToolsLocation"); } }
        }
        [Comment(@"The drive letter of the Daemon Tools virtual drive.")]
        public string DaemonToolsDrive
        {
            get { return this.CommonData.DaemonToolsDrive; }
            set { if (this.CommonData.DaemonToolsDrive != value) { this.CommonData.DaemonToolsDrive = value; Save(); FirePropertyChanged("DaemonToolsDrive"); } }
        }
        [Comment("Flag for alphanumeric sorting.  True will use alphanumeric sorting, false will use alphabetic sorting.\nNote that the sorting algorithm is case insensitive.")]
        public bool EnableAlphanumericSorting
        {
            get { return this.Data.EnableAlphanumericSorting; }
            set { if (this.Data.EnableAlphanumericSorting != value) { this.Data.EnableAlphanumericSorting = value; Save(); FirePropertyChanged("EnableAlphanumericSorting"); } }
        }
        [Comment(@"Enables the showing of tick in the list view for files that have been watched")]
        public bool EnableListViewTicks
        {
            get { return this.Data.EnableListViewTicks; }
            set { if (this.Data.EnableListViewTicks != value) { this.Data.EnableListViewTicks = value; Save(); FirePropertyChanged("EnableListViewTicks"); } }
        }
        [Comment(@"Enables the showing of watched shows in a different color in the list view.")]
        public bool EnableListViewWatchedColor
        {
            get { return this.Data.EnableListViewWatchedColor; }
            set { if (this.Data.EnableListViewWatchedColor != value) { this.Data.EnableListViewWatchedColor = value; Save(); FirePropertyChanged("EnableListViewWatchedColor"); } }
        }
         [Comment(@"Enables the showing of watched shows in a different color in the list view (Transparent disables it)")]
         public Colors ListViewWatchedColor
         {
             get
             {
                 return (Colors)(int)this.Data.ListViewWatchedColor;
             }
             set { if ((int)this.Data.ListViewWatchedColor != (int)value) { this.Data.ListViewWatchedColor = (MediaBrowser.Code.ShadowTypes.Colors)(int)value; Save(); FirePropertyChanged("ListViewWatchedColor"); FirePropertyChanged("ListViewWatchedColorMcml"); } }
         }
         public Color ListViewWatchedColorMcml
         {
             get { return new Color(this.ListViewWatchedColor); }
         }
        public bool ShowUnwatchedCount
        {
            get { return this.Data.ShowUnwatchedCount; }
            set { if (this.Data.ShowUnwatchedCount != value) { this.Data.ShowUnwatchedCount = value; Save(); FirePropertyChanged("ShowUnwatchedCount"); } }
        }

        public bool ShowUnwatchedIndicator
        {
            get { return this.Data.ShowUnwatchedIndicator; }
            set { if (this.Data.ShowUnwatchedIndicator != value) { this.Data.ShowUnwatchedIndicator = value; Save(); FirePropertyChanged("ShowUnwatchedIndicator"); } }
        }

        public bool ShowWatchedTickOnFolders
        {
            get { return this.Data.ShowWatchedTickOnFolders; }
            set { if (this.Data.ShowWatchedTickOnFolders != value) { this.Data.ShowWatchedTickOnFolders = value; Save(); FirePropertyChanged("ShowWatchedTickOnFolders"); } }
        }

        public bool ShowWatchTickInPosterView
        {
            get { return this.Data.ShowWatchTickInPosterView; }
            set { if (this.Data.ShowWatchTickInPosterView != value) { this.Data.ShowWatchTickInPosterView = value; Save(); FirePropertyChanged("ShowWatchTickInPosterView"); } }
        }

        public bool ShowHDIndicatorOnPosters
        {
            get { return this.Data.ShowHDIndicatorOnPosters; }
            set { if (this.Data.ShowHDIndicatorOnPosters != value) { this.Data.ShowHDIndicatorOnPosters = value; Save(); FirePropertyChanged("ShowHDIndicatorOnPosters"); } }
        }

        public bool ShowRemoteIndicatorOnPosters
        {
            get { return this.Data.ShowRemoteIndicatorOnPosters; }
            set { if (this.Data.ShowRemoteIndicatorOnPosters != value) { this.Data.ShowRemoteIndicatorOnPosters = value; Save(); FirePropertyChanged("ShowRemoteIndicatorOnPosters"); } }
        }

        public bool ExcludeRemoteContentInSearch
        {
            get { return this.Data.ExcludeRemoteContentInSearch; }
            set { if (this.Data.ExcludeRemoteContentInSearch != value) { this.Data.ExcludeRemoteContentInSearch = value; Save(); FirePropertyChanged("ExcludeRemoteContentInSearch"); } }
        }

        [Comment("Enables the views to default to the first unwatched item in a folder of movies or tv shows")]
        public bool DefaultToFirstUnwatched
        {
            get { return this.Data.DefaultToFirstUnwatched; }
            set { if (this.Data.DefaultToFirstUnwatched != value) { this.Data.DefaultToFirstUnwatched = value; Save(); FirePropertyChanged("DefaultToFirstUnwatched"); } }
        }
        [Comment("When navigating, if only a single folder exists, enter it.")]
        public bool AutoEnterSingleDirs
        {
            get { return this.Data.AutoEnterSingleDirs; }
            set { if (this.Data.AutoEnterSingleDirs != value) { this.Data.AutoEnterSingleDirs = value; Save(); FirePropertyChanged("AutoEnterSingleDirs"); } }
        }
        [Comment(@"Indicates that files with a date stamp before this date should be assumed to have been watched for the purpose of ticking them off.")]
        public DateTime AssumeWatchedBefore
        {
            get { return this.Data.AssumeWatchedBefore; }
            set { if (this.Data.AssumeWatchedBefore != value) { this.Data.AssumeWatchedBefore = value; Save(); FirePropertyChanged("AssumeWatchedBefore"); FirePropertyChanged("AssumeWatchedBeforeStr"); } }
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
            set { if (this.Data.InheritDefaultView != value) { this.Data.InheritDefaultView = value; Save(); FirePropertyChanged("InheritDefaultView"); } }
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
                    return ViewType.Poster;
                }
            }
            set { if (this.Data.DefaultViewType != value.ToString()) { this.Data.DefaultViewType = value.ToString(); Save(); FirePropertyChanged("DefaultViewType"); } }
        }
        [Comment("Specifies whether the default Poster and Thumb views show labels")]
        public bool DefaultShowLabels
        {
            get { return this.Data.DefaultShowLabels; }
            set { if (this.Data.DefaultShowLabels != value) { this.Data.DefaultShowLabels = value; Save(); FirePropertyChanged("DefaultShowLabels"); } }
        }
        [Comment("Specifies is the default for the Poster view is vertical scrolling")]
        public bool DefaultVerticalScroll
        {
            get { return this.Data.DefaultVerticalScroll; }
            set { if (this.Data.DefaultVerticalScroll != value) { this.Data.DefaultVerticalScroll = value; Save(); FirePropertyChanged("DefaultVerticalScroll"); } }
        }
        [Comment(@"Limits the number of levels shown by the breadcrumbs.")]
        public int BreadcrumbCountLimit
        {
            get { return this.CommonData.BreadcrumbCountLimit; }
            set { if (this.CommonData.BreadcrumbCountLimit != value) { this.CommonData.BreadcrumbCountLimit = value; Save(); FirePropertyChanged("BreadcrumbCountLimit"); } }
        }
        public int DefaultMessageTimeout
        {
            get { return this.Data.DefaultMessageTimeout; }
            set { if (this.Data.DefaultMessageTimeout != value) { this.Data.DefaultMessageTimeout = value; Save(); FirePropertyChanged("DefaultMessageTimeout"); } }
        }
        public int HttpTimeout
        {
            get { return this.CommonData.HttpTimeout; }
            set { if (this.CommonData.HttpTimeout != value) { this.CommonData.HttpTimeout = value; Save(); FirePropertyChanged("HttpTimeout"); } }
        }
        public int NewItemNotificationDisplayTime
        {
            get { return this.Data.NewItemNotificationDisplayTime; }
            set { if (this.Data.NewItemNotificationDisplayTime != value) { this.Data.NewItemNotificationDisplayTime = value; Save(); FirePropertyChanged("NewItemNotificationDisplayTime"); } }
        }
        public bool AllowInternetMetadataProviders
        {
            get { return this.Data.AllowInternetMetadataProviders; }
            set { if (this.Data.AllowInternetMetadataProviders != value) { this.Data.AllowInternetMetadataProviders = value; Save(); FirePropertyChanged("AllowInternetMetadataProviders"); } }
        }

        internal List<CommonConfigData.ExternalPlayer> ExternalPlayers
        {
            get { return this.CommonData.ExternalPlayers; }
            //set { if (this.data.ExternalPlayers != value) { this.data.ExternalPlayers = value; Save(); FirePropertyChanged("ExternalPlayers"); } }
        }

        public bool UseAutoPlayForIso
        {
            get { return this.CommonData.UseAutoPlayForIso; }
            set { if (this.CommonData.UseAutoPlayForIso != value) { this.CommonData.UseAutoPlayForIso = value; Save(); FirePropertyChanged("UseAutoPlayForIso"); } }
        }

        [Comment("List of characters to remove from titles for alphanumeric sorting.  Separate each character with a '|'.\nThis allows titles like '10,000.BC.2008.720p.BluRay.DTS.x264-hV.mkv' to be properly sorted.")]
        public string SortRemoveCharacters
        {
            get { return this.Data.SortRemoveCharacters; }
            set { if (this.Data.SortRemoveCharacters != value) { this.Data.SortRemoveCharacters = value; Save(); FirePropertyChanged("SortRemoveCharacters"); } }
        }
        [Comment("List of characters to replace with a ' ' in titles for alphanumeric sorting.  Separate each character with a '|'.\nThis allows titles like 'Iron.Man.REPACK.720p.BluRay.x264-SEPTiC.mkv' to be properly sorted.")]
        public string SortReplaceCharacters
        {
            get { return this.Data.SortReplaceCharacters; }
            set { if (this.Data.SortReplaceCharacters != value) { this.Data.SortReplaceCharacters = value; Save(); FirePropertyChanged("SortReplaceCharacters"); } }
        }
        [Comment(@"List of words to remove from alphanumeric sorting.  Separate each word with a '|'.  Note that the
        algorithm appends a ' ' to the end of each word during the search which means words found at the end
        of each title will not be removed.  This is generally not an issue since most people will only want
        articles removed and articles are rarely found at the end of media titles.  This, combined with SortReplaceCharacters,
        allows titles like 'The.Adventures.Of.Baron.Munchausen.1988.720p.BluRay.x264-SiNNERS.mkv' to be properly sorted.")]
        public string SortReplaceWords
        {
            get { return this.Data.SortReplaceWords; }
            set { if (this.Data.SortReplaceWords != value) { this.Data.SortReplaceWords = value; Save(); FirePropertyChanged("SortReplaceWords"); } }
        }

        public string ViewTheme
        {
            get { return this.Data.ViewTheme; }
            set { if (this.Data.ViewTheme != value) { this.Data.ViewTheme = value; Save(); FirePropertyChanged("ViewTheme"); } }
        }

        public string Theme
        {
            get { return Data != null ? this.Data.Theme : "Default"; }
            set { if (this.Data.Theme != value) { this.Data.Theme = value; Save(); FirePropertyChanged("Theme"); } }
        }

        public string FontTheme
        {
            get { return this.Data.FontTheme; }
            set { if (this.Data.FontTheme != value) { this.Data.FontTheme = value; Save(); FirePropertyChanged("FontTheme"); } }
        }

        [Comment(@"Enable clock onscreen.")]
        public bool ShowClock
        {
            get { return this.Data.ShowClock; }
            set { if (this.Data.ShowClock != value) { this.Data.ShowClock = value; Save(); FirePropertyChanged("ShowClock"); } }
        }

        [Comment(@"Enable more advanced commands.")]
        public bool EnableAdvancedCmds
        {
            get { return this.Data.EnableAdvancedCmds; }
            set { if (this.Data.EnableAdvancedCmds != value) { this.Data.EnableAdvancedCmds = value; Save(); FirePropertyChanged("EnableAdvancedCmds"); } }
        }

        [Comment(@"Advanced Command: Enable Delete")]
        public bool Advanced_EnableDelete
        {
            get { return this.Data.Advanced_EnableDelete; }
            set { if (this.Data.Advanced_EnableDelete != value) { this.Data.Advanced_EnableDelete = value; Save(); FirePropertyChanged("Advanced_EnableDelete"); } }
        }

        [Comment(@"Show backdrop on main views.")]
        public bool ShowBackdrop
        {
            get { return this.Data.ShowBackdrop; }
            set { if (this.Data.ShowBackdrop != value) { this.Data.ShowBackdrop = value; Save(); FirePropertyChanged("ShowBackdrop"); } }
        }

        public bool ShowConfigButton
        {
            get { return this.Data.ShowConfigButton; }
            set { if (this.Data.ShowConfigButton != value) { this.Data.ShowConfigButton = value; Save(); FirePropertyChanged("ShowConfigButton"); } }
        }

        public int AlphaBlending
        {
            get { return this.Data.AlphaBlending; }
            set { if (this.Data.AlphaBlending != value) { this.Data.AlphaBlending = value; Save(); FirePropertyChanged("AlphaBlending"); } }
        }
        public string YahooWeatherFeed
        {
            get { return this.ServerData.WeatherLocation; }
            set { if (this.ServerData.WeatherLocation != value) { this.ServerData.WeatherLocation = value; Save(); FirePropertyChanged("YahooWeatherFeed"); } }
        }
        public string YahooWeatherUnit
        {
            get { return this.ServerData.WeatherUnit == WeatherUnits.Fahrenheit ? "f" : "c"; }
            //set { if (this.ServerData.YahooWeatherUnit != value) { this.ServerData.YahooWeatherUnit = value; Save(); FirePropertyChanged("YahooWeatherUnit"); } }
        }
        public string SupporterKey
        {
            get { return this.Data.SupporterKey; }
            set { if (this.Data.SupporterKey != value) { this.Data.SupporterKey = value; FirePropertyChanged("SupporterKey"); } }
        }

        public bool HideFocusFrame
        {
            get { return this.Data.HideFocusFrame; }
            set { if (this.Data.HideFocusFrame != value) { this.Data.HideFocusFrame = value; Save(); FirePropertyChanged("HideFocusFrame"); } }
        }

        public bool ShowRootBackground
        {
            get { return this.Data.ShowRootBackground; }
            set { if (this.Data.ShowRootBackground != value) { this.Data.ShowRootBackground = value; Save(); FirePropertyChanged("ShowRootBackground"); } }
        }

        public bool EnableMouseHook
        {
            get
            {
                return false; // try eliminating this...
                //return this.CommonData.EnableMouseHook; 
            }
            set { if (this.CommonData.EnableMouseHook != value) { this.CommonData.EnableMouseHook = value; Save(); FirePropertyChanged("EnableMouseHook"); } }
        }

        public int RecentItemCount
        {
            get { return this.Data.RecentItemCount; }
            set { if (this.Data.RecentItemCount != value) { this.Data.RecentItemCount = value; Save(); FirePropertyChanged("RecentItemCount"); } }
        }

        public int RecentItemDays
        {
            get { return this.Data.RecentItemDays; }
            set { if (this.Data.RecentItemDays != value) { this.Data.RecentItemDays = value; Save(); FirePropertyChanged("RecentItemDays"); } }
        }

        public int RecentItemCollapseThresh
        {
            get { return this.Data.RecentItemCollapseThresh; }
            set { if (this.Data.RecentItemCollapseThresh != value) { this.Data.RecentItemCollapseThresh = value; Save(); FirePropertyChanged("RecentItemCollapseThresh"); } }
        }

        public string RecentItemOption
        {
            get { return this.Data.RecentItemOption; }
            set { if (this.Data.RecentItemOption != value) { this.Data.RecentItemOption = value; Save(); FirePropertyChanged("RecentItemOption"); } }
        }

        public string StartupParms
        {
            get { return this.CommonData.StartupParms; }
            set { if (this.CommonData.StartupParms != value) { this.CommonData.StartupParms = value; Save(); FirePropertyChanged("StartupParms"); } }
        }

        public List<string> PluginSources
        {
            get { return new List<string>(); }
            set {  }
        }
        
        public bool ShowMissingItems
        {
            get { return this.Data.ShowMissingItems; }
            set { if (this.Data.ShowMissingItems != value) { this.Data.ShowMissingItems = value; Save(); FirePropertyChanged("ShowMissingItems"); } }
        }

        public bool ShowUnairedItems
        {
            get { return this.Data.ShowUnairedItems; }
            set { if (this.Data.ShowUnairedItems != value) { this.Data.ShowUnairedItems = value; Save(); FirePropertyChanged("ShowUnairedItems"); } }
        }

        public bool RandomizeBackdrops
        {
            get { return this.Data.RandomizeBackdrops; }
            set { if (this.Data.RandomizeBackdrops != value) { this.Data.RandomizeBackdrops = value; Save(); FirePropertyChanged("RandomizeBackdrops"); } }
        }

        public bool RotateBackdrops
        {
            get { return this.Data.RotateBackdrops; }
            set { if (this.Data.RotateBackdrops != value) { this.Data.RotateBackdrops = value; Save(); FirePropertyChanged("RotateBackdrops"); } }
        }

        public int BackdropRotationInterval
        {
            get { return this.Data.BackdropRotationInterval; }
            set { if (this.Data.BackdropRotationInterval != value) { this.Data.BackdropRotationInterval = value; Save(); FirePropertyChanged("BackdropRotationInterval"); } }
        }

        public float BackdropTransitionInterval
        {
            get { return this.Data.BackdropTransitionInterval; }
            set { if (this.Data.BackdropTransitionInterval != value) { this.Data.BackdropTransitionInterval = (float)Math.Round(value, 1); Save(); FirePropertyChanged("BackdropTransitionInterval"); } }
        }

        public int BackdropLoadDelay
        {
            get { return this.Data.BackdropLoadDelay; }
            set { if (this.Data.BackdropLoadDelay != value) { this.Data.BackdropLoadDelay = value; Save(); FirePropertyChanged("BackdropLoadDelay"); } }
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
            set { if (this.Data.YearSortAsc != value) { this.Data.YearSortAsc = value; Save(); FirePropertyChanged("YearSortAsc"); } }
        }

        public bool AutoScrollText
        {
            get { return this.Data.AutoScrollText; }
            set { if (this.Data.AutoScrollText != value) { this.Data.AutoScrollText = value; Save(); FirePropertyChanged("AutoScrollText"); } }
        }
        public int AutoScrollDelay
        {
            get { return this.Data.AutoScrollDelay * 1000; } //Convert to milliseconds for MCML consumption
            set { if (this.Data.AutoScrollDelay != value) { this.Data.AutoScrollDelay = value; Save(); FirePropertyChanged("AutoScrollDelay"); } }
        }
        public int AutoScrollSpeed
        {
            get { return this.Data.AutoScrollSpeed; }
            set { if (this.Data.AutoScrollSpeed != value) { this.Data.AutoScrollSpeed = value; Save(); FirePropertyChanged("AutoScrollSpeed"); } }
        }

        public bool AutoValidate
        {
            get { return this.CommonData.AutoValidate; }
            set { if (this.CommonData.AutoValidate != value) { this.CommonData.AutoValidate = value; Save(); FirePropertyChanged("AutoValidate"); } }
        }

        public LogSeverity MinLoggingSeverity
        {
            get { return this.CommonData.MinLoggingSeverity; }
            set { if (this.CommonData.MinLoggingSeverity != value) { CommonData.MinLoggingSeverity = value; Save(); FirePropertyChanged("MinLoggingSeverity"); } }
        }

        [Comment(@"Enable screen Saver.")]
        public bool EnableScreenSaver
        {
            get { return this.Data.EnableScreenSaver; }
            set { if (this.Data.EnableScreenSaver != value) { this.Data.EnableScreenSaver = value; Save(); FirePropertyChanged("EnableScreenSaver"); } }
        }

        public int ScreenSaverTimeOut
        {
            get { return this.Data.ScreenSaverTimeOut; }
            set { if (this.Data.ScreenSaverTimeOut != value) { this.Data.ScreenSaverTimeOut = value; Save(); FirePropertyChanged("ScreenSaverTimeOut"); } }
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

        protected void FirePropertyChanged(string property)
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
