using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using MediaBrowser.Library.Query;
using MediaBrowser.Library.Util;
using MediaBrowser.LibraryManagement;
using Microsoft.MediaCenter.UI;
using System.Diagnostics;
using System.Linq;
using MediaBrowser.Library.Entities;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Code;
using System.Threading;
using System.Reflection;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Metadata;
using IContainer = MediaBrowser.Library.Entities.IContainer;
using PropertyChangedEventHandler = Microsoft.MediaCenter.UI.PropertyChangedEventHandler;


namespace MediaBrowser.Library {

    public class FolderModel : Item {

        int jilShift = -1;
        int selectedchildIndex = -1;
        object itemLoadLock = new object();
        DisplayPreferences displayPrefs;
        SizeRef actualThumbSize = new SizeRef(new Size(1, 1));
        FolderChildren folderChildren = new FolderChildren();
        FilterProperties filters; 
        Folder folder;

        #region Folder construction 

        public FolderModel() {
        }

        internal override void Assign(BaseItem baseItem ) { 
            base.Assign(baseItem);
            folder = (Folder)baseItem;
            folderChildren.Assign(this, FireChildrenChangedEvents);
            folder.QuickListChanged += QuickListChanged;
            folder.UnwatchedCountChanged += AdjustUnwatched;
        }

        #endregion

        public Folder Folder {
            get {
                return folder;
            }
        }

        public bool IsIndexed { get { return folderChildren.FolderIsIndexed; } }


        public override void NavigatingInto() {
            // force display prefs to reload.
            displayPrefs = null;

            // metadata should be refreshed in a higher priority
            if (Config.Instance.AutoValidate) folderChildren.RefreshAsap();

            // see if this will help get first unwatched index in time
            var ignore = FirstUnwatchedIndex;

            base.NavigatingInto();
        }

        public int Search(string searchValue)
        {
            return Search(searchValue, false, false, -1, 1);
        }

        public int Search(string searchValue, bool includeSubs, bool unwatchedOnly, int rating, int ratingFactor)
        {
            if (searchValue == null) searchValue = "";
            var searchText = (searchValue != "" ? "Items containing: '" + searchValue + "' " : "All Items ")
                             + (unwatchedOnly ? "Unwatched and " : "and ") + "Rated " + Ratings.ToString(rating)
                             + (ratingFactor > 0 ? " and below..." : " and above...");
            Async.Queue("Search", () =>
            {
                Application.CurrentInstance.ProgressBox(string.Format("Searching {0} for {1} ", Name == "Default" ? "Library" : Name , searchText));
                searchValue = searchValue.ToLower();
                IEnumerable<BaseItem> results = includeSubs ?
                    this.folder.RecursiveChildren.Distinct(i => i.Id).Where(i => MatchesCriteria(i, searchValue, unwatchedOnly, rating, ratingFactor)).ToList() :
                    this.folder.Children.Distinct(i => i.Id).Where(i => MatchesCriteria(i, searchValue, unwatchedOnly, rating, ratingFactor)).ToList();

                Application.CurrentInstance.ShowMessage = false;

                if (results.Any())
                {
                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => Application.CurrentInstance.Navigate(ItemFactory.Instance.Create(new SearchResultFolder(GroupResults(results.ToList()))
                        {
                            Name = this.Name + " - Search Results (" + searchValue + (unwatchedOnly ? "/unwatched" : "")
                                + (rating > 0 ? "/" + Ratings.ToString(rating) + (ratingFactor > 0 ? "-" : "+") : "") + ")"
                        })));
                }
                else
                {
                    Application.CurrentInstance.Information.AddInformationString("No Search Results Found");
                }

            });

            return 0;
        }

        private List<BaseItem> GroupResults(List<BaseItem> items)
        {
            var newChildren = new List<BaseItem>();
            int containerNo = 0;
            //now collapse anything that needs to be and create the child list for the list folder
            var containers = from item in items
                             where item is IGroupInIndex
                             group item by (item as IGroupInIndex).MainContainer;

            foreach (var container in containers)
            {
                Logger.ReportVerbose("Container " + (container.Key == null ? "--Unknown--" : container.Key.Name) + " items: " + container.Count());
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

                        newChildren.Add(i);
                    }
                }
                else
                {
                    var currentContainer = container.Key as IContainer ?? new IndexFolder() { Name = "<Unknown>" };
                    containerNo++;
                    var aContainer = new LocalCacheFolder(new List<BaseItem>())
                    {
                        Id = ("searchcontainer" + this.Name + this.Path + containerNo).GetMD5(),
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
                        TVDBSeriesId = currentContainer is Series ? (currentContainer as Series).TVDBSeriesId : null,
                        LogoImagePath = currentContainer is Media ? (currentContainer as Media).LogoImagePath : null,
                        ArtImagePath = currentContainer is Media ? (currentContainer as Media).ArtImagePath : null,
                        ThumbnailImagePath = currentContainer is Media ? (currentContainer as Media).ThumbnailImagePath : null,
                        DisplayMediaType = currentContainer.DisplayMediaType,
                        DateCreated = container.First().DateCreated,
                        Parent = this.Folder
                    };
                    if (container.Key is Series)
                    {

                        //always roll into seasons
                        var seasons = from episode in container
                                      group episode by episode.Parent;
                        foreach (var season in seasons)
                        {
                            var currentSeason = season.Key as Series ?? new Season() { Name = "<Unknown>" };
                            containerNo++;
                            var aSeason = new LocalCacheFolder(season.ToList())
                            {
                                Id = ("searchseason" + this.Name + this.Path + containerNo).GetMD5(),
                                Name = currentSeason.Name + " (" + season.Count() + " Items)",
                                Overview = currentSeason.Overview,
                                MpaaRating = currentSeason.MpaaRating,
                                Genres = currentSeason.Genres,
                                ImdbRating = currentSeason.ImdbRating,
                                Studios = currentSeason.Studios,
                                PrimaryImagePath = currentSeason.PrimaryImagePath,
                                SecondaryImagePath = currentSeason.SecondaryImagePath,
                                BannerImagePath = currentSeason.BannerImagePath,
                                BackdropImagePaths = currentSeason.BackdropImagePaths,
                                TVDBSeriesId = currentSeason.TVDBSeriesId,
                                LogoImagePath = currentSeason.LogoImagePath,
                                ArtImagePath = currentSeason.ArtImagePath,
                                ThumbnailImagePath = currentSeason.ThumbnailImagePath,
                                DisplayMediaType = currentSeason.DisplayMediaType,
                                DateCreated = season.First().DateCreated,
                                Parent = currentSeason == aContainer ? this.Folder : aContainer
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
                    newChildren.Add(aContainer);
                }
            }

            //finally add all the items that don't go in containers
            newChildren.AddRange(items.Where(i => (!(i is IGroupInIndex))));

            return newChildren;
        }

        private bool MatchesCriteria(BaseItem item, string value, bool unwatchedOnly, int rating, int ratingFactor)
        {
            return item.Name != null &&
                item.Name.ToLower().Contains(value) &&
                (!unwatchedOnly ||
                (item is Media && (item as Media).PlaybackStatus.PlayCount == 0)) &&
                (rating < 0 || (ratingFactor * Ratings.Level(item.OfficialRating)) <= (ratingFactor * rating));
        }

        public void ResetMediaCount()
        {
            this.mediaCount = null;
            FirePropertiesChanged("MediaCount", "MediaCountStr");
        }

        protected int? mediaCount;
        public virtual int MediaCount
        {
            get
            {
                if (mediaCount == null)
                {
                    //async this so we don't hang up the UI on large items
                    Async.Queue(this.Name + " media count", () =>
                    {
                        mediaCount = Folder.MediaCount;
                        FirePropertiesChanged("MediaCount", "MediaCountStr");
                    });
                }
                return mediaCount == null ? 0 : mediaCount.Value;
            }
        }

        public virtual string MediaCountStr
        {
            get
            {
                return MediaCount.ToString();
            }
        }

        protected int? itemCount;
        public virtual int ItemCount
        {
            get
            {
                if (mediaCount == null)
                {
                    //async this so we don't hang up the UI on large items
                    Async.Queue(this.Name + " item count", () =>
                    {
                        mediaCount = Folder.ItemCount;
                        FirePropertiesChanged("ItemCount", "ItemCountStr");
                    });
                }
                return mediaCount == null ? 0 : mediaCount.Value;
            }
        }

        public virtual string ItemCountStr
        {
            get
            {
                return ItemCount.ToString();
            }
        }

        public override int UnwatchedCount {
            get {
                if (unwatchedCountCache == -1) {
                    unwatchedCountCache = 0;
                    Async.Queue("Unwatched Counter", () =>
                    { 
                        unwatchedCountCache = folder.UnwatchedCount;
                        FireWatchedChangedEvents();
                    });
                }
                return unwatchedCountCache;
            }
        }

        public void AdjustUnwatched(object sender, UnwatchedChangedEventArgs args)
        {
            unwatchedCountCache = -1;
            FireWatchedChangedEvents();
        }

        public void ResetWatchedCount()
        {
            unwatchedCountCache = -1;
            folder.ResetUnwatchedCount();
            FireWatchedChangedEvents();
        }

        public int FirstUnwatchedIndex {
            get {
                if (Config.Instance.DefaultToFirstUnwatched) {
                    lock (this.Children)
                        for (int i = 0; i < this.Children.Count; ++i)
                            if (!this.Children[i].HaveWatched)
                                return i;

                }
                return 0;
            }
        }

        public override DateTime LastPlayed
        {
            get
            {
                return LastWatchedItem != null ? LastWatchedItem.LastPlayed : DateTime.MinValue;
            }
        }

        public string CollectionType
        {
            get { return folder.CollectionType; }
        }

        public override bool ShowNewestItems {
            get {
                return string.IsNullOrEmpty(BaseItem.Overview);
            }
        }

        /// <summary>
        /// WARNING - this call may block the thread as the folder is searched for a lastwatched item
        /// </summary>
        public bool HasLastWatchedItem
        {
            get { return LastWatchedItem != null; }
        }

        protected Item lastWatched;
        public Item LastWatchedItem
        {
            get
            {
                if (lastWatched == null)
                {
                    var baseitem = folder.LastWatchedItem;
                    if (baseitem != null)
                    {
                        lastWatched = ItemFactory.Instance.Create(folder.LastWatchedItem);
                        if (lastWatched.BaseItem is Episode) CreateEpisodeParents(lastWatched);
                    }
                }

                return lastWatched;
            }
        }

        protected void QuickListChanged(object sender, EventArgs args)
        {
            QuickListItems = null;
        }

        protected string lastQuickListType = Config.Instance.RecentItemOption;
        protected bool validated = false;
        protected object quickListLock = new object();
        protected List<Item> quickListItems;

        public override List<Item> QuickListItems {
            get {
                if (folder != null)
                {
                    string recentItemOption = Application.CurrentInstance.RecentItemOption;  //save this as it could change during this process
                    if (recentItemOption != lastQuickListType)
                    {
                        folder.ResetQuickList();
                        quickListItems = null;
                    }
                    if (quickListItems == null)
                        Async.Queue("Newest Item Loader", () =>
                        {
                            lock (quickListLock)
                            {
                                //Logger.ReportVerbose(this.Name + " Quicklist has " + folder.QuickList.Children.Count + " items");
                                quickListItems = recentItemOption == "watched" ? 
                                    folder.QuickList.Children.Select(c => ItemFactory.Instance.Create(c)).OrderByDescending(i => i.LastPlayed).ToList() :
                                    folder.QuickList.Children.Select(c => ItemFactory.Instance.Create(c)).OrderByDescending(i => i.BaseItem.DateCreated).ToList();
                                //Logger.ReportVerbose(this.Name + " Quicklist created with " + quickListItems.Count + " items");
                                foreach (var item in quickListItems)
                                {
                                    if (item.BaseItem is Episode)
                                    {
                                        //orphaned episodes need to point back to their actual season/series for some themes
                                        CreateEpisodeParents(item);
                                    }
                                    else
                                    {
                                        item.PhysicalParent = this; //otherwise, just point to us
                                    }
                                }

                                FireQuicklistPropertiesChanged();
                            }
                        }, null, true);

                    lastQuickListType = Application.CurrentInstance.RecentItemOption;
                }
                return quickListItems ?? new List<Item>();
            }
            set
            {
                folder.ResetQuickList();
                quickListItems = null;
                FireQuicklistPropertiesChanged();
            }
        }

        protected void FireQuicklistPropertiesChanged()
        {
            Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>
            {
                FirePropertyChanged("RecentItems");
                FirePropertyChanged("NewestItems");
                FirePropertyChanged("QuickListItems");
            });
            
        }

        protected void CreateEpisodeParents(Item item)
        {
            if (!TVHelper.CreateEpisodeParents(item, this))
            {
                //something went wrong - attach to us
                item.PhysicalParent = this;
            }

        }

        public List<Item> RecentItems
        {
            get
            {
                if (folder != null)
                {
                    return QuickListItems;
                } else {
                    return new List<Item>(); //return empty list if folder is protected
                }

            }
        }

        protected List<Item> newestItems;
        public List<Item> NewestItems {
            get {
                if (newestItems == null)
                {
                    if (folder != null)
                    {
                        newestItems = folder.NewestItems.Select(i => ItemFactory.Instance.Create(i)).ToList();
                        foreach (var item in newestItems) item.PhysicalParent = this;
                    }
                }
                return newestItems ?? new List<Item>();
            }
        }

        public List<Item> UnwatchedItems
        {
            get
            {
                if (folder != null)
                {
                    return QuickListItems;
                }
                else
                {
                    return new List<Item>(); //return empty list if folder is protected
                }

            }
        }

        string folderOverviewCache = null;
        public override string Overview {
            get {
                var overview = base.Overview;
                if (String.IsNullOrEmpty(overview)) {

                    if (folderOverviewCache != null) {
                        return folderOverviewCache;
                    }

                    folderOverviewCache = "";

                    Async.Queue("Overview Loader", () =>
                    {
                        RefreshFolderOverviewCache();
                        Microsoft.MediaCenter.UI.Application.DeferredInvoke( _ => {
                            FirePropertyChanged("Overview");
                        });
                    },null, true);
                  
                }
                return overview;
            }
        }

        private void RefreshFolderOverviewCache() {
            //produce list sorted by episode number if we are a TV season
            if (this.BaseItem is Season)
            {
                int unknown = 9999; //use this for episodes that don't have episode number
                var items = new SortedList<int, BaseItem>();
                foreach (BaseItem i in this.Folder.Children) {
                    Episode ep = i as Episode;
                    if (ep != null)
                    {
                        int epNum;
                        try
                        {
                            epNum = Convert.ToInt32(ep.EpisodeNumber);
                        }
                        catch { epNum = unknown++; }
                        try
                        {
                            items.Add(epNum, ep);
                        } catch {
                            //probably more than one episode coming up as "0"
                            items.Add(unknown++, ep);
                        }
                    }
                }
                folderOverviewCache = string.Join("\n", items.Select(i => (i.Value.Name)).ToArray());
            }
            else // normal folder
            {
                folderOverviewCache = string.Join("\n", folder.Children.OrderByDescending(i => i.DateCreated).Select(i => i.LongName).Take(Config.Instance.RecentItemCount).ToArray());
            }
        }

        public override void RefreshMetadata() {
            this.RefreshMetadata(true);
        }


        public override void RefreshMetadata(bool displayMsg)
        {
            //first do us
            base.RefreshMetadata(false);
            if (displayMsg) Application.CurrentInstance.Information.AddInformationString(Application.CurrentInstance.StringData("RefreshFolderProf") + " " + this.Name);
            Async.Queue("UI Forced Folder Metadata Loader", () =>
            {
                using (new MediaBrowser.Util.Profiler("Refresh " + this.Name))
                {
                    this.folder.RetrieveChildren(); // re-fetch from server
                    this.RefreshFolderOverviewCache();
                    RefreshUI();
                }
            });

        }

        public void RefreshChildren()
        {
            Async.Queue("Child Refresh", () =>
            {
                this.folder.RetrieveChildren();
                this.folderChildren.RefreshChildren();
                this.folderChildren.Sort();
                this.RefreshUI();
            });
        }

        public void RefreshUI()
        {
            Logger.ReportVerbose("Forced Refresh of UI on "+this.Name+" called from: "+new StackTrace().GetFrame(1).GetMethod().Name);


            //this could take a bit so kick this off in the background
            Async.Queue("Refresh UI", () =>
            {

                if (this.IsRoot)
                {
                    //if this is the root page - also the recent items
                    try
                    {
                        foreach (FolderModel fld in this.Children)
                        {
                            fld.QuickListItems = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.ReportException("Invalid root folder type", ex);
                    }
                    //this.SelectedChildChanged(); //make sure recent list changes
                }
                this.FireChildrenChangedEvents();
            }, null, true);
        }

        protected virtual void FireChildrenChangedEvents() {
            if (!Microsoft.MediaCenter.UI.Application.IsApplicationThread) {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke( _ => FireChildrenChangedEvents());
                return;
            }

            //   the only way to get the binder to update the underlying children is to 
            //   change the refrence to the property bound, otherwise the binder thinks 
            //   its all fine a dandy and will not update the children 
            folderChildren.StopListeningForChanges();
            folderChildren = folderChildren.Clone();
            folderChildren.ListenForChanges();

            ResetRunTime();
            ResetMediaCount();
            RefreshFolderOverviewCache();
            FirePropertiesChanged("Children", "SelectedChildIndex", "Overview");
            
            lock (watchLock)
                unwatchedCountCache = -1;
            FireWatchedChangedEvents();
            if (this.displayPrefs != null)
                UpdateActualThumbSize();

            JilOptions = null;

        }

        private void FireWatchedChangedEvents() {
            if (!Microsoft.MediaCenter.UI.Application.IsApplicationThread) {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke( _ => FireWatchedChangedEvents());
                return;
            }

            FirePropertyChanged("HaveWatched");
            FirePropertyChanged("UnwatchedCount");
            FirePropertyChanged("ShowUnwatched");
            FirePropertyChanged("UnwatchedCountString");
        }

        void ChildMetadataPropertyChanged(IPropertyObject sender, string property) {
            if (this.displayPrefs != null) {
                switch (this.displayPrefs.SortOrder) {
                    case "Year":
                        if (property != "ProductionYear")
                            return;
                        break;
                    case "Name":
                        if (property != "Name")
                            return;
                        break;
                    case "Rating":
                        if (property != "ImdbRating")
                            return;
                        break;
                    case "Runtime":
                        if (property != "RunningTime")
                            return;
                        break;

                    case "Date":
                        // date sorting is not affected by metadata
                        return;
                    case "Unwatched":
                        if (property != "Name")
                            return;
                        break;

                }
            }
            this.FirePropertyChanged("Children");
        }

        void ChildPropertyChanged(IPropertyObject sender, string property) {
            if (property == "UnwatchedCount") {
                lock (watchLock)
                    unwatchedCountCache = -1;
                FirePropertyChanged("HaveWatched");
                FirePropertyChanged("UnwatchedCount");
                FirePropertyChanged("ShowUnwatched");
                FirePropertyChanged("UnwatchedCountString");
                // note: need to be careful this doesn't trigger the load of the prefs 
                // that can then trigger a cascade that loads metadata, prefs should only be loaded by 
                // functions that are required when the item is the current item displayed
                if ((this.displayPrefs != null) && (this.DisplayPrefs.SortOrder == "Unwatched")) {
                    FirePropertyChanged("Children");
                }
            } else if (property == "ThumbAspectRatio")
                UpdateActualThumbSize();
        }

        public FolderChildren Children {
            get{
                return folderChildren;
            }
        }

        public List<Item> CondensedChildren
        {
            get
            {
                return Application.CurrentInstance.CondensedFolderLimit == 0 || folderChildren.Count <= Application.CurrentInstance.CondensedFolderLimit ? Children.Select(c => c).ToList() : folderChildren.Take(Application.CurrentInstance.CondensedFolderLimit).Concat(new[] { this }).ToList();
            }
        }

        public bool HasCondensedChildren { get { return folderChildren.Count > Application.CurrentInstance.CondensedFolderLimit; } }

        public int SelectedChildIndex {
            get {
                if (selectedchildIndex > Children.Count)
                    selectedchildIndex = -1;
                return selectedchildIndex;
            }
            set {

                if (selectedchildIndex != value) {
                    selectedchildIndex = value;
                    SelectedChildChanged();
                }
            }
        }

        public Item NextChild
        {
            get
            {
                if (selectedchildIndex < 0) {
                    return Application.CurrentInstance.CurrentItem; //we have no selected child
                }
                //we don't use the public property because we want to roll around the list instead of going to unselected
                selectedchildIndex++;
                if (selectedchildIndex >= Children.Count)
                {
                    selectedchildIndex = 0;
                }
                SelectedChildChanged();
                SelectedChild.NavigatingInto();
                return SelectedChild;
            }
        }

        public Item PrevChild
        {
            get
            {
                if (selectedchildIndex < 0) {
                    return Application.CurrentInstance.CurrentItem; //we have no selected child
                }
                //we don't use the public property because we want to roll around the list instead of going to unselected
                selectedchildIndex--;
                if (selectedchildIndex < 0)
                {
                    selectedchildIndex = Children.Count - 1;
                }
                SelectedChildChanged();
                SelectedChild.NavigatingInto();
                return SelectedChild;
            }
        }

        public Item FirstChild
        {
            get
            {
                if (Children.Count > 0)
                {
                    SelectedChildIndex = 0;
                    return SelectedChild;
                }
                else
                {
                    return Item.BlankItem;
                }
            }
        }

        public Item LastChild
        {
            get
            {
                if (Children.Count > 0)
                {
                    SelectedChildIndex = Children.Count - 1;
                    return SelectedChild;
                }
                else
                {
                    return Item.BlankItem;
                }
            }
        }
                    
        private void SelectedChildChanged() {
            FirePropertyChanged("SelectedChildIndex");
            FirePropertyChanged("SelectedChild");
            FirePropertyChanged("SelectedCondensedChild");
            Application.CurrentInstance.OnCurrentItemChanged();
        }

        public override void SetWatched(bool value) {
            Async.Queue("Folder SetWatched", () =>
                                                 {
                                                     if (Application.CurrentInstance.YesNoBox(string.Format("Mark ALL content as {0}? Are you very sure...?", value ? "Played" : "UnPlayed")) == "Y")
                                                     {
                                                         folder.Watched = value;
                                                         unwatchedCountCache = -1;
                                                         FireWatchedChangedEvents();
                                                     }
                                                 });
        }

        public Item SelectedChild {
            get {
                if ((SelectedChildIndex < 0) || (selectedchildIndex >= Children.Count))
                    return Item.BlankItem;
                return Children[SelectedChildIndex];
            }
        }

        public Item SelectedCondensedChild {
            get {
                if ((SelectedChildIndex < 0) || (selectedchildIndex >= CondensedChildren.Count))
                    return Item.BlankItem;
                return CondensedChildren[SelectedChildIndex];
            }
        }

        protected void IndexByChoice_ChosenChanged(object sender, EventArgs e)
        {
            if (displayPrefs == null) return;
            Async.Queue("Index By", () =>
            {
                Application.CurrentInstance.ProgressBox(string.Format("Building index on {0}. Please wait...", displayPrefs.IndexBy));
                folderChildren.IndexBy(displayPrefs.IndexBy);
                Application.CurrentInstance.ShowMessage = false;
                selectedchildIndex = -1;
                if (folderChildren.Count > 0)
                    SelectedChildIndex = 0;

            });
        }

        public int CurrentJilButtonIndex { get; set; }
        public void PageDownJilButtons()
        {
            var max = JilOptions.Options.Count;
            var step = max/4;
            var ndx = CurrentJilButtonIndex + step;
            CurrentJilButtonIndex = Math.Min(ndx, max - 1);
            FirePropertyChanged("CurrentJilButtonIndex");
        }

        public void PageUpJilButtons()
        {
            var max = JilOptions.Options.Count;
            var step = max/4;
            var ndx = CurrentJilButtonIndex - step;
            CurrentJilButtonIndex = Math.Max(ndx, 0);
            FirePropertyChanged("CurrentJilButtonIndex");
        }

        private Choice _jilOptions;
        public Choice JilOptions
        {
            get { return _jilOptions ?? (_jilOptions = GetJilOptions()); }
            set { _jilOptions = value; FirePropertyChanged("JilOptions"); }
        }

        protected Choice GetJilOptions()
        {
            if (DisplayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("DateDispPref"))
            {
                // Dates
                return new Choice(this, "Jil Options", new List<string>
                                                           {
                                                               Localization.LocalizedStrings.Instance.GetString("ThisWeek"),
                                                               Localization.LocalizedStrings.Instance.GetString("WeekAgo"),
                                                               Localization.LocalizedStrings.Instance.GetString("MonthAgo"),
                                                               Localization.LocalizedStrings.Instance.GetString("SixMonthsAgo"),
                                                               Localization.LocalizedStrings.Instance.GetString("YearAgo"),
                                                               Localization.LocalizedStrings.Instance.GetString("Earlier"),

                                                           });
            }
            if (DisplayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("YearDispPref"))
            {
                // Years
                return new Choice(this, "Jil Options", new List<string>
                                                           {
                                                               Localization.LocalizedStrings.Instance.GetString("ThisYear"),
                                                               Localization.LocalizedStrings.Instance.GetString("LastYear"),
                                                               Localization.LocalizedStrings.Instance.GetString("FiveYearsAgo"),
                                                               Localization.LocalizedStrings.Instance.GetString("TenYearsAgo"),
                                                               Localization.LocalizedStrings.Instance.GetString("TwentyYearsAgo"),
                                                               Localization.LocalizedStrings.Instance.GetString("Longer"),

                                                           });
            }
            if (DisplayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("RatingDispPref"))
            {
                return new Choice(this, "Jil Options", Children.Select(c => c.OfficialRating).Distinct().ToList());
            }
            if (displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("UnWatchedDispPref") ||
                     displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("RuntimeDispPref"))
            {
                return new Choice(this, "Jil Options", new List<string>());
            }

            // default
            return new Choice(this, "Jil Options", Children.Select(c => c.BaseItem is Season ? c.BaseItem.SortName : Helper.FirstCharOrDefault(c.BaseItem.SortName, true)).Distinct().ToList());
        }

        public int JILShift {
            get {
                return jilShift;
            }
            set {
                jilShift = value;
                FirePropertyChanged("JILShift");
            }
        }

        public string TripleTapSelect {
            set {

                if (!String.IsNullOrEmpty(value) && !value.Contains("*")) 
                {
                    var comparer = new BaseItemComparer(SortOrder.Name, StringComparison.InvariantCultureIgnoreCase);
                    var tempItem =  Activator.CreateInstance(this.folder.ChildType) as BaseItem ?? new BaseItem();
                    if (IsIndexed || this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("NameDispPref") || (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("UnWatchedDispPref")))
                    {
                        tempItem.Name = baseItem.GetType().Name.Equals("Series", StringComparison.OrdinalIgnoreCase) ? value.PadLeft(4,'0') : value;
                    } else
                        if (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("DateDispPref"))
                        {
                            try
                            {
                                comparer = new BaseItemComparer(SortOrder.Date);
                                int year;
                                tempItem.DateCreated = int.TryParse(value, out year) ? new DateTime(year, 1, 1) : TranslateSpecialDate(value);
                            }
                            catch (Exception e)
                            {
                                Logger.ReportException("Error in custom JIL selection", e);
                            }
                        }
                        else
                            if (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("RatingDispPref"))
                            {
                            try
                            {
                                    comparer = new BaseItemComparer(SortOrder.Rating);
                                    tempItem.OfficialRating = value;
                            }
                            catch (Exception e)
                            {
                                Logger.ReportException("Error in custom JIL selection", e);
                            }
                            } else
                                if (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("RuntimeDispPref"))
                                {
                            try
                            {
                                if (tempItem is IShow)
                                {
                                    comparer = new BaseItemComparer(SortOrder.Runtime);
                                    (tempItem as IShow).RunningTime = Convert.ToInt32(value);
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.ReportException("Error in custom JIL selection", e);
                            }
                                } else if (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("YearDispPref"))
                                    {
                                        try
                                        {
                                            comparer = new BaseItemComparer(SortOrder.Year);
                                            int year;
                                            tempItem.PremierDate = int.TryParse(value, out year) ? new DateTime(year, 1, 1) : TranslateSpecialDate(value);
                                        }
                                        catch (Exception e)
                                        {
                                            Logger.ReportException("Error in custom JIL selection", e);
                                        }
                                    }

                                    else
                                    {
                                        try
                                        {
                                            comparer = new BaseItemComparer(this.displayPrefs.SortOrder); //this won't work if these have been localized...no way around it now
                                            tempItem.GetType().GetProperty(this.displayPrefs.SortOrder).SetValue(tempItem, value, null);
                                        }
                                        catch (Exception e)
                                        {
                                            Logger.ReportException("Error in custom JIL selection", e);
                                        }
                                    }
                

                    int i = Children.TakeWhile(child => comparer.Compare(tempItem, child.BaseItem) > 0).Count();

                    JILShift = i - SelectedChildIndex;
                }
                 
            }
        }

        protected DateTime TranslateSpecialDate(string key)
        {
            //translate from our special values
            if (key == Localization.LocalizedStrings.Instance.GetString("ThisWeek"))
            {
                return DateTime.Now;
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("WeekAgo"))
            {
                return DateTime.Now.AddDays(-7);
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("MonthAgo"))
            {
                return DateTime.Now.AddDays(-30);
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("SixMonthsAgo"))
            {
                return DateTime.Now.AddDays(-180);
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("YearAgo"))
            {
                return DateTime.Now.AddYears(-1);
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("Earlier"))
            {
                return DateTime.Now.AddYears(-2);
            }
            if (key == Localization.LocalizedStrings.Instance.GetString("ThisYear"))
            {
                return DateTime.Now;
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("LastYear"))
            {
                return new DateTime(DateTime.Now.Year, 1, 1);
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("FiveYearsAgo"))
            {
                return new DateTime(DateTime.Now.Year - 5, 1, 1);
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("TenYearsAgo"))
            {
                return new DateTime(DateTime.Now.Year - 10, 1, 1);
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("TwentyYearsAgo"))
            {
                return new DateTime(DateTime.Now.Year - 20, 1, 1);
            }
            else if (key == Localization.LocalizedStrings.Instance.GetString("Longer"))
            {
                return new DateTime(DateTime.Now.Year - 30, 1, 1);
            }

            return DateTime.Now.AddYears(-2);

        }

        protected virtual void SortOrders_ChosenChanged(object sender, EventArgs e) {
            folderChildren.Sort(this.displayPrefs.SortFunction);
        }

        public virtual DisplayPreferences DisplayPrefs {
            get {
                if (this.displayPrefs == null)
                    LoadDisplayPreferences();
                return this.displayPrefs;
            }
            protected set {
                if (this.displayPrefs != null)
                    throw new NotSupportedException("Attempt to set displayPrefs twice");
                this.displayPrefs = value;
                if (this.displayPrefs != null) {
                    this.displayPrefs.ThumbConstraint.PropertyChanged += new PropertyChangedEventHandler(ThumbConstraint_PropertyChanged);
                    this.displayPrefs.ShowLabels.PropertyChanged += new PropertyChangedEventHandler(ShowLabels_PropertyChanged);
                    this.displayPrefs.SortOrders.ChosenChanged += new EventHandler(SortOrders_ChosenChanged);
                    this.displayPrefs.IndexByChoice.ChosenChanged += new EventHandler(IndexByChoice_ChosenChanged);
                    this.displayPrefs.ViewType.ChosenChanged += new EventHandler(ViewType_ChosenChanged);
                    this.displayPrefs.UseBanner.ChosenChanged += new EventHandler(UseBanner_ChosenChanged);
                    SortOrders_ChosenChanged(null, null);
                    ShowLabels_PropertyChanged(null, null);
                    if (this.actualThumbSize.Value.Height != 1)
                        ThumbConstraint_PropertyChanged(null, null);

                    if (displayPrefs.IndexBy != "None" && displayPrefs.IndexBy != "") {
                        IndexByChoice_ChosenChanged(this, null);
                    }
                }
                FirePropertyChanged("DisplayPrefs");
            }
        }

        void ViewType_ChosenChanged(object sender, EventArgs e)
        {
            var ignore = ShowNowPlayingInText;
        }

        void UseBanner_ChosenChanged(object sender, EventArgs e) {
            UpdateActualThumbSize();
        }


        protected virtual void LoadDisplayPreferences() {
            Logger.ReportVerbose("Loading display prefs for " + this.Path);

            Folder.LoadDisplayPreferences();

            var dp = new DisplayPreferences(this.Folder.DisplayPreferencesId, this.Folder);

            this.DisplayPrefs = dp;
        }

        protected void LoadDefaultDisplayPreferences(ref Guid id, ref DisplayPreferences dp)
        {
            dp = new DisplayPreferences(this.Folder.DisplayPreferencesId, this.Folder);
            dp.LoadDefaults();
            if ((this.PhysicalParent != null) && (Config.Instance.InheritDefaultView))
            {
                // inherit some of the display properties from our parent the first time we are visited
                DisplayPreferences pt = this.PhysicalParent.DisplayPrefs;
                dp.ViewType.Chosen = pt.ViewType.Chosen;
                dp.ShowLabels.Value = pt.ShowLabels.Value;
                // after some use, carrying the sort order forward doesn;t feel right - for seasons especially it can be confusing
                // dp.SortOrder = pt.SortOrder;
                dp.VerticalScroll.Value = pt.VerticalScroll.Value;
            }
        }


        protected void UpdateActualThumbSize() {

            if (!Microsoft.MediaCenter.UI.Application.IsApplicationThread) {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => UpdateActualThumbSize());
                return;
            }

            if (this.displayPrefs == null) return;

            bool useBanner = this.displayPrefs.UseBanner.Value;

            float f = folderChildren.GetChildAspect(useBanner);

            var s = GetThumbConstraint();
            if (f == 0)
                f = 1;
            float maxAspect = s.Height / s.Width;
            if (f > maxAspect)
                s.Width = (int)(s.Height / f);
            else
                s.Height = (int)(s.Width * f);

            if (this.actualThumbSize.Value != s) {
                this.actualThumbSize.Value = s;
                FirePropertyChanged("ReferenceSize");
                FirePropertyChanged("PosterZoom");
            }
        }

        protected virtual Size GetThumbConstraint()
        {
            return this.DisplayPrefs.ThumbConstraint.Value;
        }

        /// <summary>
        /// Determines the size the grid layout gives to each item, without this it bases it off the first item.
        /// We need this as without it under some circustance when labels are showing and the first item is in 
        /// focus things get upset and all the other posters dissappear
        /// It seems to be something todo with what happens when the text box gets scaled
        /// </summary>
        public Size ReferenceSize {
            get {
                Size s = this.ActualThumbSize.Value;
                if (DisplayPrefs.ShowLabels.Value)
                    s.Height += 40;
                return s;
            }
        }

        public SizeRef ActualThumbSize {
            get {

                if (this.actualThumbSize.Value.Height == 1)
                    UpdateActualThumbSize();
                return actualThumbSize;
            }
        }

        public Vector3 PosterZoom {
            get {
                Size s = this.ReferenceSize;
                float x = Math.Max(s.Height, s.Width);
                if (x == 1)
                    return new Vector3(1.15F, 1.15F, 1); // default if we haven't be set yet
                float z = (float)((-0.007 * x) + 2.5);
                if (z < 1.15)
                    z = 1.15F;
                if (z > 1.9F)
                    z = 1.9F; // above this the navigation arrows start going in strange directions!
                return new Vector3(z, z, 1);
            }
        }


        BooleanChoice showNowPlayingInText;
        public BooleanChoice ShowNowPlayingInText
        {
            get {
                if (showNowPlayingInText == null)
                {
                    showNowPlayingInText = new BooleanChoice();
                }
                var enableText = new string[] {"Detail", "ThumbStrip", "CoverFlow"};
                if (Kernel.Instance.ConfigData.ShowNowPlayingInText && enableText.Contains(DisplayPrefs.ViewTypeString))
                {
                    showNowPlayingInText.Value = true;
                }
                else
                {
                    showNowPlayingInText.Value = false;
                }
                return showNowPlayingInText;
            }
        }

        void ShowLabels_PropertyChanged(IPropertyObject sender, string property) {
            FirePropertyChanged("ReferenceSize");
            FirePropertyChanged("PosterZoom");
        }

        void ThumbConstraint_PropertyChanged(IPropertyObject sender, string property) {
            UpdateActualThumbSize();
            FirePropertyChanged("ReferenceSize");
            FirePropertyChanged("PosterZoom");
        }

        public bool FilterUnwatched
        {
            get { return Folder.Filters.IsUnWatched; }
            set
            {
                Folder.SetFilterUnWatched(value);
                Folder.SaveDisplayPrefs(DisplayPrefs);
                if (folderChildren.FolderIsIndexed)
                {
                    IndexByChoice_ChosenChanged(this, null);
                }
                else
                {
                    folderChildren.RefreshChildren();
                }
                FirePropertyChanged("FilterUnwatched");
            }
        }

        public bool FilterFavorite
        {
            get { return Folder.Filters.IsFavorite == true; }
            set
            {
                Folder.SetFilterFavorite(value);
                Folder.SaveDisplayPrefs(DisplayPrefs);
                if (folderChildren.FolderIsIndexed)
                {
                    IndexByChoice_ChosenChanged(this, null);
                }
                else
                {
                    Folder.ReloadChildren();
                }
                FirePropertyChanged("FilterFavorite");
            }
        }

        protected override void Dispose(bool disposing) {

            if (folderChildren != null)
                folderChildren.Dispose();
              

            if (this.displayPrefs != null)
                this.displayPrefs.Dispose();

            base.Dispose(disposing);
        }
        
    }
}