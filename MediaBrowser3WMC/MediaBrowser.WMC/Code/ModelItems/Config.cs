using System;
using System.Collections.Generic;
using System.Reflection;
using MediaBrowser.Model.Updates;
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
        //public string ParentalPIN
        //{
        //    get { return this.data.ParentalPIN; }
        //    set { if (this.data.ParentalPIN != value) { this.data.ParentalPIN = value; Save(); FirePropertyChanged("ParentalPIN"); } }
        //}
        //public bool UnlockOnPinEntry
        //{
        //    get { return this.data.UnlockOnPinEntry; }
        //    set { if (this.data.UnlockOnPinEntry != value) { this.data.UnlockOnPinEntry = value; Save(); FirePropertyChanged("UnlockOnPINEntry"); } }
        //}
        //public int MaxParentalLevel
        //{
        //    get { return this.data.MaxParentalLevel; }
        //    set { if (this.data.MaxParentalLevel != value) { this.data.MaxParentalLevel = value; Save(); FirePropertyChanged("MaxParentalLevel"); } }
        //}
        //public string ParentalMaxAllowedString
        //{
        //    get { return Kernel.Instance.ParentalControls.MaxAllowedString; }
        //}
        //public bool HideParentalDisAllowed
        //{
        //    get { return this.data.HideParentalDisAllowed; }
        //    set
        //    {
        //        if (this.data.HideParentalDisAllowed != value)
        //        {
        //            this.data.HideParentalDisAllowed = value;
        //            Save();
        //        }
        //    }
        //}


        //public bool ParentalBlockUnrated
        //{
        //    get { return this.data.ParentalBlockUnrated; }
        //    set
        //    {
        //        if (this.data.ParentalBlockUnrated != value)
        //        {
        //            this.data.ParentalBlockUnrated = value;
        //            Save();
        //            Kernel.Instance.ParentalControls.SwitchUnrated(value);
        //            FirePropertyChanged("ParentalBlockUnrated");
        //        }
        //    }
        //}

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
            set { if (this.Data.ShowFavoritesCollection != value) { this.Data.ShowFavoritesCollection = value; Save(); FirePropertyChanged("ShowFavoritesCollection"); Application.CurrentInstance.ReLoad(); } }
        }

        public bool ShowNewItemNotification
        {
            get { return this.Data.ShowNewItemNotification; }
            set { if (this.Data.ShowNewItemNotification != value) { this.Data.ShowNewItemNotification = value; Save(); FirePropertyChanged("ShowNewItemNotification"); Application.CurrentInstance.ReLoad(); } }
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

        public bool ShowIndexWarning
        {
            get { return this.Data.ShowIndexWarning; }
            set { if (this.Data.ShowIndexWarning != value) { this.Data.ShowIndexWarning = value; Save(); FirePropertyChanged("ShowIndexWarning"); } }
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
        [Comment(@"Flag for beta updates.  True will prompt you to update to beta versions.")]
        //public bool EnableBetas
        //{
        //    get { return this.data.EnableBetas; }
        //    set { if (this.data.EnableBetas != value) { this.data.EnableBetas = value; Save(); FirePropertyChanged("EnableBetas"); } }
        //}
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

        //public string PreferredMetaDataLanguage
        //{
        //    get { return this.data.PreferredMetaDataLanguage; }
        //    set { if (this.data.PreferredMetaDataLanguage != value) { this.data.PreferredMetaDataLanguage = value; Save(); FirePropertyChanged("PreferredMetaDataLanguage"); } }
        //}

        //public string UserSettingsPath
        //{
        //    get { return this.data.UserSettingsPath; }
        //    set { if (this.data.UserSettingsPath != value) { this.data.UserSettingsPath = value; Save(); FirePropertyChanged("UserSettingsPath"); } }
        //}

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


        [Comment(@"The name displayed in the top right when you first navigate into your library")]
        public string InitialBreadcrumbName
        {
            get { return this.CommonData.InitialBreadcrumbName; }
            set
            {
                if (this.CommonData.InitialBreadcrumbName != value)
                {
                    this.CommonData.InitialBreadcrumbName = value;
                    Save();
                    FirePropertyChanged("InitialBreadcrumbName");
                }
            }
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
            get { return this.Data.YahooWeatherFeed; }
            set { if (this.Data.YahooWeatherFeed != value) { this.Data.YahooWeatherFeed = value; Save(); FirePropertyChanged("YahooWeatherFeed"); } }
        }
        public string YahooWeatherUnit
        {
            get { return this.Data.YahooWeatherUnit; }
            set { if (this.Data.YahooWeatherUnit != value) { this.Data.YahooWeatherUnit = value; Save(); FirePropertyChanged("YahooWeatherUnit"); } }
        }
        public string SupporterKey
        {
            get { return this.Data.SupporterKey; }
            set { if (this.Data.SupporterKey != value) { this.Data.SupporterKey = value; FirePropertyChanged("SupporterKey"); } }
        }

        //public string PodcastHome
        //{
        //    get { return this.data.PodcastHome; }
        //    set { if (this.data.PodcastHome != value) { this.data.PodcastHome = value; Save(); FirePropertyChanged("PodcastHome"); } }
        //}
        public bool HideFocusFrame
        {
            get { return this.Data.HideFocusFrame; }
            set { if (this.Data.HideFocusFrame != value) { this.Data.HideFocusFrame = value; Save(); FirePropertyChanged("HideFocusFrame"); } }
        }

        //public bool EnableProxyLikeCaching
        //{
        //    get { return this.data.EnableProxyLikeCaching; }
        //    set { if (this.data.EnableProxyLikeCaching != value) { this.data.EnableProxyLikeCaching = value; Save(); FirePropertyChanged("EnableProxyLikeCaching"); } }
        //}

        //public int MetadataCheckForUpdateAge
        //{
        //    get { return this.data.MetadataCheckForUpdateAge; }
        //    set { if (this.data.MetadataCheckForUpdateAge != value) { this.data.MetadataCheckForUpdateAge = value; Save(); FirePropertyChanged("MetadataCheckForUpdateAge"); } }
        //}
        public bool ShowRootBackground
        {
            get { return this.Data.ShowRootBackground; }
            set { if (this.Data.ShowRootBackground != value) { this.Data.ShowRootBackground = value; Save(); FirePropertyChanged("ShowRootBackground"); } }
        }

        public bool EnableMouseHook
        {
            get { return this.CommonData.EnableMouseHook; }
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

        public List<string> PluginSources
        {
            get { return this.CommonData.PluginSources; }
            set { if (this.CommonData.PluginSources != value) { this.CommonData.PluginSources = value; Save(); FirePropertyChanged("PluginSources"); } }
        }
        
        //public bool PNGTakesPrecedence
        //{
        //    get { return this.data.PNGTakesPrecedence; }
        //    set { if (this.data.PNGTakesPrecedence != value) { this.data.PNGTakesPrecedence = value; Save(); FirePropertyChanged("PNGTakesPrecedence"); } }
        //}

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

        //public int MinResumeDuration
        //{
        //    get { return this.data.MinResumeDuration; }
        //    set { if (this.data.MinResumeDuration != value) { this.data.MinResumeDuration = value; Save(); FirePropertyChanged("MinResumeDuration"); } }
        //}

        //public int MinResumePct
        //{
        //    get { return this.data.MinResumePct; }
        //    set { if (this.data.MinResumePct != value) { this.data.MinResumePct = value; Save(); FirePropertyChanged("MinResumePct"); } }
        //}

        //public int MaxResumePct
        //{
        //    get { return this.data.MaxResumePct; }
        //    set { if (this.data.MaxResumePct != value) { this.data.MaxResumePct = value; Save(); FirePropertyChanged("MaxResumePct"); } }
        //}

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

        //public bool SaveLocalMeta
        //{
        //    get { return this.data.SaveLocalMeta; }
        //    set { if (this.data.SaveLocalMeta != value) { this.data.SaveLocalMeta = value; Save(); FirePropertyChanged("SaveLocalMeta"); } }
        //}

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
