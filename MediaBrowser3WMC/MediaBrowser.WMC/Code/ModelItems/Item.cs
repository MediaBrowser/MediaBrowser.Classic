using System;
using System.Collections.Generic;
using System.Threading;
using MediaBrowser.Code;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Metadata;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.UserInput;
using MediaBrowser.LibraryManagement;
using Microsoft.MediaCenter;

namespace MediaBrowser.Library
{

    public partial class Item : BaseModelItem
    {
        static Item blank;
        static Dictionary<Type, ItemType> itemTypeMap;
        static Item()
        {
            itemTypeMap = new Dictionary<Type, ItemType>();
            itemTypeMap[typeof(Episode)] = ItemType.Episode;
            itemTypeMap[typeof(Movie)] = ItemType.Movie;
            itemTypeMap[typeof(Series)] = ItemType.Series;
            itemTypeMap[typeof(Season)] = ItemType.Season;
            itemTypeMap[typeof(MediaBrowser.Library.Entities.Folder)] = ItemType.Folder;

            blank = new Item();
            BaseItem item = new BaseItem();
            item.Path = "";
            item.Name = "";
            blank.Assign(item);

        }

        object loadMetadatLock = new object();
        protected object watchLock = new object();

        private PlaybackStatus playstate;
        protected BaseItem baseItem;

        protected int unwatchedCountCache = -1;


        #region Item Construction
        public Item()
        {
        }

        internal virtual void Assign(BaseItem baseItem)
        {
            this.baseItem = baseItem;
            baseItem.MetadataChanged += new EventHandler<MetadataChangedEventArgs>(MetadataChanged);
        }

        #endregion

        public BaseItem BaseItem { get { return baseItem; } }

        public FolderModel PhysicalParent { get; internal set; }

        internal FolderModel TopParent
        {
            get
            {
                if (PhysicalParent != null && !PhysicalParent.IsRoot)
                {
                    return PhysicalParent.TopParent;
                }
                else
                {
                    return this as FolderModel;
                }
            }
        }

        public Guid Id { get { return baseItem.Id; } }

        public virtual void NavigatingInto()
        {
        }

        public bool IsVideo
        {
            get
            {
                return (baseItem is Video);
            }
        }

        public bool IsNotVideo
        {
            get
            {
                return (baseItem is Folder) ? !((baseItem as Folder).HasVideoChildren) : !(baseItem is Video);
            }
        }

        public bool IsFavorite
        {
            get { return baseItem.IsFavorite; }
            set
            {
                if (baseItem.IsFavorite != value)
                {
                    baseItem.IsFavorite = value;
                    FirePropertyChanged("IsFavorite");
                }
            }
        }

        // having this in Item and not in Folder helps us avoid lots of messy mcml 
        public virtual bool ShowNewestItems
        {
            get
            {
                return false;
            }
        }

        public string Name
        {
            get
            {
                return BaseItem.Name;
            }
        }

        public string LongName
        {
            get
            {
                return BaseItem.LongName;
            }
        }

        public string Path
        {
            get
            {
                return baseItem.Path;
            }
        }

        public DateTime CreatedDate
        {
            get
            {
                return baseItem.DateCreated;
            }
        }

        public string CreatedDateString
        {
            get
            {
                return baseItem.DateCreated.ToShortDateString();
            }
        }


        public ItemType ItemType
        {
            get
            {
                ItemType type;
                if (!itemTypeMap.TryGetValue(baseItem.GetType(), out type))
                {
                    type = ItemType.None;
                }
                return type;
            }
        }

        public string ItemTypeString
        {
            get
            {
                string[] items = BaseItem.GetType().ToString().Split('.');
                return items[items.Length - 1];
            }
        }

        public string LocationType
        {
            get { return baseItem.LocationType.ToString(); }
        }

        private bool? _isMissing;
        public bool IsMissing
        {
            get
            {
                if (_isMissing == null)
                {
                    DetermineVirtualType();
                }
                return _isMissing ?? false;
            }
        }

        private bool? _isFuture;
        public bool IsFuture
        {
            get
            {
                if (_isFuture == null)
                {
                    DetermineVirtualType();
                }
                return _isFuture ?? false;
            }
        }

        private void DetermineVirtualType()
        {
            if (baseItem.LocationType != Model.Entities.LocationType.Virtual)
            {
                _isMissing = false;
                _isFuture = false;
            }
            else
            {
                _isMissing = baseItem.IsMissing;
                _isFuture = baseItem.IsFuture;
            }
        }

        protected static Dictionary<string, string> MediaImageNames = new Dictionary<string, string>() {
            {"f4v","flv"},
            {"m4v","mov"},
            {"mpg","mpeg"},
            {"ogv","ogg"},
            {"threegp","3gp"}
        };

        protected string MediaImageName
        {
            get
            {
                if (MediaImageNames.ContainsKey(MediaTypeString))
                    return "media_" + MediaImageNames[MediaTypeString];
                else
                    return "media_" + MediaTypeString;
            }
        }

        public string MediaTypeString
        {
            get
            {
                var mediaType = "";
                var video = baseItem as Video;
                if (video != null)
                {
                    mediaType = video.MediaType.ToString().ToLower();
                }
                return mediaType;
            }
        }

        public string VideoFormatString
        {
            get
            {
                    string videoFormat =  "";
                    var video = baseItem as Video;
                    if (video != null)
                    {
                        videoFormat = video.VideoFormat;
                    }
                    return videoFormat;
            }
        }

        public Microsoft.MediaCenter.UI.Image MediaTypeImage
        {
            get
            {
                return Helper.GetMediaInfoImage(MediaImageName);
            }
        }

        public string HDTypeString
        {
            get
            {
                if (HDType != 0)
                {
                return HDType.ToString() + this.MediaInfo.ScanTypeChar;
                }
                else return "";
            }
        }

        public Microsoft.MediaCenter.UI.Image HDTypeImage
        {
            get
            {
                return Helper.GetMediaInfoImage("HDType_" + this.HDTypeString);
            }
        }

        public Microsoft.MediaCenter.UI.Image AspectRatioImage
        {
            get
            {
                string aspectImageName = "";

                switch (AspectRatioString)
                {
                    //handle special cases
                    case "5:4":
                        aspectImageName = "125";
                        break;
                    case "4:3":
                        aspectImageName = "133";
                        break;
                    case "3:2":
                        aspectImageName = "150";
                        break;
                    case "16:9":
                        aspectImageName = "177";
                        break;
                    case "2:1":
                        aspectImageName = "200";
                        break;
                    case "4:1":
                        aspectImageName = "400";
                        break;
                    default:
                        //convert
                        if (AspectRatioString.Length > 3)
                            aspectImageName = AspectRatioString.Substring(0, 4).Replace(".", "");
                        else
                            aspectImageName = AspectRatioString.Replace(":", "-");
                        break;
                }

                return Helper.GetMediaInfoImage("Aspect_" + aspectImageName);
            }
        }

        public bool IsRoot
        {
            get
            {
                return baseItem.Id == Application.CurrentInstance.RootFolder.Id;
            }
        }

        public bool IsRemoteContent
        {
            get { return baseItem.IsRemoteContent; }
        }

        public bool SelectAction()
        {
            if (this.BaseItem != null)
            {
                return BaseItem.SelectAction(this);
            }
            {
                Logger.ReportWarning("BaseItem null in request to navigate to " + this.Name);
                return false;
            }
        }

        #region Playback

        public bool PlayAction()
        {
            if (this.BaseItem != null)
            {
                return BaseItem.PlayAction(this);
            }
            else
            {
                Logger.ReportWarning("BaseItem null in request to play " + this.Name);
                return false;
            }
        }

        public bool SupportsMultiPlay
        {
            get
            {
                return baseItem is Folder;
            }
        }

        public bool ParentalAllowed { get { return baseItem.ParentalAllowed; } }
        public string ParentalRating
        {
            get
            {
                return baseItem.ParentalRating;
            }
        }

        public void Play(bool resume, bool queue)
        {
            if (resume)
            {
                Application.CurrentInstance.Resume(this);
            }
            else if (queue)
            {
                Application.CurrentInstance.AddToQueue(this);
            }
            else
            {
                Application.CurrentInstance.Play(this);
            }
        }

        public void UpdateResume()
        {
            Logger.ReportVerbose("Updating Resume status...");
            Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => FirePropertyChanged("CanResume")); //force UI to update
        }

        private void Play(bool resume)
        {
            Play(resume, false);
        }


        public void Queue()
        {
            Play(false, true);
        }

        public void Play()
        {
            Play(false);
        }
        public void Resume()
        {
            Play(true);
        }

        public bool CanResume
        {
            get
            {
                return BaseItem.CanResume;
            }
        }

        public TimeSpan WatchedTime
        {
            get
            {
                return new TimeSpan(this.PlayState.PositionTicks);
            }
        }

        public string RecentDateString
        {
            get
            {
                switch (Application.CurrentInstance.RecentItemOption)
                {
                    case "watched":
                        string runTimeStr = "";
                        string watchTimeStr = "";
                        string lastPlayedStr = LastPlayedString;
                        if (this.PlayState != null && this.PlayState.PositionTicks > 0)
                        {
                            TimeSpan watchTime = new TimeSpan(this.PlayState.PositionTicks);
                            watchTimeStr = " " + watchTime.TotalMinutes.ToString("F0") + " ";
                            if (!String.IsNullOrEmpty(this.RunningTimeString))
                            {
                                runTimeStr = Kernel.Instance.StringData.GetString("OfEHS") + " " + RunningTimeString;
                            }
                            else
                            {
                                runTimeStr = Kernel.Instance.StringData.GetString("MinutesStr"); //have watched time but not running time so tack on 'mins'
                            }
                        }
                        else if (this is FolderModel)
                        {
                            lastPlayedStr = Kernel.Instance.StringData.GetString("VariousEHS");
                        }
                        return Kernel.Instance.StringData.GetString("WatchedEHS") + watchTimeStr + runTimeStr + " " +
                            Kernel.Instance.StringData.GetString("OnEHS") + " " + lastPlayedStr;
                    default:
                        return Kernel.Instance.StringData.GetString("AddedOnEHS") + " " + CreatedDateString;
                }
            }
        }
        public void RecentItemsChanged()
        {
            FirePropertiesChanged("QuickListItems", "RecentItems","RecentWatchedItems","RecentUnwatchedItems");
        }

        public virtual DateTime LastPlayed
        {
            get
            {
                if (PlayState != null)
                {
                    return PlayState.LastPlayed;
                }
                else
                {
                    return DateTime.MinValue;
                }
            }
        }

        public string LastPlayedString
        {
            get
            {
                if (PlayState == null) return "";
                return PlayState.LastPlayed == DateTime.MinValue ? "" : PlayState.LastPlayed.ToShortDateString();
            }
        }

        // fix theme crash
        public virtual List<Item> QuickListItems
        {
            get
            {
                return new List<Item>();
            }
            set { }
        }


        public PlaybackStatus PlayState
        {
            get
            {
                EnsurePlayStateChangesBoundToUI();
                return playstate ?? new PlaybackStatus();
            }
        }

        public void ResetPlayState()
        {
            //this will force it to re-load
            playstate = null;
        }

        internal void EnsurePlayStateChangesBoundToUI()
        {
            if (playstate == null)
            {

                Media media = baseItem as Media;

                if (media != null)
                {
                    playstate = media.PlaybackStatus;
                    // if we want any chance to reclaim memory we are going to have to use 
                    // weak event handlers
                    playstate.WasPlayedChanged += new EventHandler<EventArgs>(PlaybackStatusPlayedChanged);
                    PlaybackStatusPlayedChanged(this, null);
                }
            }
        }

        void PlaybackStatusPlayedChanged(object sender, EventArgs e)
        {
            lock (watchLock)
                unwatchedCountCache = -1;

            //force UI to update
            Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => 
            {
                FirePropertyChanged("HaveWatched");
                FirePropertyChanged("UnwatchedCount");
                FirePropertyChanged("ShowUnwatched");
                FirePropertyChanged("UnwatchedCountString");
                FirePropertyChanged("PlayState");
                FirePropertyChanged("InProgress");
            }); 
        }

        #endregion

        #region watch tracking

        public bool HaveWatched
        {
            get
            {
                return UnwatchedCount == 0;
            }
        }

        public bool InProgress
        {
            get
            {
                return CanResume;
            }
        }

        public bool ShowUnwatched
        {
            get { return ((Config.Instance.ShowUnwatchedCount) && (this.UnwatchedCountString.Length > 0)); }
        }

        public string UnwatchedCountString
        {
            get
            {
                if (this.IsPlayable)
                    return "";
                int i = this.UnwatchedCount;
                return (i == 0) ? "" : i.ToString();
            }
        }

        public virtual int UnwatchedCount
        {
            get
            {
                int count = 0;

                var media = baseItem as Media;
                if (media != null && media.PlaybackStatus != null && !media.PlaybackStatus.WasPlayed)
                {
                    count = 1;
                }
                return count;
            }
        }

        public void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
        }

        public void ToggleWatched()
        {
            Logger.ReportVerbose("Start ToggleWatched() initial value: " + HaveWatched.ToString());
            SetWatched(!this.HaveWatched);
            lock (watchLock)
                unwatchedCountCache = -1;
            FirePropertyChanged("HaveWatched");
            FirePropertyChanged("UnwatchedCount");
            FirePropertyChanged("ShowUnwatched");
            FirePropertyChanged("InProgress");
            FirePropertyChanged("UnwatchedCountString");
            Logger.ReportVerbose("  ToggleWatched() changed to: " + HaveWatched.ToString());
            //HACK: This sort causes errors in detail lists, further debug necessary
            //this.PhysicalParent.Children.Sort();
        }

        public virtual void SetWatched(bool value)
        {
            SetWatched(value, true);
        }
        
        public virtual void SetWatched(bool value, bool displayMessage)
        {
            if (IsPlayable)
            {
                if (value != HaveWatched)
                {
                    if (value && PlayState.PlayCount == 0)
                    {
                        PlayState.PlayCount = 1;
                        //remove ourselves from the unwatched list as well
                        if (this.PhysicalParent != null)
                        {
                            this.PhysicalParent.RemoveRecentlyUnwatched(this); //thought about asynch'ing this but its a list of 20 items...
                        }
                        //don't add to watched list as we didn't really watch it (and it might just clutter up the list)
                        if (displayMessage) Application.CurrentInstance.Information.AddInformationString(string.Format(Application.CurrentInstance.StringData("SetWatchedProf"), this.Name));
                    }
                    else
                    {
                        PlayState.WasPlayed = false;
                        //remove ourselves from the watched list as well
                        if (this.PhysicalParent != null)
                        {
                            this.PhysicalParent.RemoveNewlyWatched(this); //thought about asynch'ing this but its a list of 20 items...
                        }
                        if (displayMessage) Application.CurrentInstance.Information.AddInformationString(string.Format(Application.CurrentInstance.StringData("ClearWatchedProf"), this.Name));
                    }
                    Kernel.Instance.SavePlayState(BaseItem, PlayState);
                    lock (watchLock)
                        unwatchedCountCache = -1;
                    Async.Queue("Toggle Watched", () => Kernel.ApiClient.UpdatePlayedStatus(this.Id.ToString(), Kernel.CurrentUser.Id, PlayState.WasPlayed));
                }
            }

        }

        #endregion


        #region Metadata loading and refresh

        public virtual void RefreshMetadata()
        {
            RefreshMetadata(true);
        }

        public virtual void RefreshMetadata(bool displayMessage)
        {
            if (displayMessage)
                Application.CurrentInstance.Information.AddInformationString(Application.CurrentInstance.StringData("RefreshProf") + " " + this.Name);
            Async.Queue("UI Triggered Metadata Loader", () =>
                                                            {
                                                                if (!string.IsNullOrEmpty(baseItem.ApiId))
                                                                {
                                                                    // Tell server to refresh us
                                                                    Kernel.ApiClient.RefreshItem(baseItem.ApiId);

                                                                    // wait a few beats for that to happen...
                                                                    Thread.Sleep(1000);
                                                                }

                                                                // and then re-load ourselves from the server
                                                                ReLoadFromServer();
                                                            });
        }

        public void ReLoadFromServer()
        {
            //but never null ourselves out
            this.Assign(baseItem.ReLoad() ?? baseItem);
            // force images to reload
            primaryImage = null;
            bannerImage = null;
            primaryImageSmall = null;
            logoImage = null;
            artImage = null;
            thumbnailImage = null;
            backdropImages = null;
            baseItem.ReCacheAllImages();
            Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => this.FireAllPropertiesChanged());
        }

        public void ClearImages()
        {
            //clear our our images so they will re-load
            primaryImage = null;
            bannerImage = null;
            backdropImage = null;
            Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => this.FireAllPropertiesChanged());
        }
        #endregion


        public bool IsPlayable
        {
            get
            {
                return baseItem.IsPlayable;
            }
        }

        public bool IsFolder
        {
            get
            {
                return baseItem is Folder;
            }
        }

        protected FolderModel season;
        public FolderModel Season
        {
            get
            {

                Episode episode = baseItem as Episode;
                if (episode != null)
                {
                    season = ItemFactory.Instance.Create(episode.Season) as FolderModel;
                }

                return season;
            }
        }

        protected FolderModel series;
        public FolderModel Series
        {
            get
            {
                if (series == null)
                {
                    series = ItemFactory.Instance.Create(this.baseItem.OurSeries) as FolderModel;
                }
                return series;
            }
        }

        public string SeasonNumber
        {
            get
            {
                var episode = baseItem as Episode;
                if (episode != null)
                {
                    if (episode.SeasonNumber != null)
                        return episode.SeasonNumber;
                    else
                        return "";
                }
                else
                    return "";
            }
        }

        public string EpisodeNumber
        {
            get
            {
                var episode = baseItem as Episode;
                if (episode != null)
                {
                    if (episode.EpisodeNumber != null)
                        return episode.EpisodeNumber;
                    else
                        return "";
                }
                else
                    return "";
            }
        }

        // this is a shortcut for MCML
        public void ProcessCommand(RemoteCommand command)
        {
            Application.CurrentInstance.PlaybackController.ProcessCommand(command);
        }

        public bool ContainsTrailers
        {
            get
            {
                ISupportsTrailers entity = baseItem as ISupportsTrailers;

                if (entity != null)
                {
                    return entity.ContainsTrailers;
                }

                return false;
            }
        }

        public void PlayTrailers()
        {
            Application.CurrentInstance.PlayLocalTrailer(this);
        }

        #region Dynamic Data Support
        class FakeLateBoundOldDictionary : System.Collections.IDictionary
        {

            BaseItem item;

            public FakeLateBoundOldDictionary(BaseItem item)
            {
                this.item = item;
            }

            public void Add(object key, object value)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(object key)
            {
                throw new NotImplementedException();
            }

            public System.Collections.IDictionaryEnumerator GetEnumerator()
            {
                throw new NotImplementedException();
            }

            public bool IsFixedSize
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsReadOnly
            {
                get { throw new NotImplementedException(); }
            }

            public System.Collections.ICollection Keys
            {
                get { throw new NotImplementedException(); }
            }

            public void Remove(object key)
            {
                throw new NotImplementedException();
            }

            public System.Collections.ICollection Values
            {
                get { throw new NotImplementedException(); }
            }

            public object this[object key]
            {
                get
                {
                    var prop = item.GetType().GetProperty(key.ToString());
                    return prop != null ? prop.GetValue(item, null) : null;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }


            public void CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsSynchronized
            {
                get { throw new NotImplementedException(); }
            }

            public object SyncRoot
            {
                get { throw new NotImplementedException(); }
            }


            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

        }

        public System.Collections.IDictionary DynamicProperties
        {
            get
            {
                return new FakeLateBoundOldDictionary(this.BaseItem);
            }
        }

    }
        #endregion


}
