using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MediaBrowser.Library.Query;
using MediaBrowser.Library.Util;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Localization;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Util;

namespace MediaBrowser.Library.Entities {

    public class ChildrenChangedEventArgs : EventArgs {
        public bool FolderContentChanged { get; set; }
    }

    public class UnwatchedChangedEventArgs : EventArgs {
        public int CountAdjustment { get; set; }
    }

    public class Folder : BaseItem, IFolder {

        public event EventHandler<ChildrenChangedEventArgs> ChildrenChanged;
        public event EventHandler<EventArgs> QuickListChanged;
        public event EventHandler<UnwatchedChangedEventArgs> UnwatchedCountChanged;

        MediaBrowser.Library.Util.Lazy<List<BaseItem>> children;
        protected IFolderMediaLocation location;
        protected IComparer<BaseItem> sortFunction;
        object validateChildrenLock = new object();
        public MBDirectoryWatcher directoryWatcher;
        Type childType;
        protected IndexFolder quickListFolder;
        public Model.Entities.DisplayPreferences DisplayPreferences { get; set; }
        public virtual string DisplayPreferencesId { get; set; }
        private string _collectionType;
        public string CollectionType
        {
            get { return _collectionType ?? (Parent != null ? Parent.CollectionType : null); }
            set { _collectionType = value; }
        }

        public Folder()
            : base() {
            RetrieveChildren();
        }

        public IComparer<BaseItem> SortFunction
        {
            get
            {
                return this.sortFunction;
            }
        }

        private Dictionary<string, IComparer<BaseItem>> sortOrderOptions= new Dictionary<string,IComparer<BaseItem>>() { 
            {LocalizedStrings.Instance.GetString("NameDispPref"), new BaseItemComparer(SortOrder.Name)},
            {LocalizedStrings.Instance.GetString("DateDispPref"), new BaseItemComparer(SortOrder.Date)},
            {LocalizedStrings.Instance.GetString("RatingDispPref"), new BaseItemComparer(SortOrder.Rating)},
            {LocalizedStrings.Instance.GetString("CriticRatingDispPref"), new BaseItemComparer(SortOrder.CriticRating)},
            {LocalizedStrings.Instance.GetString("UserRatingDispPref"), new BaseItemComparer(SortOrder.UserRating)},
            {LocalizedStrings.Instance.GetString("RuntimeDispPref"), new BaseItemComparer(SortOrder.Runtime)},
            {LocalizedStrings.Instance.GetString("UnWatchedDispPref"), new BaseItemComparer(SortOrder.Unwatched)},
            {LocalizedStrings.Instance.GetString("YearDispPref"), new BaseItemComparer(SortOrder.Year)}
        };
        //Dynamic Choice Items - these can be overidden or added to by sub-classes to provide for different options for different item types
        /// <summary>
        /// Dictionary of sort options - consists of a localized display string and an IComparer(Baseitem) for the sort
        /// </summary>
        public virtual Dictionary<string, IComparer<BaseItem>> SortOrderOptions
        {
            get { return sortOrderOptions; }
            set { sortOrderOptions = value; }
        }
        private Dictionary<string, string> indexByOptions = new Dictionary<string, string>() { 
            {LocalizedStrings.Instance.GetString("NoneDispPref"), ""}, 
            {LocalizedStrings.Instance.GetString("ActorDispPref"), "Actors"},
            {LocalizedStrings.Instance.GetString("GenreDispPref"), "Genres"},
            {LocalizedStrings.Instance.GetString("DirectorDispPref"), "Directors"},
            {LocalizedStrings.Instance.GetString("YearDispPref"), "ProductionYear"},
            {LocalizedStrings.Instance.GetString("OfficialRatingDispPref"), "MpaaRating"},
            {LocalizedStrings.Instance.GetString("StudioDispPref"), "Studios"}
        };
        /// <summary>
        /// Dictionary of index options - consists of a display value and a property name (must match the property exactly)
        /// </summary>
        public virtual Dictionary<string, string> IndexByOptions
        {
            get { return indexByOptions; }
            set { indexByOptions = value; }
        }

        private FilterProperties _filters;
        public FilterProperties Filters
        {
            get { return _filters ?? (_filters = GetFilterProperties()); }
            set { _filters = value; }
        }

        private FilterProperties GetFilterProperties()
        {
            var filters = new FilterProperties();
            if (Parent != null) // Don't ever filter the root
            {
                if (DisplayPreferences == null) LoadDisplayPreferences();
                if (DisplayPreferences != null)
                {
                    filters.IsUnWatched = Parent != null && Parent.Parent != null ? Parent.Filters.IsUnWatched :Boolean.Parse(DisplayPreferences.CustomPrefs.GetValueOrDefault("IsUnWatched", Boolean.FalseString));
                    filters.IsFavorite = DisplayPreferences.CustomPrefs.GetValueOrDefault("IsFavorite", null) == Boolean.TrueString;
                }
            }
            return filters;
        }

        public void SetFilterUnWatched(bool value)
        {
            Filters.IsUnWatched = value;
            if (DisplayPreferences != null)
            {
                if (value)
                    DisplayPreferences.CustomPrefs["IsUnWatched"] = Boolean.TrueString;
                else
                {
                    DisplayPreferences.CustomPrefs.Remove("IsUnWatched");
                }
            }
        }

        public void SetFilterFavorite(bool value)
        {
            Filters.IsFavorite = value;
            DisplayPreferences.CustomPrefs["IsFavorite"] = value.ToString();
        }

        /// <summary>
        /// By default children are loaded on first access, this operation is slow. So sometimes you may
        ///  want to force the children to load;
        /// </summary>
        public virtual void EnsureChildrenLoaded() {
            var ignore = ActualChildren;
        }

        public IFolderMediaLocation FolderMediaLocation {
            get {
                if (location == null) {
                    location = Kernel.Instance.GetLocation<IFolderMediaLocation>(Path);
                }
                return location;
            }
        }

        public Type ChildType
        {
            get {
                if (childType == null)
                {
                    if (ActualChildren.Count > 0)
                        childType = ActualChildren[0].GetType();
                    else
                        return typeof(BaseItem);
                }
                return childType; 
            }
        }

        public bool ContainsMusic { get { return children != null && children.Value.Any(i => i is MusicAlbum || i is MusicArtist || i is Song); } }

        public override bool PlayAction(Item item)
        {
            //set our flag to show the popup menu
            return Application.CurrentInstance.DisplayPopupPlay = true;
        }

        public override bool IsPlayable
        {
            get { return Application.CurrentInstance.PlaybackEnabled; }
        }

        public virtual void RetrieveChildren()
        {
            children = ApiId != null ? new Lazy<List<BaseItem>>(() => GetChildren(true), () => OnChildrenChanged(null)) : new Lazy<List<BaseItem>>(() => new List<BaseItem>(), null);
            mediaCount = null;
        }

        public void ReloadChildren()
        {
            RetrieveChildren();
        }

        public virtual bool PromptForChildRefresh
        {
            get
            {
                return Kernel.Instance.ConfigData.AskIncludeChildrenRefresh;
            }
        }

        public virtual bool DefaultIncludeChildrenRefresh
        {
            get
            {
                return Kernel.Instance.ConfigData.DefaultIncludeChildrenRefresh;
            }
        }

        protected virtual bool HideEmptyFolders { get { return Kernel.Instance.ConfigData.HideEmptyFolders; } }

        /// <summary>
        /// Returns a safe clone of the children
        /// </summary>
        public IList<BaseItem> Children {
            get {
                if (ActualChildren == null)
                {
                    Logger.ReportWarning("Actual Children NULL for "+ Name + "/" + Path);
                    return new List<BaseItem>();
                }
                // return a clone
                lock (ActualChildren)
                {
                    //once again, the hide empty folders feature is a problem.  causes a recursive load on big libraries
                    IList<BaseItem> visibleChildren = ActualChildren.Where(IsVisible).ToList();
                    return HideEmptyFolders ? visibleChildren.Where(i => !(i is Folder) || (i as Folder).HasMedia).ToList() : visibleChildren.ToList();
                }
            }
        }

        protected bool IsVisible(BaseItem item)
        {
            return (!Filters.IsFavorite || item.IsFavorite) && (!Filters.IsUnWatched || !item.Watched);
        }

        /// <summary>
        /// Return our children only if they have actually been loaded
        /// </summary>
        public IList<BaseItem> LoadedChildren
        {
            get
            {
                return children.HasValue ? Children : new List<BaseItem>();
            }
        }

        /// <summary>
        /// Returns our first child or null if no children
        /// </summary>
        public virtual BaseItem FirstChild
        {
            get
            {
                return this.ActualChildren.Count > 0 ? this.ActualChildren[0] : null;
            }
        }


        protected BaseItem lastWatchedItem;
        public BaseItem LastWatchedItem
        {
            get
            {
                return Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                                                                          {
                                                                              UserId = Kernel.CurrentUser.ApiId,
                                                                              ParentId = ApiId,
                                                                              Recursive = true,
                                                                              Fields = MB3ApiRepository.StandardFields,
                                                                              SortOrder = Model.Entities.SortOrder.Descending,
                                                                              SortBy = new string[] {"DatePlayed"},
                                                                              Limit = 1
                                                                          }).FirstOrDefault();
            }
        }

        protected List<BaseItem> newestItems;

        public List<BaseItem> NewestItems
        {
            get
            {
                if (newestItems == null)
                {
                    newestItems = this.Children.OrderByDescending(i => i.DateCreated).Take(Kernel.Instance.ConfigData.RecentItemCount).ToList();
                }
                return newestItems;
            }
        }

        protected Guid QuickListID(string option)
        {
            return ("quicklist" + option + this.Name + this.Path +Kernel.CurrentUser.Name).GetMD5();
        }

        protected bool reBuildQuickList = false;
        public virtual Folder QuickList
        {
            get
            {
                if (quickListFolder == null)
                {
                    quickListFolder = UpdateQuickList(Kernel.Instance.ConfigData.RecentItemOption);

                }
                return quickListFolder ?? new IndexFolder();
            }
        }

        protected virtual string RalParentId 
        {
            get
            {
                return ApiId;
            }
        }

        public virtual ItemFilter[] AdditionalRalFilters
        {
            get { return new ItemFilter[] {};}
        }

        public virtual string[] RalExcludeTypes
        {
            get { return new[] {"series", "season", "musicalbum", "musicartist", "folder", "boxset"}; }
        }

        public virtual string[] RalIncludeTypes { get; set; }

        public virtual void ResetQuickList()
        {
            quickListFolder = null; //it will re-load next time requested
            reBuildQuickList = true;
        }

        protected void RemoveQuicklist()
        {
        }

        protected virtual IEnumerable<BaseItem> GetLatestItems(string recentItemOption, int maxItems)
        {
            
            switch (recentItemOption)
            {
                case "watched":
                    return Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                                                                                {
                                                                                    UserId = Kernel.CurrentUser.ApiId,
                                                                                    ParentId = RalParentId,
                                                                                    Limit = maxItems,
                                                                                    Recursive = true,
                                                                                    ExcludeItemTypes = RalExcludeTypes,
                                                                                    IncludeItemTypes = RalIncludeTypes,
                                                                                    ExcludeLocationTypes = new[] { LocationType.Virtual },
                                                                                    Fields = MB3ApiRepository.StandardFields,
                                                                                    Filters = (new[] {Config.Instance.TreatWatchedAsInProgress ? ItemFilter.IsResumable : ItemFilter.IsPlayed, }).Concat(AdditionalRalFilters).ToArray(),
                                                                                    SortBy = new[] {ItemSortBy.DatePlayed},
                                                                                    SortOrder = Model.Entities.SortOrder.Descending
                                                                                }).ToList();


                case "unwatched":
                    return Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                                                                                {
                                                                                    UserId = Kernel.CurrentUser.ApiId,
                                                                                    ParentId = RalParentId,
                                                                                    Limit = maxItems,
                                                                                    Recursive = true,
                                                                                    Fields = MB3ApiRepository.StandardFields,
                                                                                    ExcludeItemTypes = RalExcludeTypes,
                                                                                    IncludeItemTypes = RalIncludeTypes,
                                                                                    ExcludeLocationTypes = new[] { LocationType.Virtual },
                                                                                    Filters = (new[] { ItemFilter.IsUnplayed, }).Concat(AdditionalRalFilters).ToArray(),
                                                                                    SortBy = new[] {ItemSortBy.DateCreated},
                                                                                    SortOrder = Model.Entities.SortOrder.Descending
                                                                                }).ToList();

                default:
                    return Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                                                                                {
                                                                                    UserId = Kernel.CurrentUser.ApiId,
                                                                                    ParentId = RalParentId,
                                                                                    Limit = maxItems,
                                                                                    Recursive = true,
                                                                                    Filters = AdditionalRalFilters,
                                                                                    ExcludeItemTypes = RalExcludeTypes,
                                                                                    IncludeItemTypes = RalIncludeTypes,
                                                                                    ExcludeLocationTypes = new[] { LocationType.Virtual },
                                                                                    Fields = MB3ApiRepository.StandardFields,
                                                                                    SortBy = new[] {ItemSortBy.DateCreated},
                                                                                    SortOrder = Model.Entities.SortOrder.Descending
                                                                                }).ToList();

            }
        }

        public virtual IndexFolder UpdateQuickList(string recentItemOption) 
        {
            //rebuild the proper list
            List<BaseItem> items = null;
            int containerNo = 0;
            int maxItems = this.ActualChildren.Count > 0 ? (this.ActualChildren[0] is IContainer || this.ActualChildren[0] is MusicArtist) && Kernel.Instance.ConfigData.RecentItemCollapseThresh <= 6 ? Kernel.Instance.ConfigData.RecentItemContainerCount : Kernel.Instance.ConfigData.RecentItemCount : Kernel.Instance.ConfigData.RecentItemCount;
            using (new Profiler(string.Format("RAL child retrieval for {0} option {1}", Name, recentItemOption)))
            {
                items = GetLatestItems(recentItemOption, maxItems).ToList();
            }

            Logger.ReportVerbose(recentItemOption + " list for " + this.Name + " loaded with " + items.Count + " items.");
                var folderChildren = new List<BaseItem>();
                //now collapse anything that needs to be and create the child list for the list folder
                var containers = from item in items
                                 where item is IGroupInIndex
                                 group item by (item as IGroupInIndex).MainContainerId;
            foreach (var container in containers)
            {
                var containerObj = ((IGroupInIndex)container.First()).MainContainer;
                //Logger.ReportVerbose("Container " + (containerObj == null ? "--Unknown--" : containerObj.Name) + " items: " + container.Count());
                if (container.Count() < Kernel.Instance.ConfigData.RecentItemCollapseThresh)
                {
                    //add the items without rolling up
                    foreach (var i in container)
                    {
                        //make sure any inherited images get loaded
                        var ignore = i.Parent != null ? i.Parent.BackdropImages : null;
                        ignore = i.BackdropImages;
                        var ignore2 = i.LogoImage;
                        ignore2 = i.ArtImage;

                        folderChildren.Add(i);
                    }
                }
                else
                {
                    var currentContainer = containerObj ?? new IndexFolder() {Name = "<Unknown>"};
                    var currentSeries = currentContainer as Series;
                    containerNo++;
                    var aContainer = new SearchResultFolder(new List<BaseItem>())
                                         {
                                             Id = ("container" + recentItemOption + this.Name + this.Path + containerNo).GetMD5(),
                                             Name = currentContainer.Name + " (" + container.Count() + " Items)",
                                             Overview = currentContainer.Overview,
                                             MpaaRating = currentContainer.MpaaRating,
                                             Genres = currentContainer.Genres,
                                             ImdbRating = currentContainer.ImdbRating,
                                             Studios = currentContainer.Studios,
                                             PrimaryImagePath = currentContainer.PrimaryImagePath,
                                             SecondaryImagePath = currentContainer.SecondaryImagePath,
                                             BannerImagePath = currentContainer.BannerImagePath,
                                             BackdropImagePaths = currentContainer.BackdropImagePaths,
                                             ThemeId = currentSeries != null ? currentSeries.ApiId : null,
                                             TVDBSeriesId = currentSeries != null ? currentSeries.TVDBSeriesId : null,
                                             LogoImagePath = currentSeries != null ? currentSeries.LogoImagePath : null,
                                             ArtImagePath = currentSeries != null ? currentSeries.ArtImagePath : null,
                                             ThumbnailImagePath = currentSeries != null ? currentSeries.ThumbnailImagePath : null,
                                             ThemeSongs = currentSeries != null ? currentSeries.ThemeSongs : null,
                                             ThemeVideos = currentSeries != null ? currentSeries.ThemeVideos : null,
                                             DisplayMediaType = currentContainer.DisplayMediaType,
                                             DateCreated = container.First().DateCreated,
                                             Parent = this
                                         };
                    if (containerObj is Series)
                    {

                        //always roll into seasons
                        var seasons = from episode in container
                                      group episode by (episode as Episode).SeasonId;
                        foreach (var season in seasons)
                        {
                            var currentSeason = ((Episode) season.First()).Season;
                            containerNo++;
                            var aSeason = new SearchResultFolder(season.ToList())
                                              {
                                                  Id = ("season" + recentItemOption + this.Name + this.Path + containerNo).GetMD5(),
                                                  Name = currentSeason.Name + " (" + season.Count() + " Items)",
                                                  Overview = currentSeason.Overview,
                                                  MpaaRating = currentSeason.MpaaRating,
                                                  Genres = currentSeason.Genres,
                                                  ImdbRating = currentSeason.ImdbRating,
                                                  Studios = currentSeason.Studios,
                                                  PrimaryImagePath = currentSeason.PrimaryImagePath ?? containerObj.PrimaryImagePath,
                                                  SecondaryImagePath = currentSeason.SecondaryImagePath,
                                                  BannerImagePath = currentSeason.BannerImagePath ?? containerObj.BannerImagePath,
                                                  BackdropImagePaths = currentSeason.BackdropImagePaths ?? containerObj.BackdropImagePaths,
                                                  TVDBSeriesId = currentSeason.TVDBSeriesId,
                                                  LogoImagePath = currentSeason.LogoImagePath,
                                                  ArtImagePath = currentSeason.ArtImagePath,
                                                  ThumbnailImagePath = currentSeason.ThumbnailImagePath,
                                                  DisplayMediaType = currentSeason.DisplayMediaType,
                                                  DateCreated = season.First().DateCreated,
                                                  Parent = currentSeason.Id == aContainer.Id ? this : aContainer
                                              };

                            aContainer.AddChild(aSeason);
                        }
                    }
                    else
                    {
                        //not series so just add all children to container
                        aContainer.AddChildren(container.ToList());
                    }

                    //and container to children
                    folderChildren.Add(aContainer);
                }

            }
            //finally add all the items that don't go in containers
            folderChildren.AddRange(items.Where(i => (!(i is IGroupInIndex))));

            //and create our quicklist folder
            return new IndexFolder(folderChildren) { Id = QuickListID(recentItemOption), Name = "User:" + Kernel.CurrentUser.Name, DateCreated = DateTime.UtcNow, Parent = this };

        }

        public virtual void Sort(IComparer<BaseItem> sortFunction) {
            Sort(sortFunction, true);
        }

        //can't change the signature of virtual function so have to use this property to report changes -ebr
        public bool FolderChildrenChanged = false;

        public virtual void ValidateChildren() {
            // we never want 2 threads validating children at the same time
            lock (validateChildrenLock) {
                FolderChildrenChanged = ValidateChildrenImpl();
                if (FolderChildrenChanged)
                {
                    //recalculate and store the new counts
                    this.mediaCount = null;
                    var ignore = this.MediaCount;
                    ignore = this.RunTime;
                }
            }
        }

        public override string OfficialRating
        {
            get
            {
                return "None"; // default to "None" for folders so they won't block automatically if block unrated is set
            }
            set
            {
                base.OfficialRating = value;
            }
        }

        public int RunTime { get; set; }

        public virtual bool HasMedia
        {
            get { return (ApiRecursiveItemCount ?? 0) > 0 || (mediaCount ?? 0) > 0 || this.RecursiveMedia.Any(); }
        }

        protected int? mediaCount;
        public virtual int MediaCount
        {
            get
            {
                if (ApiRecursiveItemCount == null) Logger.ReportVerbose("************** Api recursive count is null for {0}",Name);
                return ApiRecursiveItemCount ?? mediaCount ?? (mediaCount = this.RecursiveMedia.Distinct(i => i.Id).Count()).Value;
            }
        }

        protected int? itemCount;
        public virtual int ItemCount
        {
            get
            {
                if (ApiItemCount == null) Logger.ReportVerbose("************** Api item count is null for {0}", Name);
                return ApiItemCount ?? itemCount ?? (itemCount = this.Children.Count).Value;
            }
        }

        public override bool Watched {
            get { return UnwatchedCount == 0; }
            set {
                foreach (var item in this.EnumerateChildren()) {
                    var media = item as Media;
                    if (media != null) {
                        media.PlaybackStatus.WasPlayed = value;
                        Kernel.ApiClient.UpdatePlayedStatus(media.ApiId, Kernel.CurrentUser.Id, value);
                    }
                    var folder = item as Folder;
                    if (folder != null) {
                        folder.Watched = value;
                    }
                }

                _unwatchedCount = null;
                OnChildrenChanged(null);
            }
        }

        private int? _unwatchedCount;
        public int UnwatchedCount
        {
            get { return _unwatchedCount ?? (int)(_unwatchedCount = GetUnwatchedCount()); }
            set { _unwatchedCount = value; }
        }

        public void AdjustUnwatched(int adjustment)
        {
            if (ShowUnwatchedCount)
            {
                UnwatchedCount += adjustment;
                if (UnwatchedCountChanged != null)
                {
                    UnwatchedCountChanged(this, new UnwatchedChangedEventArgs {CountAdjustment = adjustment});
                }
            }

            // And cascade up
            if (Parent != null) Parent.AdjustUnwatched(adjustment);
        }

        public void ResetUnwatchedCount()
        {
            _unwatchedCount = null;
        }

        protected int GetUnwatchedCount()
        {
            var count = 0;

            // it may be expensive to bring in the playback status 
            // so don't lock up the object during.
            foreach (var item in this.Children)
            {
                var media = item as Media;
                if (media != null && !media.PlaybackStatus.WasPlayed)
                {
                    count++;
                }
                else
                {
                    var folder = item as Folder;
                    if (folder != null)
                    {
                        count += folder.UnwatchedCount;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Will search all the children recursively
        /// </summary>
        /// <param name="searchFunction"></param>
        /// <returns></returns>
        public LocalCacheFolder Search(Func<BaseItem, bool> searchFunction, string name) {
            var items = new Dictionary<Guid,BaseItem>();

            foreach (var item in RecursiveChildren) {
                if (searchFunction(item) && !item.IsTrailer && (!Config.Instance.ExcludeRemoteContentInSearch || !item.IsRemoteContent)) {
                    var ignore = item.BackdropImages; //force these to load
                    items[item.Id] = item;
                }
            }
            return new LocalCacheFolder(items.Values.ToList());
        }

        class BaseItemIndexComparer : IEqualityComparer<BaseItem> {

            public bool Equals(BaseItem x, BaseItem y) {
                return x.Name.Equals(y.Name);
            }

            public int GetHashCode(BaseItem item) {
                return item.Name.GetHashCode();
            }
        }

        private IEnumerable<BaseItem> MapStringsToBaseItems(IEnumerable<string> strings, Func<string, BaseItem> func) {
            if (strings == null) return null;

            return strings
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .Select(func);
        }

        protected virtual Func<string, BaseItem> GetConstructor(string property) {
            switch (property) {
                case "Actors":
                case "Directors":
                    return Person.GetPerson;

                case "Genres":
                    return Genre.GetGenre;

                case "ProductionYear":
                    return Year.GetYear;

                case "Studios":
                    return Studio.GetStudio;

                default:
                    return GenericItem.GetItem;
            }
        }

        public virtual IEnumerable<BaseItem> IndexBy(string property)
        {

            if (string.IsNullOrEmpty(property)) throw new ArgumentException("Index type should not be none!");

            if (property == LocalizedStrings.Instance.GetString("GenreDispPref"))
            {
                var query = new ItemQuery
                {
                    UserId = Kernel.CurrentUser.ApiId,
                    ParentId = ApiId,
                    Recursive = true,
                    Fields = new[] { ItemFields.SortName },
                    SortBy = new[] { "SortName" }
                };

                var ret = CollectionType == "Music" || ContainsMusic ?
                              Kernel.Instance.MB3ApiRepository.RetrieveMusicGenres(query).Select(g => new ApiGenreFolder(g, ApiId, new[] {"MusicAlbum"}, null, this)).Cast<BaseItem>().ToList() :
                              Kernel.Instance.MB3ApiRepository.RetrieveGenres(query).Select(g => new ApiGenreFolder(g, ApiId, null, null, this)).Cast<BaseItem>().ToList();
                ApiRecursiveItemCount = ret.Count;
                Logger.ReportVerbose("=========== Indexing with new technique...");
                return ret;

            } else if (property == LocalizedStrings.Instance.GetString("ActorDispPref"))
            {
                var personTypes = new[] {PersonType.Actor, PersonType.GuestStar};
                var ret = LocalizedStrings.Instance.GetString("StartingLetters").Select(c => new PersonLetterFolder(c.ToString(), false, ApiId, personTypes, null, null, this)).Cast<BaseItem>().ToList();
                ret.Add(new PersonLetterFolder("#", true, ApiId, personTypes, null, null, this));
                ApiRecursiveItemCount = ret.Count;
                return ret;
            } else if (property == LocalizedStrings.Instance.GetString("DirectorDispPref"))
            {
                var personTypes = new[] {PersonType.Director};
                var ret = LocalizedStrings.Instance.GetString("StartingLetters").Select(c => new PersonLetterFolder(c.ToString(), false, ApiId, personTypes, null, null, this)).Cast<BaseItem>().ToList();
                ret.Add(new PersonLetterFolder("#", true, ApiId, personTypes, null, null, this));
                ApiRecursiveItemCount = ret.Count;
                return ret;
            } else if (property == LocalizedStrings.Instance.GetString("YearDispPref"))
            {
                var query = new ItemsByNameQuery
                                {
                                    UserId = Kernel.CurrentUser.ApiId,
                                    ParentId = ApiId,
                                    Recursive = true,
                                    Fields = new[] { ItemFields.SortName },
                                    SortBy = new[] {"SortName"},

                                };
                var ret = Kernel.Instance.MB3ApiRepository.RetrieveIbnItems("Years", query).Select(p => new ApiYearFolder(p, ApiId, null, new[] {"Audio"}, this)).Cast<BaseItem>().ToList();
                ApiRecursiveItemCount = ret.Count;
                return ret;
            } else if (property == LocalizedStrings.Instance.GetString("StudioDispPref"))
            {
                var query = new ItemsByNameQuery
                                {
                                    UserId = Kernel.CurrentUser.ApiId,
                                    ParentId = ApiId,
                                    Recursive = true,
                                    Fields = new[] { ItemFields.SortName },
                                    SortBy = new[] {"SortName"},

                                };
                var ret = Kernel.Instance.MB3ApiRepository.RetrieveIbnItems("Studios", query).Select(p => new ApiStudioFolder(p, ApiId, null, new[] {"Audio"}, this)).Cast<BaseItem>().ToList();
                ApiRecursiveItemCount = ret.Count;
                return ret;
            }

            return Kernel.Instance.MB3ApiRepository.RetrieveChildren(this.ApiId);
        }

        private static BaseItem UnknownItem(IndexType indexType) {

            const string unknown = "<Unknown>";

            switch (indexType)
            {
                case IndexType.Director:
                case IndexType.Actor:
                    return Person.GetPerson(unknown);
                case IndexType.Studio:
                    return Studio.GetStudio(unknown);
                case IndexType.Year:
                    return Year.GetYear(unknown);
                default:
                    return Genre.GetGenre(unknown);
            }
        }

        public virtual void OnNavigatingInto()
        {
            
        }

        public bool ChildrenLoaded { get { return children != null; } }

        /// <summary>
        /// Recursive enumerator that returns recursive children only if they have already been loaded
        /// </summary>
        public virtual IEnumerable<BaseItem> RecursiveLoadedChildren
        {
            get
            {
                foreach (var item in LoadedChildren)
                {
                    yield return item;
                    var folder = item as Folder;
                    if (folder != null)
                    {
                        foreach (var subitem in folder.RecursiveLoadedChildren)
                        {
                            yield return subitem;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A recursive enumerator that walks through all the sub children
        /// that are not hidden by parental controls.  Use for UI operations.
        ///   Safe for multithreaded use, since it operates on list clones
        /// </summary>
        public virtual IEnumerable<BaseItem> RecursiveChildren {
            get {
                foreach (var item in Children) {
                    yield return item;
                    var folder = item as Folder;
                    if (folder != null) {
                        foreach (var subitem in folder.RecursiveChildren) {
                            yield return subitem;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A recursive enumerator that walks through all the sub children
        /// ignoring parental controls (use only from refresh operations)
        ///   Safe for multithreaded use, since it operates on list clones
        /// </summary>
        public virtual IEnumerable<BaseItem> AllRecursiveChildren
        {
            get
            {
                List<BaseItem> childCopy;
                lock(ActualChildren)
                    childCopy = ActualChildren.ToList();
                foreach (var item in childCopy)
                {
                    yield return item;
                    var folder = item as Folder;
                    if (folder != null)
                    {
                        foreach (var subitem in folder.AllRecursiveChildren)
                        {
                            yield return subitem;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A recursive enumerator that walks through all the sub children
        /// that are folders and not hidden by parental controls.  Use for UI operations.
        ///   Safe for multithreaded use, since it operates on list clones
        /// </summary>
        public virtual IEnumerable<Folder> RecursiveFolders
        {
            get
            {
                foreach (var item in Children)
                {
                    Folder folder = item as Folder;

                    if (folder != null)
                    {
                        yield return folder;
                        foreach (var subitem in folder.RecursiveFolders)
                        {
                            yield return subitem;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A recursive enumerator that walks through all the sub children
        /// that are some type of media and not hidden by parental controls.  Use for UI operations.
        ///   Safe for multithreaded use, since it operates on list clones
        /// </summary>
        public virtual IEnumerable<Media> RecursiveMedia
        {
            get
            {
                return RecursiveChildren.OfType<Media>().Where(m => m.LocationType != LocationType.Virtual);
            }
        }

        /// <summary>
        /// Protected enumeration through children, 
        ///  this has the potential to block out the item, so its not exposed publicly
        /// </summary>
        /// <returns></returns>
        protected IEnumerable<BaseItem> EnumerateChildren() {
            lock (ActualChildren) {
                foreach (var item in ActualChildren) {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Direct access to children 
        /// </summary>
        protected virtual List<BaseItem> ActualChildren {
            get {
                return children.Value;
            }
        }

        protected void OnChildrenChanged(ChildrenChangedEventArgs args) {
            Sort(sortFunction, false);

            if (ChildrenChanged != null)
            {
                ChildrenChanged(this, args);
            }

            OnQuickListChanged(args);
        }

        public void OnQuickListChanged(EventArgs args) {

            if (QuickListChanged != null)
            {
                QuickListChanged(this, args);
            }
        }

        bool ValidateChildrenImpl()
        {
            return false;
        }

        List<BaseItem> GetChildren(bool allowCache) {

            List<BaseItem> items = null;
            if (allowCache) {
                items = GetCachedChildren();
            }

            if (items != null) SetParent(items);
            return items;
        }

        protected virtual List<BaseItem> GetNonCachedChildren() {

            return new List<BaseItem>();
        }

        protected void SaveChildren(IList<BaseItem> items)
        {
            SaveChildren(items, false);
        }

        protected void SaveChildren(IList<BaseItem> items, bool saveIndvidualChidren) {
            //Logger.ReportVerbose("Saving " + items.Count + " children for " + this.Name);
            Kernel.Instance.MB3ApiRepository.SaveChildren(Id, items.Select(i => i.Id));
            if (saveIndvidualChidren)
            {
                foreach (var item in items)
                {
                    Kernel.Instance.MB3ApiRepository.SaveItem(item); 
                }
            }
        }

        public virtual void LoadDisplayPreferences()
        {
            // Load from api
            DisplayPreferences = Kernel.ApiClient.GetDisplayPrefs(DisplayPreferencesId);
            //Re-initialize these to un-filtered
            DisplayPreferences.CustomPrefs.Remove("IsUnWatched");
            DisplayPreferences.CustomPrefs.Remove("IsFavorite");
        }

        public virtual void SaveDisplayPrefs(DisplayPreferences prefs)
        {
            try
            {
                Kernel.Instance.MB3ApiRepository.SaveDisplayPreferences(DisplayPreferencesId, DisplayPreferences);
            }
            catch (Exception e)
            {
                Logger.ReportException("Unable to save display prefs for {0}",e,Name);
            }
        }

        protected void SetParent(IEnumerable<BaseItem> items) {
            foreach (var item in items) {
                item.Parent = this;
            }
        }

        void AddItemToIndex(Dictionary<BaseItem, List<BaseItem>> index, BaseItem item, BaseItem child) {
            List<BaseItem> subItems;
            if (!index.TryGetValue(item, out subItems)) {
                subItems = new List<BaseItem>();
                index[item] = subItems;
            }
            if (child is Episode)
            {
                //we want to group these by series - find or create a series head
                Episode episode = child as Episode;
                Folder currentSeries = episode.Parent is IndexFolder ? episode.Parent : episode.Series; //may already be indexed
                IndexFolder series = (IndexFolder)index[item].Find(i => i.Id == (item.Name+currentSeries.Name).GetMD5());
                if (series == null)
                {
                    series = new IndexFolder() { 
                        Id = (item.Name+currentSeries.Name).GetMD5(),
                        Name = currentSeries.Name,
                        Overview = currentSeries.Overview,
                        PrimaryImagePath = currentSeries.PrimaryImagePath,
                        SecondaryImagePath = currentSeries.SecondaryImagePath,
                        BannerImagePath = currentSeries.BannerImagePath,
                        BackdropImagePaths = currentSeries.BackdropImagePaths
                    };
                    index[item].Add(series);
                }
                series.AddChild(episode);
            }
            else
            {
                if (!(child is Season)) subItems.Add(child); //never want seasons
            }
        }

        protected virtual void Sort(IComparer<BaseItem> function, bool notifyChange)
        {
            if (function == null) return;

            this.sortFunction = function;
            if (ActualChildren == null) return;

            lock (ActualChildren) {
                ActualChildren.Sort(function);
            }
            if (notifyChange && ChildrenChanged != null)
                {
                    ChildrenChanged(this, null);
                }
        }

        protected virtual bool CollapseBoxSets {get { return Config.Instance.CollapseBoxSets; }}

        protected virtual List<BaseItem> GetCachedChildren()
        {
            return Kernel.Instance.MB3ApiRepository.RetrieveChildren(ApiId, CollapseBoxSets).ToList();
        }

        public bool HasVideoChildren {
            get {
                return this.RecursiveChildren.OfType<Video>().Any();
            }
        }

        public ThumbSize ThumbDisplaySize
        {
            get
            {
                if (this.ActualChildren.Count > 0) //if we have no children, nothing to display
                {
                    Guid id = this.Id;
                    if (Config.Instance.EnableSyncViews)
                    {
                        if (this.GetType() != typeof(Folder))
                        {
                            id = this.GetType().FullName.GetMD5();
                        }
                    }

                    ThumbSize s = Kernel.Instance.MB3ApiRepository.RetrieveThumbSize(id) ?? new ThumbSize(Kernel.Instance.ConfigData.DefaultPosterSize.Width, Kernel.Instance.ConfigData.DefaultPosterSize.Height);
                    float f = this.ActualChildren[0].PrimaryImage != null ? this.ActualChildren[0].PrimaryImage.Aspect : 1; //just use the first child as our guide
                    if (f == 0)
                        f = 1;
                    if (s.Width < 10) { s.Width = Config.Instance.DefaultPosterSize.Width; s.Height = Config.Instance.DefaultPosterSize.Height; }
                    float maxAspect = s.Height / s.Width;
                    if (f > maxAspect)
                        s.Width = (int)(s.Height / f);
                    else
                        s.Height = (int)(s.Width * f);
                    return s;
                }
                else return new ThumbSize(Config.Instance.DefaultPosterSize.Width, Config.Instance.DefaultPosterSize.Height);
            }
        }

    }
}
