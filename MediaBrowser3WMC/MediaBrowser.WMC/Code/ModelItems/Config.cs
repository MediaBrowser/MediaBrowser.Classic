using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Configuration;
using System.Reflection;
using System.Xml;

using Microsoft.MediaCenter.UI;
using MediaBrowser.Library;
using MediaBrowser.Library.Entities;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Attributes;
using Microsoft.MediaCenter;
using System.Diagnostics;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Code;
using MediaBrowser.Library.Logging;

namespace MediaBrowser
{

    public class Config : IModelItem
    {
        private ConfigData data;

        public bool AlwaysShowDetailsPage
        {
            get { return this.data.AlwaysShowDetailsPage; }
            set { if (this.data.AlwaysShowDetailsPage != value) { this.data.AlwaysShowDetailsPage = value; Save(); FirePropertyChanged("AlwaysShowDetailsPage"); } }
        }

        public int ParentalUnlockPeriod
        {
            get { return this.data.ParentalUnlockPeriod; }
            set { if (this.data.ParentalUnlockPeriod != value) { this.data.ParentalUnlockPeriod = value; Save(); FirePropertyChanged("ParentalUnlockPeriod"); } }
        }

        public bool ParentalControlEnabled
        {
            get
            {
                if (ParentalControlUnlocked)
                {
                    return false;
                }
                else return this.data.ParentalControlEnabled;
            }
            //we don't set this value if we are temp unlocked because it will get saved to config file as permanent - interface must force re-lock before manipulating this value
            set { if (!ParentalControlUnlocked) { this.data.ParentalControlEnabled = value; Save(); FirePropertyChanged("ParentalControlEnabled"); } }
        }
        public bool ParentalControlUnlocked
        {
            get { return Kernel.Instance.ParentalControls.Unlocked; }
            set { Kernel.Instance.ParentalControls.Unlocked = value; FirePropertyChanged("ParentalControlEnabled"); }
        }
        public string ParentalPIN
        {
            get { return this.data.ParentalPIN; }
            set { if (this.data.ParentalPIN != value) { this.data.ParentalPIN = value; Save(); FirePropertyChanged("ParentalPIN"); } }
        }
        public bool UnlockOnPinEntry
        {
            get { return this.data.UnlockOnPinEntry; }
            set { if (this.data.UnlockOnPinEntry != value) { this.data.UnlockOnPinEntry = value; Save(); FirePropertyChanged("UnlockOnPINEntry"); } }
        }
        public int MaxParentalLevel
        {
            get { return this.data.MaxParentalLevel; }
            set { if (this.data.MaxParentalLevel != value) { this.data.MaxParentalLevel = value; Save(); FirePropertyChanged("MaxParentalLevel"); } }
        }
        public string ParentalMaxAllowedString
        {
            get { return Kernel.Instance.ParentalControls.MaxAllowedString; }
        }
        public bool HideParentalDisAllowed
        {
            get { return this.data.HideParentalDisAllowed; }
            set
            {
                if (this.data.HideParentalDisAllowed != value)
                {
                    this.data.HideParentalDisAllowed = value;
                    Save();
                }
            }
        }


        public bool ParentalBlockUnrated
        {
            get { return this.data.ParentalBlockUnrated; }
            set
            {
                if (this.data.ParentalBlockUnrated != value)
                {
                    this.data.ParentalBlockUnrated = value;
                    Save();
                    Kernel.Instance.ParentalControls.SwitchUnrated(value);
                    FirePropertyChanged("ParentalBlockUnrated");
                }
            }
        }

        public bool EnableRootPage
        {
            get { return this.data.EnableRootPage; }
            set { if (this.data.EnableRootPage != value) { this.data.EnableRootPage = value; Save(); FirePropertyChanged("EnableRootPage"); } }
        }

        public bool ProcessBanners
        {
            get { return this.data.ProcessBanners; }
            set { if (this.data.ProcessBanners != value) { this.data.ProcessBanners = value; Save(); FirePropertyChanged("ProcessBanners"); } }
        }

        public bool ProcessBackdrops
        {
            get { return this.data.ProcessBackdrops; }
            set { if (this.data.ProcessBackdrops != value) { this.data.ProcessBackdrops = value; Save(); FirePropertyChanged("ProcessBackdrops"); } }
        }

        public bool AskIncludeChildrenRefresh
        {
            get { return this.data.AskIncludeChildrenRefresh; }
            set { if (this.data.AskIncludeChildrenRefresh != value) { this.data.AskIncludeChildrenRefresh = value; Save(); FirePropertyChanged("AskIncludeChildrenRefresh"); } }
        }

        public bool DefaultIncludeChildrenRefresh
        {
            get { return this.data.DefaultIncludeChildrenRefresh; }
            set { if (this.data.DefaultIncludeChildrenRefresh != value) { this.data.DefaultIncludeChildrenRefresh = value; Save(); FirePropertyChanged("DefaultIncludeChildrenRefresh"); } }
        }

        public bool IsFirstRun
        {
            get { return this.data.IsFirstRun; }
            set { if (this.data.IsFirstRun != value) { this.data.IsFirstRun = value; Save(); FirePropertyChanged("HasBeenConfigured"); } }
        }

        public bool CacheAllImagesInMemory
        {
            get { return this.data.CacheAllImagesInMemory; }
            set { if (this.data.CacheAllImagesInMemory != value) { this.data.CacheAllImagesInMemory = value; Save(); FirePropertyChanged("CacheAllImagesInMemory"); } }
        }

        public bool HideEmptyFolders
        {
            get { return this.data.HideEmptyFolders; }
            set { if (this.data.HideEmptyFolders != value) { this.data.HideEmptyFolders = value; Save(); FirePropertyChanged("HideEmptyFolders"); } }
        }

        [Comment(@"The current version of MB - will be the last version te first time we run so we can do something")]
        public string MBVersion
        {
            get { return this.data.MBVersion; }
            set { if (this.data.MBVersion != value) { this.data.MBVersion = value; Save(); FirePropertyChanged("MBVersion"); } }
        }
        [Comment("Synchronize the view for similar folder types")]
        public bool EnableSyncViews
        {
            get { return this.data.EnableSyncViews; }
            set { if (this.data.EnableSyncViews != value) { this.data.EnableSyncViews = value; Save(); FirePropertyChanged("EnableSyncViews"); } }

        }


        [Comment("Dim all unselected posters in poster and thumbstrib views")]
        public bool DimUnselectedPosters
        {
            get { return this.data.DimUnselectedPosters; }
            set { if (this.data.DimUnselectedPosters != value) { this.data.DimUnselectedPosters = value; Save(); FirePropertyChanged("DimUnselectedPosters"); } }
        }


        [Comment(@"Location of images to match to items by name.
            Can be used to provide images for indexing folders - genres, actors, directors etc.
            Should contain folders to match 
            item names each with banner, folder, backdrop images in jpg or png format. The folder 
            name needs to match the name returned by the source of the item (e.g. the folder/filename 
            without extension or name of the indexing folder) this is not necessarily the 
            metadata name displayed. Where names contain characters that are illegal in filenames 
            they should just be removed.")]
        public string ImageByNameLocation
        {
            get { return this.data.ImageByNameLocation; }
            set { if (this.data.ImageByNameLocation != value) { this.data.ImageByNameLocation = value; Save(); FirePropertyChanged("ImageByNameLocation"); } }
        }

        [Comment(@"Enables you to scan the display to cope with overscan issue, parameter should be of the for x,y,z scaling factors")]
        public Vector3 OverScanScaling
        {
            get { return this.data.OverScanScaling.ToMediaCenterVector3(); }
            set
            {
                if (this.data.OverScanScaling.ToMediaCenterVector3() != value)
                {
                    this.data.OverScanScaling = MediaBrowser.Code.ShadowTypes.Vector3.FromMediaCenterVector3(value);
                    Save();
                    FirePropertyChanged("OverScanScaling");
                }
            }
        }
        [Comment("Defines padding to apply round the edge of the screen to cope with overscan issues")]
        public Inset OverScanPadding
        {
            get { return this.data.OverScanPadding.ToMediaCenterInset(); }
            set
            {
                if (this.data.OverScanPadding.ToMediaCenterInset() != value)
                {
                    this.data.OverScanPadding = MediaBrowser.Code.ShadowTypes.Inset.FromMediaCenterInset(value);
                    Save();
                    FirePropertyChanged("OverScanPadding");
                }
            }
        }
        [Comment(@"Enables the writing of trace log files in a production environment to assist with problem solving")]
        public bool EnableTraceLogging
        {
            get { return this.data.EnableTraceLogging; }
            set { if (this.data.EnableTraceLogging != value) { this.data.EnableTraceLogging = value; Save(); FirePropertyChanged("EnableTraceLogging"); } }
        }
        [Comment(@"The default size of posters before change are made to the view settings")]
        public Size DefaultPosterSize
        {
            get
            {
                return this.data.DefaultPosterSize.ToMediaCenterSize();
            }
            set
            {
                if (this.data.DefaultPosterSize.ToMediaCenterSize() != value)
                {
                    this.data.DefaultPosterSize = MediaBrowser.Code.ShadowTypes.Size.FromMediaCenterSize(value);
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
            get { return this.data.GridSpacing.ToMediaCenterSize(); }
            set
            {
                if (this.data.GridSpacing.ToMediaCenterSize() != value)
                {
                    this.data.GridSpacing = MediaBrowser.Code.ShadowTypes.Size.FromMediaCenterSize(value);
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
            get { return this.data.ThumbStripPosterWidth; }
            set { if (this.data.ThumbStripPosterWidth != value) { this.data.ThumbStripPosterWidth = value; Save(); FirePropertyChanged("ThumbStripPosterWidth"); } }
        }

        public bool RememberIndexing
        {
            get { return this.data.RememberIndexing; }
            set { if (this.data.RememberIndexing != value) { this.data.RememberIndexing = value; Save(); FirePropertyChanged("RememberIndexing"); } }
        }

        public bool ShowIndexWarning
        {
            get { return this.data.ShowIndexWarning; }
            set { if (this.data.ShowIndexWarning != value) { this.data.ShowIndexWarning = value; Save(); FirePropertyChanged("ShowIndexWarning"); } }
        }

        public double IndexWarningThreshold
        {
            get { return this.data.IndexWarningThreshold; }
            set { if (this.data.IndexWarningThreshold != value) { this.data.IndexWarningThreshold = value; Save(); FirePropertyChanged("IndexWarningThreshold"); } }
        }

        [Comment(@"Controls the maximum difference between the actual aspect ration of a poster image and the thumbnails being displayed to allow the application to stretch the image non-proportionally.
            x = Abs( (image width/ image height) - (display width / display height) )
            if x is less than the configured value the imae will be stretched non-proportionally to fit the display size")]
        public float MaximumAspectRatioDistortion
        {
            get { return this.data.MaximumAspectRatioDistortion; }
            set { if (this.data.MaximumAspectRatioDistortion != value) { this.data.MaximumAspectRatioDistortion = value; Save(); FirePropertyChanged("MaximumAspectRatioDistortion"); } }
        }
        [Comment(@"Enable transcode 360 support on extenders")]
        public bool EnableTranscode360
        {
            get { return this.data.EnableTranscode360; }
            set { if (this.data.EnableTranscode360 != value) { this.data.EnableTranscode360 = value; Save(); FirePropertyChanged("EnableTranscode360"); } }
        }
        [Comment(@"A lower case comma delimited list of types the extender supports natively. Example: .dvr-ms,.wmv")]
        public string ExtenderNativeTypes
        {
            get { return this.data.ExtenderNativeTypes; }
            set { if (this.data.ExtenderNativeTypes != value) { this.data.ExtenderNativeTypes = value; Save(); FirePropertyChanged("ExtenderNativeTypes"); } }
        }
        [Comment("ShowThemeBackground [Default Value - False]\n\tTrue: Enables transparent background.\n\tFalse: Use default Video Browser background.")]
        public bool ShowThemeBackground
        {
            get { return this.data.ShowThemeBackground; }
            set { if (this.data.ShowThemeBackground != value) { this.data.ShowThemeBackground = value; Save(); FirePropertyChanged("ShowThemeBackground"); } }
        }
        [Comment("Example. If set to true the following will be treated as a movie and an automatic playlist will be created.\n\tIndiana Jones / Disc 1 / a.avi\n\tIndiana Jones / Disc 2 / b.avi")]
        public bool EnableNestedMovieFolders
        {
            get { return this.data.EnableNestedMovieFolders; }
            set { if (this.data.EnableNestedMovieFolders != value) { this.data.EnableNestedMovieFolders = value; Save(); FirePropertyChanged("EnableNestedMovieFolders"); } }
        }
        [Comment("Example. If set to true the following will be treated as a movie and an automatic playlist will be created.\n\tIndiana Jones / a.avi\n\tIndiana Jones / b.avi (This only works for 2 videos (no more))\n**Setting this to false will override EnableNestedMovieFolders if that is enabled.**")]
        public bool EnableMoviePlaylists
        {
            get { return this.data.EnableMoviePlaylists; }
            set { if (this.data.EnableMoviePlaylists != value) { this.data.EnableMoviePlaylists = value; Save(); FirePropertyChanged("EnableMoviePlaylists"); } }
        }
        [Comment("Limit to the number of video files that willbe assumed to be a single movie and create a playlist for")]
        public int PlaylistLimit
        {
            get { return this.data.PlaylistLimit; }
            set { if (this.data.PlaylistLimit != value) { this.data.PlaylistLimit = value; Save(); FirePropertyChanged("PlaylistLimit"); } }
        }
        [Comment("The starting folder for video browser. By default its set to MyVideos.\nCan be set to a folder for example c:\\ or a virtual folder for example c:\\folder.vf")]
        public string InitialFolder
        {
            get { return this.data.InitialFolder; }
            set { if (this.data.InitialFolder != value) { this.data.InitialFolder = value; Save(); FirePropertyChanged("InitialFolder"); } }
        }
        [Comment(@"Flag for auto-updates.  True will auto-update, false will not.")]
        public bool EnableUpdates
        {
            get { return this.data.EnableUpdates; }
            set { if (this.data.EnableUpdates != value) { this.data.EnableUpdates = value; Save(); FirePropertyChanged("EnableUpdates"); } }
        }
        [Comment(@"Flag for beta updates.  True will prompt you to update to beta versions.")]
        public bool EnableBetas
        {
            get { return this.data.EnableBetas; }
            set { if (this.data.EnableBetas != value) { this.data.EnableBetas = value; Save(); FirePropertyChanged("EnableBetas"); } }
        }
        [Comment(@"Set the location of the Daemon Tools binary..")]
        public string DaemonToolsLocation
        {
            get { return this.data.DaemonToolsLocation; }
            set { if (this.data.DaemonToolsLocation != value) { this.data.DaemonToolsLocation = value; Save(); FirePropertyChanged("DaemonToolsLocation"); } }
        }
        [Comment(@"The drive letter of the Daemon Tools virtual drive.")]
        public string DaemonToolsDrive
        {
            get { return this.data.DaemonToolsDrive; }
            set { if (this.data.DaemonToolsDrive != value) { this.data.DaemonToolsDrive = value; Save(); FirePropertyChanged("DaemonToolsDrive"); } }
        }
        [Comment("Flag for alphanumeric sorting.  True will use alphanumeric sorting, false will use alphabetic sorting.\nNote that the sorting algorithm is case insensitive.")]
        public bool EnableAlphanumericSorting
        {
            get { return this.data.EnableAlphanumericSorting; }
            set { if (this.data.EnableAlphanumericSorting != value) { this.data.EnableAlphanumericSorting = value; Save(); FirePropertyChanged("EnableAlphanumericSorting"); } }
        }
        [Comment(@"Enables the showing of tick in the list view for files that have been watched")]
        public bool EnableListViewTicks
        {
            get { return this.data.EnableListViewTicks; }
            set { if (this.data.EnableListViewTicks != value) { this.data.EnableListViewTicks = value; Save(); FirePropertyChanged("EnableListViewTicks"); } }
        }
        [Comment(@"Enables the showing of watched shows in a different color in the list view.")]
        public bool EnableListViewWatchedColor
        {
            get { return this.data.EnableListViewWatchedColor; }
            set { if (this.data.EnableListViewWatchedColor != value) { this.data.EnableListViewWatchedColor = value; Save(); FirePropertyChanged("EnableListViewWatchedColor"); } }
        }
         [Comment(@"Enables the showing of watched shows in a different color in the list view (Transparent disables it)")]
         public Colors ListViewWatchedColor
         {
             get
             {
                 return (Colors)(int)this.data.ListViewWatchedColor;
             }
             set { if ((int)this.data.ListViewWatchedColor != (int)value) { this.data.ListViewWatchedColor = (MediaBrowser.Code.ShadowTypes.Colors)(int)value; Save(); FirePropertyChanged("ListViewWatchedColor"); FirePropertyChanged("ListViewWatchedColorMcml"); } }
         }
         public Color ListViewWatchedColorMcml
         {
             get { return new Color(this.ListViewWatchedColor); }
         }
        public bool ShowUnwatchedCount
        {
            get { return this.data.ShowUnwatchedCount; }
            set { if (this.data.ShowUnwatchedCount != value) { this.data.ShowUnwatchedCount = value; Save(); FirePropertyChanged("ShowUnwatchedCount"); } }
        }

        public bool ShowUnwatchedIndicator
        {
            get { return this.data.ShowUnwatchedIndicator; }
            set { if (this.data.ShowUnwatchedIndicator != value) { this.data.ShowUnwatchedIndicator = value; Save(); FirePropertyChanged("ShowUnwatchedIndicator"); } }
        }

        public bool ShowWatchedTickOnFolders
        {
            get { return this.data.ShowWatchedTickOnFolders; }
            set { if (this.data.ShowWatchedTickOnFolders != value) { this.data.ShowWatchedTickOnFolders = value; Save(); FirePropertyChanged("ShowWatchedTickOnFolders"); } }
        }

        public bool ShowWatchTickInPosterView
        {
            get { return this.data.ShowWatchTickInPosterView; }
            set { if (this.data.ShowWatchTickInPosterView != value) { this.data.ShowWatchTickInPosterView = value; Save(); FirePropertyChanged("ShowWatchTickInPosterView"); } }
        }

        public bool ShowHDIndicatorOnPosters
        {
            get { return this.data.ShowHDIndicatorOnPosters; }
            set { if (this.data.ShowHDIndicatorOnPosters != value) { this.data.ShowHDIndicatorOnPosters = value; Save(); FirePropertyChanged("ShowHDIndicatorOnPosters"); } }
        }

        public bool ShowRemoteIndicatorOnPosters
        {
            get { return this.data.ShowRemoteIndicatorOnPosters; }
            set { if (this.data.ShowRemoteIndicatorOnPosters != value) { this.data.ShowRemoteIndicatorOnPosters = value; Save(); FirePropertyChanged("ShowRemoteIndicatorOnPosters"); } }
        }

        public bool ExcludeRemoteContentInSearch
        {
            get { return this.data.ExcludeRemoteContentInSearch; }
            set { if (this.data.ExcludeRemoteContentInSearch != value) { this.data.ExcludeRemoteContentInSearch = value; Save(); FirePropertyChanged("ExcludeRemoteContentInSearch"); } }
        }

        [Comment("Enables the views to default to the first unwatched item in a folder of movies or tv shows")]
        public bool DefaultToFirstUnwatched
        {
            get { return this.data.DefaultToFirstUnwatched; }
            set { if (this.data.DefaultToFirstUnwatched != value) { this.data.DefaultToFirstUnwatched = value; Save(); FirePropertyChanged("DefaultToFirstUnwatched"); } }
        }
        [Comment("When navigating, if only a single folder exists, enter it.")]
        public bool AutoEnterSingleDirs
        {
            get { return this.data.AutoEnterSingleDirs; }
            set { if (this.data.AutoEnterSingleDirs != value) { this.data.AutoEnterSingleDirs = value; Save(); FirePropertyChanged("AutoEnterSingleDirs"); } }
        }
        [Comment(@"Indicates that files with a date stamp before this date should be assumed to have been watched for the purpose of ticking them off.")]
        public DateTime AssumeWatchedBefore
        {
            get { return this.data.AssumeWatchedBefore; }
            set { if (this.data.AssumeWatchedBefore != value) { this.data.AssumeWatchedBefore = value; Save(); FirePropertyChanged("AssumeWatchedBefore"); FirePropertyChanged("AssumeWatchedBeforeStr"); } }
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
            get { return this.data.InheritDefaultView; }
            set { if (this.data.InheritDefaultView != value) { this.data.InheritDefaultView = value; Save(); FirePropertyChanged("InheritDefaultView"); } }
        }

        [Comment("Changes the default view index for folders that have not yet been visited.\n\t[Detail|Poster|Thumb]")]
        public ViewType DefaultViewType
        {
            get
            {
                try
                {
                    return (ViewType)Enum.Parse(typeof(ViewType), this.data.DefaultViewType);
                }
                catch
                {
                    return ViewType.Poster;
                }
            }
            set { if (this.data.DefaultViewType != value.ToString()) { this.data.DefaultViewType = value.ToString(); Save(); FirePropertyChanged("DefaultViewType"); } }
        }
        [Comment("Specifies whether the default Poster and Thumb views show labels")]
        public bool DefaultShowLabels
        {
            get { return this.data.DefaultShowLabels; }
            set { if (this.data.DefaultShowLabels != value) { this.data.DefaultShowLabels = value; Save(); FirePropertyChanged("DefaultShowLabels"); } }
        }
        [Comment("Specifies is the default for the Poster view is vertical scrolling")]
        public bool DefaultVerticalScroll
        {
            get { return this.data.DefaultVerticalScroll; }
            set { if (this.data.DefaultVerticalScroll != value) { this.data.DefaultVerticalScroll = value; Save(); FirePropertyChanged("DefaultVerticalScroll"); } }
        }
        [Comment(@"Limits the number of levels shown by the breadcrumbs.")]
        public int BreadcrumbCountLimit
        {
            get { return this.data.BreadcrumbCountLimit; }
            set { if (this.data.BreadcrumbCountLimit != value) { this.data.BreadcrumbCountLimit = value; Save(); FirePropertyChanged("BreadcrumbCountLimit"); } }
        }
        public bool AllowInternetMetadataProviders
        {
            get { return this.data.AllowInternetMetadataProviders; }
            set { if (this.data.AllowInternetMetadataProviders != value) { this.data.AllowInternetMetadataProviders = value; Save(); FirePropertyChanged("AllowInternetMetadataProviders"); } }
        }

        internal List<ConfigData.ExternalPlayer> ExternalPlayers
        {
            get { return this.data.ExternalPlayers; }
            //set { if (this.data.ExternalPlayers != value) { this.data.ExternalPlayers = value; Save(); FirePropertyChanged("ExternalPlayers"); } }
        }

        public bool UseAutoPlayForIso
        {
            get { return this.data.UseAutoPlayForIso; }
            set { if (this.data.UseAutoPlayForIso != value) { this.data.UseAutoPlayForIso = value; Save(); FirePropertyChanged("UseAutoPlayForIso"); } }
        }

        [Comment("List of characters to remove from titles for alphanumeric sorting.  Separate each character with a '|'.\nThis allows titles like '10,000.BC.2008.720p.BluRay.DTS.x264-hV.mkv' to be properly sorted.")]
        public string SortRemoveCharacters
        {
            get { return this.data.SortRemoveCharacters; }
            set { if (this.data.SortRemoveCharacters != value) { this.data.SortRemoveCharacters = value; Save(); FirePropertyChanged("SortRemoveCharacters"); } }
        }
        [Comment("List of characters to replace with a ' ' in titles for alphanumeric sorting.  Separate each character with a '|'.\nThis allows titles like 'Iron.Man.REPACK.720p.BluRay.x264-SEPTiC.mkv' to be properly sorted.")]
        public string SortReplaceCharacters
        {
            get { return this.data.SortReplaceCharacters; }
            set { if (this.data.SortReplaceCharacters != value) { this.data.SortReplaceCharacters = value; Save(); FirePropertyChanged("SortReplaceCharacters"); } }
        }
        [Comment(@"List of words to remove from alphanumeric sorting.  Separate each word with a '|'.  Note that the
        algorithm appends a ' ' to the end of each word during the search which means words found at the end
        of each title will not be removed.  This is generally not an issue since most people will only want
        articles removed and articles are rarely found at the end of media titles.  This, combined with SortReplaceCharacters,
        allows titles like 'The.Adventures.Of.Baron.Munchausen.1988.720p.BluRay.x264-SiNNERS.mkv' to be properly sorted.")]
        public string SortReplaceWords
        {
            get { return this.data.SortReplaceWords; }
            set { if (this.data.SortReplaceWords != value) { this.data.SortReplaceWords = value; Save(); FirePropertyChanged("SortReplaceWords"); } }
        }

        public string ViewTheme
        {
            get { return this.data.ViewTheme; }
            set { if (this.data.ViewTheme != value) { this.data.ViewTheme = value; Save(); FirePropertyChanged("ViewTheme"); } }
        }

        public string PreferredMetaDataLanguage
        {
            get { return this.data.PreferredMetaDataLanguage; }
            set { if (this.data.PreferredMetaDataLanguage != value) { this.data.PreferredMetaDataLanguage = value; Save(); FirePropertyChanged("PreferredMetaDataLanguage"); } }
        }

        public string UserSettingsPath
        {
            get { return this.data.UserSettingsPath; }
            set { if (this.data.UserSettingsPath != value) { this.data.UserSettingsPath = value; Save(); FirePropertyChanged("UserSettingsPath"); } }
        }

        public string Theme
        {
            get { return this.data.Theme; }
            set { if (this.data.Theme != value) { this.data.Theme = value; Save(); FirePropertyChanged("Theme"); } }
        }

        public string FontTheme
        {
            get { return this.data.FontTheme; }
            set { if (this.data.FontTheme != value) { this.data.FontTheme = value; Save(); FirePropertyChanged("FontTheme"); } }
        }

        [Comment(@"Enable clock onscreen.")]
        public bool ShowClock
        {
            get { return this.data.ShowClock; }
            set { if (this.data.ShowClock != value) { this.data.ShowClock = value; Save(); FirePropertyChanged("ShowClock"); } }
        }

        [Comment(@"Enable more advanced commands.")]
        public bool EnableAdvancedCmds
        {
            get { return this.data.EnableAdvancedCmds; }
            set { if (this.data.EnableAdvancedCmds != value) { this.data.EnableAdvancedCmds = value; Save(); FirePropertyChanged("EnableAdvancedCmds"); } }
        }

        [Comment(@"Advanced Command: Enable Delete")]
        public bool Advanced_EnableDelete
        {
            get { return this.data.Advanced_EnableDelete; }
            set { if (this.data.Advanced_EnableDelete != value) { this.data.Advanced_EnableDelete = value; Save(); FirePropertyChanged("Advanced_EnableDelete"); } }
        }

        [Comment(@"Show backdrop on main views.")]
        public bool ShowBackdrop
        {
            get { return this.data.ShowBackdrop; }
            set { if (this.data.ShowBackdrop != value) { this.data.ShowBackdrop = value; Save(); FirePropertyChanged("ShowBackdrop"); } }
        }


        [Comment(@"The name displayed in the top right when you first navigate into your library")]
        public string InitialBreadcrumbName
        {
            get { return this.data.InitialBreadcrumbName; }
            set
            {
                if (this.data.InitialBreadcrumbName != value)
                {
                    this.data.InitialBreadcrumbName = value;
                    Save();
                    FirePropertyChanged("InitialBreadcrumbName");
                }
            }
        }

        public bool ShowConfigButton
        {
            get { return this.data.ShowConfigButton; }
            set { if (this.data.ShowConfigButton != value) { this.data.ShowConfigButton = value; Save(); FirePropertyChanged("ShowConfigButton"); } }
        }

        public int AlphaBlending
        {
            get { return this.data.AlphaBlending; }
            set { if (this.data.AlphaBlending != value) { this.data.AlphaBlending = value; Save(); FirePropertyChanged("AlphaBlending"); } }
        }
        public string YahooWeatherFeed
        {
            get { return this.data.YahooWeatherFeed; }
            set { if (this.data.YahooWeatherFeed != value) { this.data.YahooWeatherFeed = value; Save(); FirePropertyChanged("YahooWeatherFeed"); } }
        }
        public string YahooWeatherUnit
        {
            get { return this.data.YahooWeatherUnit; }
            set { if (this.data.YahooWeatherUnit != value) { this.data.YahooWeatherUnit = value; Save(); FirePropertyChanged("YahooWeatherUnit"); } }
        }
        public string SupporterKey
        {
            get { return this.data.SupporterKey; }
            set { if (this.data.SupporterKey != value) { this.data.SupporterKey = value; FirePropertyChanged("SupporterKey"); } }
        }

        public string PodcastHome
        {
            get { return this.data.PodcastHome; }
            set { if (this.data.PodcastHome != value) { this.data.PodcastHome = value; Save(); FirePropertyChanged("PodcastHome"); } }
        }
        public bool HideFocusFrame
        {
            get { return this.data.HideFocusFrame; }
            set { if (this.data.HideFocusFrame != value) { this.data.HideFocusFrame = value; Save(); FirePropertyChanged("HideFocusFrame"); } }
        }

        public bool EnableProxyLikeCaching
        {
            get { return this.data.EnableProxyLikeCaching; }
            set { if (this.data.EnableProxyLikeCaching != value) { this.data.EnableProxyLikeCaching = value; Save(); FirePropertyChanged("EnableProxyLikeCaching"); } }
        }

        public int MetadataCheckForUpdateAge
        {
            get { return this.data.MetadataCheckForUpdateAge; }
            set { if (this.data.MetadataCheckForUpdateAge != value) { this.data.MetadataCheckForUpdateAge = value; Save(); FirePropertyChanged("MetadataCheckForUpdateAge"); } }
        }
        public bool ShowRootBackground
        {
            get { return this.data.ShowRootBackground; }
            set { if (this.data.ShowRootBackground != value) { this.data.ShowRootBackground = value; Save(); FirePropertyChanged("ShowRootBackground"); } }
        }

        public bool EnableMouseHook
        {
            get { return this.data.EnableMouseHook; }
            set { if (this.data.EnableMouseHook != value) { this.data.EnableMouseHook = value; Save(); FirePropertyChanged("EnableMouseHook"); } }
        }

        public int RecentItemCount
        {
            get { return this.data.RecentItemCount; }
            set { if (this.data.RecentItemCount != value) { this.data.RecentItemCount = value; Save(); FirePropertyChanged("RecentItemCount"); } }
        }

        public int RecentItemDays
        {
            get { return this.data.RecentItemDays; }
            set { if (this.data.RecentItemDays != value) { this.data.RecentItemDays = value; Save(); FirePropertyChanged("RecentItemDays"); } }
        }

        public int RecentItemCollapseThresh
        {
            get { return this.data.RecentItemCollapseThresh; }
            set { if (this.data.RecentItemCollapseThresh != value) { this.data.RecentItemCollapseThresh = value; Save(); FirePropertyChanged("RecentItemCollapseThresh"); } }
        }

        public string RecentItemOption
        {
            get { return this.data.RecentItemOption; }
            set { if (this.data.RecentItemOption != value) { this.data.RecentItemOption = value; Save(); FirePropertyChanged("RecentItemOption"); } }
        }

        public List<string> PluginSources
        {
            get { return this.data.PluginSources; }
            set { if (this.data.PluginSources != value) { this.data.PluginSources = value; Save(); FirePropertyChanged("PluginSources"); } }
        }
        
        public bool PNGTakesPrecedence
        {
            get { return this.data.PNGTakesPrecedence; }
            set { if (this.data.PNGTakesPrecedence != value) { this.data.PNGTakesPrecedence = value; Save(); FirePropertyChanged("PNGTakesPrecedence"); } }
        }

        public bool RandomizeBackdrops
        {
            get { return this.data.RandomizeBackdrops; }
            set { if (this.data.RandomizeBackdrops != value) { this.data.RandomizeBackdrops = value; Save(); FirePropertyChanged("RandomizeBackdrops"); } }
        }

        public bool RotateBackdrops
        {
            get { return this.data.RotateBackdrops; }
            set { if (this.data.RotateBackdrops != value) { this.data.RotateBackdrops = value; Save(); FirePropertyChanged("RotateBackdrops"); } }
        }

        public int BackdropRotationInterval
        {
            get { return this.data.BackdropRotationInterval; }
            set { if (this.data.BackdropRotationInterval != value) { this.data.BackdropRotationInterval = value; Save(); FirePropertyChanged("BackdropRotationInterval"); } }
        }

        public float BackdropTransitionInterval
        {
            get { return this.data.BackdropTransitionInterval; }
            set { if (this.data.BackdropTransitionInterval != value) { this.data.BackdropTransitionInterval = (float)Math.Round(value, 1); Save(); FirePropertyChanged("BackdropTransitionInterval"); } }
        }

        public int BackdropLoadDelay
        {
            get { return this.data.BackdropLoadDelay; }
            set { if (this.data.BackdropLoadDelay != value) { this.data.BackdropLoadDelay = value; Save(); FirePropertyChanged("BackdropLoadDelay"); } }
        }
        
        public int FullRefreshInterval
        {
            get { return Kernel.Instance.ServiceConfigData.FullRefreshInterval; }
            //set { if (this.data.FullRefreshInterval != value) { this.data.FullRefreshInterval = value; Save(); FirePropertyChanged("FullRefreshInterval"); } }
        }

        public bool ServiceRefreshFailed
        {
            get { return Kernel.Instance.ServiceConfigData.RefreshFailed; }
            //set { if (this.data.FullRefreshInterval != value) { this.data.FullRefreshInterval = value; Save(); FirePropertyChanged("FullRefreshInterval"); } }
        }

        public DateTime LastFullRefresh
        {
            get { return Kernel.Instance.ServiceConfigData.LastFullRefresh;  }
            //set { if (this.data.LastFullRefresh != value) { this.data.LastFullRefresh = value; Save(); FirePropertyChanged("LastFullRefresh"); } }
        }

        public int MinResumeDuration
        {
            get { return this.data.MinResumeDuration; }
            set { if (this.data.MinResumeDuration != value) { this.data.MinResumeDuration = value; Save(); FirePropertyChanged("MinResumeDuration"); } }
        }

        public int MinResumePct
        {
            get { return this.data.MinResumePct; }
            set { if (this.data.MinResumePct != value) { this.data.MinResumePct = value; Save(); FirePropertyChanged("MinResumePct"); } }
        }

        public int MaxResumePct
        {
            get { return this.data.MaxResumePct; }
            set { if (this.data.MaxResumePct != value) { this.data.MaxResumePct = value; Save(); FirePropertyChanged("MaxResumePct"); } }
        }

         public bool YearSortAsc
        {
            get { return this.data.YearSortAsc; }
            set { if (this.data.YearSortAsc != value) { this.data.YearSortAsc = value; Save(); FirePropertyChanged("YearSortAsc"); } }
        }

        public bool AutoScrollText
        {
            get { return this.data.AutoScrollText; }
            set { if (this.data.AutoScrollText != value) { this.data.AutoScrollText = value; Save(); FirePropertyChanged("AutoScrollText"); } }
        }
        public int AutoScrollDelay
        {
            get { return this.data.AutoScrollDelay * 1000; } //Convert to milliseconds for MCML consumption
            set { if (this.data.AutoScrollDelay != value) { this.data.AutoScrollDelay = value; Save(); FirePropertyChanged("AutoScrollDelay"); } }
        }
        public int AutoScrollSpeed
        {
            get { return this.data.AutoScrollSpeed; }
            set { if (this.data.AutoScrollSpeed != value) { this.data.AutoScrollSpeed = value; Save(); FirePropertyChanged("AutoScrollSpeed"); } }
        }

        public bool AutoValidate
        {
            get { return this.data.AutoValidate; }
            set { if (this.data.AutoValidate != value) { this.data.AutoValidate = value; Save(); FirePropertyChanged("AutoValidate"); } }
        }

        public bool SaveLocalMeta
        {
            get { return this.data.SaveLocalMeta; }
            set { if (this.data.SaveLocalMeta != value) { this.data.SaveLocalMeta = value; Save(); FirePropertyChanged("SaveLocalMeta"); } }
        }

        public LogSeverity MinLoggingSeverity
        {
            get { return this.data.MinLoggingSeverity; }
            set { if (this.data.MinLoggingSeverity != value) { this.data.MinLoggingSeverity = value; Save(); FirePropertyChanged("MinLoggingSeverity"); } }
        }

        [Comment(@"Enable screen Saver.")]
        public bool EnableScreenSaver
        {
            get { return this.data.EnableScreenSaver; }
            set { if (this.data.EnableScreenSaver != value) { this.data.EnableScreenSaver = value; Save(); FirePropertyChanged("EnableScreenSaver"); } }
        }

        public int ScreenSaverTimeOut
        {
            get { return this.data.ScreenSaverTimeOut; }
            set { if (this.data.ScreenSaverTimeOut != value) { this.data.ScreenSaverTimeOut = value; Save(); FirePropertyChanged("ScreenSaverTimeOut"); } }
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

        private static Config _instance = new Config();
        public static Config Instance
        {
            get
            {
                return _instance;
            }
        }

        bool isValid;
        private Config()
        {
            isValid = Load();
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
                this.data.Save();
        }

        public void Reset()
        {
            lock (this)
            {
                this.data = new ConfigData();
                Save();
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

        private bool Load()
        {
            try
            {
                this.data = ConfigData.FromFile(ApplicationPaths.ConfigFile);
                return true;
            }
            catch (Exception ex)
            {
                MediaCenterEnvironment ev = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
                DialogResult r = ev.Dialog(ex.Message + "\n" + Application.CurrentInstance.StringData("ConfigErrorDial"), Application.CurrentInstance.StringData("ConfigErrorCapDial"), DialogButtons.Yes | DialogButtons.No, 600, true);
                if (r == DialogResult.Yes)
                {
                    this.data = new ConfigData();
                    Save();
                    return true;
                }
                else
                    return false;
            }
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
