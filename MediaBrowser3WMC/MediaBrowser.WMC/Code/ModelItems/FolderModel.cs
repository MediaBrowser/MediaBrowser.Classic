using System;
using System.Collections.Generic;
using System.Collections;
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


namespace MediaBrowser.Library {

    public class FolderModel : Item {

        int jilShift = -1;
        int selectedchildIndex = -1;
        object itemLoadLock = new object();
        DisplayPreferences displayPrefs;
        SizeRef actualThumbSize = new SizeRef(new Size(1, 1));
        FolderChildren folderChildren = new FolderChildren();
        Folder folder;

        #region Folder construction 

        public FolderModel() {
        }

        internal override void Assign(BaseItem baseItem ) { 
            base.Assign(baseItem);
            folder = (MediaBrowser.Library.Entities.Folder)baseItem;
            folderChildren.Assign(this, FireChildrenChangedEvents);
        }

        #endregion

        public Folder Folder {
            get {
                return folder;
            }
        }

        public override void NavigatingInto() {
            // force display prefs to reload.
            displayPrefs = null;

            // metadata should be refreshed in a higher priority
            if (Config.Instance.AutoValidate) folderChildren.RefreshAsap();

            base.NavigatingInto();
        }

        public int Search(string searchValue)
        {
            return Search(searchValue, false, false, -1, 1);
        }

        public int Search(string searchValue, bool includeSubs, bool unwatchedOnly, int rating, int ratingFactor)
        {
            if (searchValue == null) searchValue = "";
            searchValue = searchValue.ToLower();
            IEnumerable<BaseItem> results = includeSubs ?
                this.folder.RecursiveChildren.Where(i => MatchesCriteria(i, searchValue, unwatchedOnly, rating, ratingFactor)).ToList() :
                this.folder.Children.Where(i => MatchesCriteria(i, searchValue, unwatchedOnly, rating, ratingFactor)).ToList();

            if (results.Count() > 0)
            {
                Application.CurrentInstance.Navigate(ItemFactory.Instance.Create(new SearchResultFolder(GroupResults(results)) 
                    { Name = this.Name + " - Search Results (" + searchValue + (unwatchedOnly ? "/unwatched":"") 
                        + (rating > 0 ? "/"+Ratings.ToString(rating) + (ratingFactor > 0 ? "-" : "+") : "") + ")" }));
                return results.Count();
            }
            else
            {
                Application.CurrentInstance.Information.AddInformationString("No Search Results Found");
            }
            return 0;
        }

        private List<BaseItem> GroupResults(IEnumerable<BaseItem> items)
        {
            List<BaseItem> folderChildren = new List<BaseItem>();
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

                        folderChildren.Add(i);
                    }
                }
                else
                {
                    var currentContainer = container.Key as IContainer ?? new IndexFolder() { Name = "<Unknown>" };
                    containerNo++;
                    IndexFolder aContainer = new IndexFolder(new List<BaseItem>())
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
                            IndexFolder aSeason = new IndexFolder(season.ToList())
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
                    folderChildren.Add(aContainer);
                }
            }

            //finally add all the items that don't go in containers
            folderChildren.AddRange(items.Where(i => (!(i is IGroupInIndex))));

            return folderChildren;
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
                if (Children.Count > 0)
                {
                    Media lastPlayedItem = this.folder.RecursiveChildren.Where(i => i is Media).OrderByDescending(i => (i as Media).PlaybackStatus.LastPlayed).First() as Media;
                    if (lastPlayedItem != null)
                    {
                        return lastPlayedItem.PlaybackStatus.LastPlayed;
                    }
                    else
                    {
                        return DateTime.MinValue;
                    }
                }
                return DateTime.MinValue;
            }
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
            get { return folder.LastWatchedItem != null; }
        }

        protected Item lastWatched;
        public Item LastWatchedItem
        {
            get
            {
                if (lastWatched == null)
                {
                    Async.Queue("lastwatched load", () =>
                    {
                        var baseItem = folder.LastWatchedItem;
                        if (baseItem != null)
                        {
                            lastWatched = ItemFactory.Instance.Create(folder.LastWatchedItem);
                            if (lastWatched.BaseItem is Episode) CreateEpisodeParents(lastWatched);
                            FirePropertyChanged("LastWatchedItem");
                        }
                    });
                }
                return lastWatched;
            }
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
                            //the first time kick off a validation of our whole tree so we pick up anything new
                            if (!validated && Config.Instance.AutoValidate)
                            {
                                Async.Queue(this.Name + " Initial Validation", () =>
                                {
                                    validated = true;
                                    var changed = false;
                                    var lastDate = folder.QuickList.DateCreated.ToLocalTime();
                                    using (new MediaBrowser.Util.Profiler(this.Name + " Initial validate"))
                                    {
                                        changed = folder.DateCreated > lastDate || folder.DateModified > lastDate;
                                        changed = folder.RecursiveChildren.Aggregate(changed, (current, subItem) => current | (subItem.DateCreated > lastDate || subItem.DateModified > lastDate));
                                    }
                                    if (changed)
                                    {
                                        Logger.ReportVerbose(this.Name + " has had changes.");
                                        this.mediaCount = this.runtime = null;
                                        QuickListItems = null; //this will force it to re-load
                                    }

                                }, null, true);
                            }
                            lock (quickListLock)
                            {
                                //Logger.ReportVerbose(this.Name + " Quicklist has " + folder.QuickList.Children.Count + " items");
                                quickListItems = recentItemOption == "watched" ? 
                                    folder.QuickList.Children.Select(c => ItemFactory.Instance.Create(c)).OrderByDescending(i => i.LastPlayed).ToList() :
                                    folder.QuickList.Children.Select(c => ItemFactory.Instance.Create(c)).OrderByDescending(i => i.BaseItem.DateCreated).ToList();
                                Logger.ReportVerbose(this.Name + " Quicklist created with " + quickListItems.Count + " items");
                                foreach (var item in quickListItems)
                                {
                                    if (item.BaseItem is Episode)
                                    {
                                        //orphaned episodes need to point back to their actual season/series for some themes
                                        var episode = item.BaseItem as Episode;
                                        if (episode.Parent is Series && !(episode.Parent is IndexFolder))
                                        {
                                            //we loaded in context - just create normally
                                            item.PhysicalParent = ItemFactory.Instance.Create(item.BaseItem.Parent) as FolderModel;
                                        }
                                        else
                                        {
                                            CreateEpisodeParents(item);
                                        }
                                    }
                                    else
                                    {
                                        item.PhysicalParent = this; //otherwise, just point to us
                                    }
                                }
                                if (recentItemOption == "unwatched" && folder.QuickList.RecursiveMedia.Count() != folder.QuickList.UnwatchedCount)
                                {
                                    //something is watched in our unwatched list - force a rebuild
                                    Logger.ReportVerbose(this.Name + " unwatched items changed.");
                                    QuickListItems = null;
                                }
                                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>
                                {
                                    FirePropertyChanged("RecentItems");
                                    FirePropertyChanged("NewestItems");
                                    FirePropertyChanged("QuickListItems");
                                });
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
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>
                {
                    FirePropertyChanged("RecentItems");
                    FirePropertyChanged("NewestItems");
                    FirePropertyChanged("QuickListItems");
                });

            }
        }

        protected void CreateEpisodeParents(Item item)
        {
            //** I don't really like this little bit of 'magic' to derive our season/series but I guess
            //** it is better than storing backwards pointers...maybe.

            var episode = item.BaseItem as Episode;
            if (episode == null) return;
            //this item loaded out of context (no season/series parent) we need to derive and create them
            if (episode.Parent != null && episode.Path != null)
            {
                var mySeason = episode.RetrieveSeason();
                if (mySeason != null)
                {
                    //found season - attach it
                    episode.Parent = mySeason;
                    //and create a model item for it
                    item.PhysicalParent = ItemFactory.Instance.Create(mySeason) as FolderModel;
                }
                //gonna need a series too
                var mySeries = episode.RetrieveSeries();
                if (mySeries != null)
                {
                    if (mySeason != null)
                        mySeason.Parent = mySeries;
                    else
                        episode.Parent = mySeries;

                    if (item.PhysicalParent == null)
                        item.PhysicalParent = ItemFactory.Instance.Create(mySeries) as FolderModel;
                    else
                        item.PhysicalParent.PhysicalParent = ItemFactory.Instance.Create(mySeries) as FolderModel;

                    //now force the blasted images to load so they will inherit
                    var ignoreList = mySeries.BackdropImages;
                    ignoreList = mySeason != null ? mySeason.BackdropImages : null;
                    ignoreList = episode.BackdropImages;
                    var ignore = mySeries.ArtImage;
                    ignore = mySeries.LogoImage;
                    ignore = mySeason != null ? mySeason.ArtImage : null;
                    ignore = mySeason != null ? mySeason.LogoImage : null;
                    ignore = episode.ArtImage;
                    ignore = episode.LogoImage;
                }
                else
                {
                    //something went wrong deriving all this - attach to us
                    item.PhysicalParent = this;
                }
            }

        }

        public List<Item> RecentItems
        {
            get
            {
                //only want items from non-protected folders
                if (folder != null && folder.ParentalAllowed)
                {
                    return QuickListItems;
                    //return GetRecentWatchedItems(Config.Instance.RecentItemCount);
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
                    if (folder != null && folder.ParentalAllowed)
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
                //only want items from non-protected folders
                if (folder != null && folder.ParentalAllowed)
                {
                    return QuickListItems;
                    //return GetRecentUnwatchedItems(Config.Instance.RecentItemCount);
                }
                else
                {
                    return new List<Item>(); //return empty list if folder is protected
                }

            }
        }
        

        public void AddNewlyWatched(Item item)
        {
            //called when we watch something so add to top of list (this way we don't have to re-build whole thing)
            if (item.ParentalAllowed || !Config.Instance.HideParentalDisAllowed)
            {
                folder.LastWatchedItem = item.BaseItem;
                this.lastWatched = null;
                FirePropertyChanged("LastWatchedItem");
                if (Config.Instance.RecentItemOption == "watched" && quickListItems != null) //already have a list
                {
                    //first we need to remove ourselves if we're already in the list (can't search with item cuz we were cloned)
                    Item us = quickListItems.Find(i => i.Id == item.Id);
                    if (us != null)
                    {
                        quickListItems.Remove(us);
                    }
                    //then add at the top and tell the UI to update
                    quickListItems.Insert(0, item);
                    FirePropertyChanged("RecentItems");
                    FirePropertyChanged("QuickListItems");
                }
            }
        }

        public void RemoveNewlyWatched(Item item)
        {
            //called when we clear the watched status manually (this way we don't have to re-build whole thing)
            if (HasLastWatchedItem && folder.LastWatchedItem.Id == item.Id)
            {
                folder.LastWatchedItem = null;
                FirePropertyChanged("LastWatchedItem");
            }
            if (Config.Instance.RecentItemOption == "watched" && (quickListItems != null)) // have a list
            {
                Item us = quickListItems.Find(i => i.Id == item.Id);
                if (us != null)
                {
                    quickListItems.Remove(us);
                    FirePropertyChanged("RecentItems");
                    FirePropertyChanged("QuickListItems");
                }
            }
            else if (Config.Instance.RecentItemOption == "unwatched" && quickListItems != null) // have a list
            {
                Item us = quickListItems.Find(i => i.Id == item.Id);
                if (us != null)
                {
                    quickListItems.Remove(us);
                    FirePropertyChanged("UnwatchedItems");
                    FirePropertyChanged("QuickListItems");
                }
            }
        }

        public void RemoveRecentlyUnwatched(Item item)
        {
            //called when watched status set manually (this way we don't have to re-build whole thing)
            if (Config.Instance.RecentItemOption == "unwatched" && quickListItems != null) // have a list
            {
                Item us = quickListItems.Find(i => i.Id == item.Id);
                if (us != null)
                {
                    quickListItems.Remove(us);
                    FirePropertyChanged("UnwatchedItems");
                    FirePropertyChanged("QuickListItems");
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
                    this.folder.ReCacheAllImages();
                    //and now all our children
                    foreach (BaseItem item in this.folder.RecursiveChildren)
                    {
                        Logger.ReportInfo("re-caching images for " + item.Name);
                        item.ReCacheAllImages();
                    }
                }
            });

        }

        public void RefreshChildren()
        {
            Async.Queue("Child Refresh", () =>
            {
                this.folder.ValidateChildren();
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
                        foreach (FolderModel folder in this.Children)
                        {
                            folder.QuickListItems = null;
                            //folder.newestItems = null; //force it to go get the real items
                            //folder.GetNewestItems(Config.Instance.RecentItemCount);
                            //folder.recentUnwatchedItems = null;
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

        protected void FireChildrenChangedEvents() {
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
            FirePropertiesChanged("Children", "SelectedChildIndex");
            
            lock (watchLock)
                unwatchedCountCache = -1;
            FireWatchedChangedEvents();
            if (this.displayPrefs != null)
                UpdateActualThumbSize();
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
                Application.CurrentInstance.OnNavigationInto(SelectedChild);
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
                Application.CurrentInstance.OnNavigationInto(SelectedChild);
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
            Application.CurrentInstance.OnCurrentItemChanged();
        }

        public override void SetWatched(bool value) {
            folder.Watched = value;
        }

        public Item SelectedChild {
            get {
                if ((SelectedChildIndex < 0) || (selectedchildIndex >= Children.Count))
                    return Item.BlankItem;
                return Children[SelectedChildIndex];
            }
        }

        protected void IndexByChoice_ChosenChanged(object sender, EventArgs e) {

            folderChildren.IndexBy(displayPrefs.IndexBy);
            selectedchildIndex = -1;
            if (folderChildren.Count > 0)
                SelectedChildIndex = 0;
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

                if (!String.IsNullOrEmpty(value) && (MediaBrowser.LibraryManagement.Helper.IsAlphaNumeric(value))) {
                    BaseItemComparer comparer = new BaseItemComparer(SortOrder.Name, StringComparison.InvariantCultureIgnoreCase);
                    BaseItem tempItem = Activator.CreateInstance(this.folder.ChildType) as BaseItem;
                    if (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("NameDispPref") || (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("UnWatchedDispPref")))
                    {
                        tempItem.Name = this.baseItem is Series && !(this.baseItem is Season) ? "Season "+value : value;
                    } else
                        if (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("DateDispPref"))
                        {
                            //no good way to do this
                            return;
                        } else
                            if (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("RatingDispPref"))
                            {
                            try
                            {
                                if (tempItem is IShow)
                                {
                                    comparer = new BaseItemComparer(SortOrder.Rating);
                                    (tempItem as IShow).ImdbRating = Convert.ToSingle(value);
                                }
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
                                } else
                                    if (this.displayPrefs.SortOrder == Localization.LocalizedStrings.Instance.GetString("YearDispPref"))
                                    {
                                        try
                                        {
                                            if (tempItem is IShow)
                                            {
                                                comparer = new BaseItemComparer(SortOrder.Year);
                                                (tempItem as IShow).ProductionYear = Convert.ToInt32(value);
                                            }
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
                

                    int i = 0; 
                    foreach (var child in Children) {
                        if (comparer.Compare(tempItem, child.BaseItem) <= 0) break;
                        i++; 
                    }

                    JILShift = i - SelectedChildIndex;
                }
                 
            }
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

            this.Folder.LoadDisplayPreferences();

            var dp = new DisplayPreferences((Folder.DisplayMediaType + Kernel.CurrentUser.Name).GetMD5(), this.Folder);

            this.DisplayPrefs = dp;
        }

        protected void LoadDefaultDisplayPreferences(ref Guid id, ref DisplayPreferences dp)
        {
            dp = new DisplayPreferences(id, this.Folder);
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

            Size s = this.DisplayPrefs.ThumbConstraint.Value;
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

        protected override void Dispose(bool disposing) {

            if (folderChildren != null)
                folderChildren.Dispose();
              

            if (this.displayPrefs != null)
                this.displayPrefs.Dispose();

            base.Dispose(disposing);
        }
        
    }
}