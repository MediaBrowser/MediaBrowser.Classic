﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using MediaBrowser.Code;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Metadata;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.UserInput;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Model.Dto;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.UI;

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
            var show = BaseItem as IDetailLoad;
            if (show != null)
            {
                Async.Queue(Async.ThreadPoolName.DetailLoad, show.LoadFullDetails, DetailsChanged);
            }

        }

        protected void DetailsChanged()
        {
            _chapters = null;
            _actors = null;
            
            UIFirePropertiesChange("Chapters","HasChapterInfo","Actors");
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
                    UIFirePropertyChange("IsFavorite");
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

        public bool HasSpecialFeatures { get { return baseItem.SpecialFeatureCount > 0; } }

        private List<Item> _specialFeatures; 
        public List<Item> SpecialFeatures
        {
            get
            {
                if (_specialFeatures == null)
                {
                    var show = baseItem as Show;
                    if (HasSpecialFeatures && show != null)
                    {
                        _specialFeatures = new List<Item>();
                        Async.Queue(Async.ThreadPoolName.SpecialFeatureLoad, () =>
                                                                {
                                                                    _specialFeatures = show.SpecialFeatures.Select(s => ItemFactory.Instance.Create(s) as Item).ToList();
                                                                    UIFirePropertiesChange("SpecialFeatures", "HasSpecialFeatures");
                                                                });
                    }
                    else
                    {
                        _specialFeatures = new List<Item>();
                    }
                }

                return _specialFeatures;
            }
        }

        public int MovieCount { get { return baseItem.MovieCount; } }
        public int SeriesCount { get { return baseItem.SeriesCount; } }
        public int EpisodeCount { get { return baseItem.EpisodeCount; } }
        public int GameCount { get { return baseItem.GameCount; } }
        public int TrailerCount { get { return baseItem.TrailerCount; } }
        public int SongCount { get { return baseItem.SongCount; } }
        public int AlbumCount { get { return baseItem.AlbumCount; } }
        public int MusicVideoCount { get { return baseItem.MusicVideoCount; } }

        public int PartCount
        {
            get { return baseItem.PartCount; }
        }

        private List<Item> _additionalParts;
        public List<Item> AdditionalParts
        {
            get { return _additionalParts ?? (_additionalParts = baseItem.PartCount > 1 ? baseItem.AdditionalParts.Select(p => ItemFactory.Instance.Create(p)).ToList() : new List<Item>()); }
        }

        public bool HasAdditionalParts { get { return PartCount > 1; } }

        public List<SeekPositionItem> SeekPoints
        {
            get { return _seekPoints ?? (_seekPoints = CreateSeekPoints()); }
            set { _seekPoints = value; }
        }

        /// <summary>
        /// Create a list of seek points based on percentage of the running time
        /// </summary>
        /// <returns></returns>
        private List<SeekPositionItem> CreateSeekPoints()
        {
            return new List<SeekPositionItem>(GetSeekPoints(this, 200));
        }

        private IEnumerable<SeekPositionItem> GetSeekPoints(Item item, int numPoints)
        {
            var nextChapterIndex = 1;
            var nextChapter = Chapters.Count > nextChapterIndex ? Chapters[nextChapterIndex] : null;

            for (var i = 0; i <= numPoints; i++)
            {
                var pos = RunTimeTicks/numPoints*i;
                var seek = new SeekPositionItem(pos, item);
                if (nextChapter != null && pos >= nextChapter.PositionTicks)
                {
                    seek.PositionTicks = nextChapter.PositionTicks;
                    seek.ChapterIndex = nextChapterIndex;
                    nextChapterIndex++;
                    nextChapter = Chapters.Count > nextChapterIndex ? Chapters[nextChapterIndex] : null;
                }

                yield return seek;
            }
        }

        public long CurrentPlaybackPosition
        {
            get { return _currentPlaybackPosition; }
            set
            {
                if (_currentPlaybackPosition != value)
                {
                    _currentPlaybackPosition = value;
                    UIFirePropertiesChange("CurrentPlaybackPosition","CurrentPositionString","CurrentEndTimeString", "CurrentTimeRemainingString");
                }
            }
        }

        public string CurrentPositionString { get { return Helper.TicksToFriendlyTime(CurrentPlaybackPosition); } }
        public string CurrentEndTimeString { get { return DateTime.Now.AddTicks(RunTimeTicks - CurrentPlaybackPosition).ToString("t"); } }
        public string CurrentTimeRemainingString { get { return "-" + Helper.TicksToFriendlyTime(Math.Abs(CurrentPlaybackPosition - RunTimeTicks)); } }

        public void SetNextChapterSeekIndex()
        {
            var ndx = SeekPoints.FindIndex(SeekPositionIndex + 1, i => i.IsChapterPoint);
            SeekPositionIndex = ndx >= 0 ? ndx : 0;
            UIFirePropertyChange("CurrentDisplayChapter");
        }

        public void SetPrevChapterSeekIndex()
        {
            var count = SeekPositionIndex > 0 ? SeekPositionIndex - 1 : SeekPoints.Count - 1;
            var ndx = SeekPoints.FindLastIndex(count, count, i => i.IsChapterPoint);
            SeekPositionIndex = ndx >= 0 ? ndx : 0;
            UIFirePropertyChange("CurrentDisplayChapter");
        }

        public bool ShowChapterImage
        {
            get { return _showChapterImage; }
            set
            {
                if (_showChapterImage != value)
                {
                    _showChapterImage = value;
                    UIFirePropertiesChange("ShowChapterImage","CurrentDisplayChapter");
                }
            }
        }

        public ChapterItem CurrentDisplayChapter
        {
            get { return SeekPositionIndex >= 0 && SeekPoints[SeekPositionIndex].IsChapterPoint ? Chapters[SeekPoints[SeekPositionIndex].ChapterIndex.Value] : ChapterItem.Create(new Chapter {Name = "Unknown"}, this); }
        }

        public int SeekPositionIndex
        {
            get { return _seekPositionIndex; }
            set
            {
                _seekPositionIndex = value; 
                UIFirePropertyChange("SeekPositionIndex");
                ShowChapterImage = (value >= 0 && value < SeekPoints.Count) && SeekPoints[value].IsChapterPoint;
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

        public bool Is3D
        {
            get
            {
                var video = baseItem as Video;
                return video != null && video.Is3D;
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

        public bool IsChannelItem
        {
            get { return baseItem.IsChannelItem; }
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
            Application.UIDeferredInvokeIfRequired(() => UIFirePropertyChange("CanResume")); //force UI to update
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

        public bool CanDelete { get { return baseItem.CanDelete; } }

        public bool CanResume { get { return BaseItem.CanResume; } }
        public bool CanResumeMain { get { return BaseItem.CanResumeMain; } }

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
            UIFirePropertiesChange("QuickListItems", "RecentItems","RecentWatchedItems","RecentUnwatchedItems");
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
                if (PlayState == null || !HaveWatched) return "";
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
            Application.UIDeferredInvokeIfRequired(() => 
            {
                UIFirePropertyChange("HaveWatched");
                UIFirePropertyChange("UnwatchedCount");
                UIFirePropertyChange("ShowUnwatched");
                UIFirePropertyChange("UnwatchedCountString");
                UIFirePropertyChange("PlayState");
                UIFirePropertyChange("InProgress");
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
            get 
            {                
                return ((Config.Instance.ShowUnwatchedCount) && baseItem.ShowUnwatchedCount && (this.UnwatchedCountString.Length > 0)); 
            }
        }

        public virtual bool ShowWatched
        {
            get 
            {
                return ((Config.Instance.ShowWatchTickInPosterView) && baseItem.ShowUnwatchedCount); 
            }
        }

        public string UnwatchedCountString
        {
            get
            {
                if (!IsFolder)
                    return "";
                var i = UnwatchedCount;
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
            //Logger.ReportVerbose("Start ToggleWatched() initial value: " + HaveWatched.ToString());
            SetWatched(!this.HaveWatched);
            lock (watchLock)
                unwatchedCountCache = -1;
            UIFirePropertyChange("HaveWatched");
            UIFirePropertyChange("UnwatchedCount");
            UIFirePropertyChange("ShowUnwatched");
            UIFirePropertyChange("InProgress");
            UIFirePropertyChange("UnwatchedCountString");
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
                        //adjust parent counts
                        var parent = PhysicalParent;
                        if (parent != null)
                        {
                            parent.Folder.AdjustUnwatched(-1);
                        }
                        //don't add to watched list as we didn't really watch it (and it might just clutter up the list)
                        if (displayMessage) Application.CurrentInstance.Information.AddInformationString(string.Format(Application.CurrentInstance.StringData("SetWatchedProf"), this.Name));
                    }
                    else
                    {
                        PlayState.WasPlayed = false;
                        //adjust parent count
                        if (this.PhysicalParent != null)
                        {
                            PhysicalParent.Folder.AdjustUnwatched(1);
                        }
                        if (displayMessage) Application.CurrentInstance.Information.AddInformationString(string.Format(Application.CurrentInstance.StringData("ClearWatchedProf"), this.Name));
                    }
                    Async.Queue(Async.ThreadPoolName.ToggleWatched, () => Kernel.ApiClient.UpdatePlayedStatus(this.Id.ToString(), Kernel.CurrentUser.Id, PlayState.WasPlayed));
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
            Async.Queue(Async.ThreadPoolName.UITriggeredMetadataLoader, () =>
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
            Application.UIDeferredInvokeIfRequired(() => this.UIFireAllPropertiesChanged());
        }

        public void ClearImages()
        {
            //clear our our images so they will re-load
            primaryImage = null;
            bannerImage = null;
            backdropImage = null;
            Application.UIDeferredInvokeIfRequired(() => this.UIFireAllPropertiesChanged());
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
        private Item _artist;
        private List<SeekPositionItem> _seekPoints;
        private long _currentPlaybackPosition;
        private int _seekPositionIndex;
        private bool _showChapterImage;

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

        public Item Artist
        {
            get
            {
                var album = BaseItem as MusicAlbum;
                if (album == null) return BlankItem;

                return _artist ?? (_artist = album.AlbumArtist != null ? ItemFactory.Instance.Create(Kernel.Instance.MB3ApiRepository.RetrieveArtist(album.AlbumArtist) ?? new BaseItem {Name = "Unknown"}) : BlankItem);
            }
        }
    }
        #endregion


}
