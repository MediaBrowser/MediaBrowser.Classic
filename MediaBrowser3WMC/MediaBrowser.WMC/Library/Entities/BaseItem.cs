using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Providers;
using MediaBrowser.Library.Entities.Attributes;
using System.Diagnostics;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.Library.Query;
using MediaBrowser.Library.Sorting;
using MediaBrowser.Library.Metadata;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Threading;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities {

    public class MetadataChangedEventArgs : EventArgs { }

    public class BaseItem
    {

        public LocationType LocationType { get; set; }

        public virtual bool IsMissing { get { return false; } }

        public virtual bool IsFuture { get { return false; } }

        public bool FullDetailsLoaded = false;

        public virtual string ApiId { get { return Id.ToString("N"); } }

        public EventHandler<MetadataChangedEventArgs> MetadataChanged;

        public Folder Parent { get; set; }

        public Folder TopParent
        {
            get
            {
                Folder parent = this.Parent;
                while (parent != null && parent.Parent != null && parent.Parent != Kernel.Instance.RootFolder)
                {
                    parent = parent.Parent;
                }
                return parent;
            }
        }
        public Guid TopParentID
        {
            get
            {
                return TopParent.Id;
            }
        }

        /// <summary>
        /// Finds a parent of a given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>``0.</returns>
        public T FindParent<T>()
            where T : Folder
        {
            var parent = Parent;

            while (parent != null)
            {
                var result = parent as T;
                if (result != null)
                {
                    return result;
                }

                parent = parent.Parent;
            }

            return null;
        }



        #region Images

        [Persist]
        public virtual string PrimaryImagePath { get; set; }
        [Persist]
        public virtual string SecondaryImagePath { get; set; }
        [Persist]
        public virtual string LogoImagePath { get; set; }
        [Persist]
        public virtual string ArtImagePath { get; set; }
        [Persist]
        public virtual string ThumbnailImagePath { get; set; }
        [Persist]
        public virtual string DiscImagePath { get; set; }
        [Persist]
        public virtual string BannerImagePath { get; set; }

        protected virtual bool InheritThumb { get { return true; } }
        protected virtual bool InheritBanner { get { return true; } }
        protected virtual bool InheritLogo { get { return true; } }
        protected virtual bool InheritArt { get { return true; } }

        public string BackdropImagePath {
            get {
                string path = null;
                if (BackdropImagePaths != null && BackdropImagePaths.Any()) {
                    path = BackdropImagePaths[0];
                }
                return path;
            }
            set {
                if (BackdropImagePaths == null) {
                    BackdropImagePaths = new List<string>();
                }
                if (BackdropImagePaths.Contains(value)) {
                    BackdropImagePaths.Remove(value);
                }
                BackdropImagePaths.Insert(0, value);
            }
        }

        [Persist]
        public virtual List<string> BackdropImagePaths { get; set; }


        public LibraryImage PrimaryImage {
            get {
                if (IsPlayable || this is Folder || this is Person )
                    return GetImage(PrimaryImagePath, true);
                else
                    return GetImage(PrimaryImagePath);
            }
        }

        public LibraryImage SecondaryImage {
            get {
                return GetImage(SecondaryImagePath);
            }
        }

        public LibraryImage ThumbnailImage {
            get {
                return InheritThumb ? SearchParents<LibraryImage>(this, item => item.GetImage(item.ThumbnailImagePath)) ?? PrimaryImage : GetImage(ThumbnailImagePath) ?? PrimaryImage;
            }
        }

        public LibraryImage DiscImage {
            get {
                return GetImage(DiscImagePath);
            }
        }

        // banner images will fall back to parent
        public LibraryImage BannerImage {
            get {
                return InheritBanner ? SearchParents<LibraryImage>(this, item => item.GetImage(item.BannerImagePath, Kernel.Instance.ConfigData.ProcessBanners)) : GetImage(BannerImagePath);
            }
        }

        // logo images will fall back to parent
        public LibraryImage LogoImage {
            get {
                return InheritLogo ? SearchParents<LibraryImage>(this, item => item.GetImage(item.LogoImagePath)) : GetImage(LogoImagePath);
            }
        }

        // art images will fall back to parent
        public LibraryImage ArtImage {
            get {
                return InheritArt ? SearchParents<LibraryImage>(this, item => item.GetImage(item.ArtImagePath)) : GetImage(ArtImagePath);
            }
        }

        static T SearchParents<T>(BaseItem item, Func<BaseItem, T> search) where T : class {
            var result = search(item);
            if (result == null && item.Parent != null) {
                result = SearchParents(item.Parent, search);
            }
            return result;
        }

        public LibraryImage BackdropImage {
            get {
                return GetImage(BackdropImagePath, Kernel.Instance.ConfigData.ProcessBackdrops);
            }
        }

        public LibraryImage PrimaryBackdropImage
        {
            get
            {
                if (BackdropImagePaths != null && BackdropImagePaths.Count != 0)
                {
                    return GetImage(BackdropImagePaths[0], Kernel.Instance.ConfigData.ProcessBackdrops);
                }
                else return null;
            }
        }


        public List<LibraryImage> BackdropImages {
            get {
                var images = new List<LibraryImage>();
                if (BackdropImagePaths == null)
                {
                    // inherit from parent
                    if (Parent != null)
                    {
                        BackdropImagePaths = Parent.BackdropImagePaths;
                    }
                }
                if (BackdropImagePaths != null) {
                    foreach (var path in BackdropImagePaths) {
                        var image = GetImage(path, Kernel.Instance.ConfigData.ProcessBackdrops);
                        if (image != null) images.Add(image);
                    }
                }  
                return images;
            }
        }

        private LibraryImage GetImage(string path)
        {
            return GetImage(path, false);
        }

        private LibraryImage GetImage(string path, bool canBeProcessed) {
            if (string.IsNullOrEmpty(path)) return null;
            return Kernel.Instance.GetImage(path, canBeProcessed, this);
        }

        #endregion

        [NotSourcedFromProvider]
        [Persist]
        public Guid Id { get; set; }

        [NotSourcedFromProvider]
        [Persist]
        public DateTime DateCreated { get; set; }

        [NotSourcedFromProvider]
        [Persist]
        public DateTime DateModified { get; set; }

        [NotSourcedFromProvider]
        [Persist]
        protected string defaultName;

        [NotSourcedFromProvider]
        [Persist]
        public string Path { get; set; }

        [Persist]
        public Single? ImdbRating { get; set; }

        [Persist]
        string name;

        public virtual string Name {
            get {
                return name ?? defaultName;
            }
            set {
                name = value;
            }
        }


        public virtual string LongName {
            get {
                return Name;
            }
        }

        [Persist]
        string sortName;

        public virtual string SortName { 
            get {
                return sortName ?? SortHelper.GetSortableName(Name);
            }
            set {
                sortName = value;
            }
        }


        [Persist]
        public virtual string Overview { get; set; }

        [Persist]
        public virtual string SubTitle { get; set; }

        [Persist]
        public virtual string DisplayMediaType { get; set; }

        [Persist]
        public string CustomRating { get; set; }
        [Persist]
        public string CustomPIN { get; set; }

        public UserItemDataDto UserData { get; set; }
        public string ApiParentId { get; set; }

        public int SpecialFeatureCount { get; set; }

        public virtual string OfficialRating
        {
            get
            {
                return ""; //will be implemented by sub-classes
            }
            set { }
        }

        public virtual string ShortDescription
        {
            get
            {
                return ""; //will be implemented by sub-classes
            }
            set { }
        }

        public virtual string TagLine
        {
            get
            {
                return ""; //will be implemented by sub-classes
            }
            set { }
        }

        public virtual bool Watched { get { return false; }
            set { }
        }

        public virtual List<Chapter> Chapters { get; set; }

        public bool IsOffline { get; set; }
        public bool IsExternalDisc { get; set; }

        public virtual bool CanResumeMain
        {
            get { return false; }
        }

        public bool CanDelete { get; set; }

        public virtual bool CanResume
        {
            get { return CanResumeMain; }
        }

        private bool _parentalAllowed = true;
        public bool ParentalAllowed { get { return _parentalAllowed; } set { _parentalAllowed = value; } } 
        public virtual string ParentalRating
        {
            get
            {
                if (string.IsNullOrEmpty(this.CustomRating)) {
                    if (this == Kernel.Instance.RootFolder)
                    {
                        //never want to block the root
                        return "None";
                    }
                    else
                    {
                        return OfficialRating;
                    }
                }           
                else
                    return this.CustomRating;
            }
        }

        public virtual bool PassesFilter(FilterProperties filters)
        {
            if (filters == null) return true;

            if (Ratings.Level(ParentalRating) > filters.RatedLessThan) return false;
            if (Ratings.Level(ParentalRating) < filters.RatedGreaterThan) return false;
            if (filters.IsFavorite != null && UserData != null && UserData.IsFavorite != filters.IsFavorite) return false;
            if (!filters.OfTypes.Contains(DisplayMediaType)) return false;

            return true;
        }

        public virtual bool IsFavorite
        {
            get { return UserData != null && UserData.IsFavorite; }
            set
            {
                if (UserData != null)
                {
                    if (UserData.IsFavorite != value)
                    {
                        UserData.IsFavorite = value;
                        Async.Queue(Async.ThreadPoolName.UpdateFav, () => Kernel.ApiClient.UpdateFavoriteStatus(ApiId, Kernel.CurrentUser.Id, value));
                    }
                }
                else
                {
                    UserData = new UserItemDataDto {IsFavorite = value};
                    Async.Queue(Async.ThreadPoolName.UpdateFav, () => Kernel.ApiClient.UpdateFavoriteStatus(ApiId, Kernel.CurrentUser.Id, value));
                }

            }
        }

        public virtual List<ThemeItem> ThemeSongs { get; set; }
        public virtual List<ThemeItem> ThemeVideos { get; set; }

        protected void LoadThemes()
        {
            try
            {
                var result = Kernel.ApiClient.GetAllThemeMedia(Kernel.CurrentUser.ApiId, ApiId);
                if (result == null) return;

                if (result.ThemeSongsResult.TotalRecordCount > 0)
                {
                    ThemeSongs = result.ThemeSongsResult.Items.Select(i => new ThemeItem {Id = i.Id, Path = i.Path}).ToList();
                }
                if (result.ThemeVideosResult.TotalRecordCount > 0)
                {
                    ThemeVideos = result.ThemeVideosResult.Items.Select(i => new ThemeItem {Id = i.Id, Path = i.Path}).ToList();
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error trying to load theme media", e);
            }
        }

        public virtual bool PlayAction(Item item)
        {
            //this will be overridden by sub-classes to perform the proper action for that item type
            Logger.ReportWarning("No play action defined for item type " + this.GetType() + " on item " + item.Name);
            return false;
        }

        public virtual bool SelectAction(Item item)
        {
            //this can be overridden by sub-classes to perform the proper action for that item type
            Application.CurrentInstance.Navigate(item);  //default is open the item
            return true;
        }

        /// <summary>
        /// Reload ourselves from the proper place
        /// </summary>
        /// <returns></returns>
        public virtual BaseItem ReLoad()
        {
            return Kernel.Instance.MB3ApiRepository.RetrieveItem(this.Id);
        }

        public virtual string CustomUI { get; set; }

        bool? isRemoteContent = null;
        public bool IsRemoteContent
        {
            get
            {
                if (isRemoteContent == null) { 
                    isRemoteContent = LocationType == LocationType.Remote || Path != null && Path.ToLower().StartsWith("http://");
                }
                return isRemoteContent.Value;
            }
        }

        public virtual Series OurSeries
        {
            get
            {
                //default baseItem has no series - return a valid blank item so MCML won't blow chow
                return Series.BlankSeries;
            }
        }

        public virtual bool IsPlayable
        {
            get
            {
                return PlaybackAllowed;
            }
        }

        public bool PlaybackAllowed { get; set; }

        bool? isTrailer = null;
        private IEnumerable<BaseItem> _additionalParts;
        private readonly object _partLock = new object();

        public bool IsTrailer
        {
            get
            {
                if (isTrailer == null)
                {
                    isTrailer = DisplayMediaType != null && DisplayMediaType.ToLower() == "trailer";
                }
                return isTrailer.Value;
            }
        }

        public DateTime PremierDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool HasEndDate { get { return EndDate > DateTime.MinValue; }}
        public List<string> ProductionLocations { get; set; }

        //counts
        public int MovieCount { get; set; }
        public int EpisodeCount { get; set; }
        public int SeriesCount { get; set; }
        public int GameCount { get; set; }
        public int TrailerCount { get; set; }
        public int SongCount { get; set; }
        public int AlbumCount { get; set; }
        public int MusicVideoCount { get; set; }

        [Persist]
        public virtual int? ProductionYear { get; set; }

        public int? ApiRecursiveItemCount { get; set; }
        public int? ApiItemCount { get; set; }

        public float? CriticRating { get; set; }
        public string CriticRatingSummary { get; set; }
        public float? MetaScore { get; set; }

        [Persist]
        public virtual string FirstAired { get; set; }

        public long RuntimeTicks { get; set; }
        public int PartCount { get; set; }

        public IEnumerable<BaseItem> AdditionalParts
        {
            get { return _additionalParts ?? (_additionalParts = GetAdditionalParts()); }
        }

        public bool IsChannelItem
        {
            get { return !(this is Channel) && !(Parent is Channel) && !(Parent is ChannelFolder); }
        }

        public virtual bool ShowUnwatchedCount {get { return true; }}

        // we may want to do this automatically, somewhere down the line
        public virtual bool AssignFromItem(BaseItem item) {
            // we should never reasign identity 
            Debug.Assert(item.Id == this.Id);
            if (item.Path != null)
                Debug.Assert(item.Path.ToLower() == this.Path.ToLower());
            Debug.Assert(item.GetType() == this.GetType());
            bool changed = false;
            //the following is to get around an anomoly with how directory creation dates seem to be returned from the actual item vs a shortcut to it
            //  I will attempt to re-write the date generations in a future release -ebr
            if (Kernel.Instance.ConfigData.EnableShortcutDateHack) 
            {
                changed = this.DateModified.ToShortDateString() != item.DateModified.ToShortDateString();
                changed |= this.DateCreated.ToShortDateString() != item.DateCreated.ToShortDateString();
            }
            else
            {
                changed = this.DateModified != item.DateModified;
                changed |= this.DateCreated != item.DateCreated;
            }
            changed |= this.defaultName != item.defaultName;
            //if (changed && Debugger.IsAttached) Debugger.Break();

            this.Path = item.Path;
            this.DateModified = item.DateModified;
            this.DateCreated = item.DateCreated;
            this.defaultName = item.defaultName;

            return changed;
        }

        protected void OnMetadataChanged(MetadataChangedEventArgs args) {
            if (MetadataChanged != null) {
                MetadataChanged(this, args);
            }
        }
        
        /// <summary>
        /// Refresh metadata on this item, will return true if the metadata changed  
        /// </summary>
        /// <returns>true if the metadata changed</returns>
        public bool RefreshMetadata() {
            return RefreshMetadata(MetadataRefreshOptions.Default);
        }

        /// <summary>
        /// Refresh metadata on this item, will return true if the metadata changed 
        /// </summary>
        /// <returns>True if the metadata changed</returns>
        public virtual bool RefreshMetadata(MetadataRefreshOptions options)
        {
            Logger.ReportVerbose("In refresh metadata for {0}",Name);
            //Just fire this if we are attached to model item it will refresh
            OnMetadataChanged(new MetadataChangedEventArgs());
            return true;

        }

        public void ReCacheAllImages()
        {
            if (this.PrimaryImage != null)
            {
                PrimaryImage.ClearLocalImages();
                this.PrimaryImage.GetLocalImagePath(); //no size - cache at original size
            }

            foreach (LibraryImage image in this.BackdropImages)
            {
                image.ClearLocalImages();
                image.GetLocalImagePath(); //force the backdrops to re-cache
            }
            if (this.BannerImage != null)
            {
                this.BannerImage.ClearLocalImages();
                this.BannerImage.GetLocalImagePath(); //and banner
            }
            if (this.LogoImage != null)
            {
                this.LogoImage.ClearLocalImages();
                this.LogoImage.GetLocalImagePath(); //and logo
            }
            if (this.ArtImage != null)
            {
                this.ArtImage.ClearLocalImages();
                this.ArtImage.GetLocalImagePath(); //and art
            }
            if (this.DiscImage != null)
            {
                this.DiscImage.ClearLocalImages();
                this.DiscImage.GetLocalImagePath(); //and disc
            }
            if (this.ThumbnailImage != null)
            {
                this.ThumbnailImage.ClearLocalImages();
                this.ThumbnailImage.GetLocalImagePath(); //and, finally, thumb
            }
        }

        public virtual void FillCustomValues(BaseItemDto mb3Item)
        {
            
        }

        public void MigrateAllImages()
        {
            if (this.PrimaryImage != null)
            {
                Logger.ReportInfo("Migrating primary image for " + Name );
                    this.PrimaryImage.MigrateFromOldID(); 
            }

            foreach (MediaBrowser.Library.ImageManagement.LibraryImage image in this.BackdropImages)
            {
                image.MigrateFromOldID(); 
            }
            if (this.BannerImage != null)
            {
                this.BannerImage.MigrateFromOldID(); 
            }
        }

        protected virtual IEnumerable<BaseItem> GetAdditionalParts()
        {
            lock (_partLock)
            {
                return Kernel.Instance.MB3ApiRepository.RetrieveAdditionalParts(ApiId);
            }
        }

    }
}
