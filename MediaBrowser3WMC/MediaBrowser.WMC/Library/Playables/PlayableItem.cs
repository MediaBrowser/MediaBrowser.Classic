﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Events;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Playables.ExternalPlayer;
using MediaBrowser.Library.Playables.TMT5;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Util;
using MediaBrowser.LibraryManagement;

namespace MediaBrowser.Library.Playables
{
    /// <summary>
    /// Encapsulates play back for Media.
    /// </summary>
    public abstract class PlayableItem
    {
        #region Progress EventHandler
        volatile EventHandler<GenericEventArgs<PlayableItem>> _Progress;
        /// <summary>
        /// Fires whenever the PlaybackController reports playback progress
        /// </summary>
        public event EventHandler<GenericEventArgs<PlayableItem>> Progress
        {
            add
            {
                _Progress += value;
            }
            remove
            {
                _Progress -= value;
            }
        }

        internal void OnProgress(BasePlaybackController controller, PlaybackStateEventArgs args)
        {
            CurrentFileIndex = args.CurrentFileIndex;
            CurrentMediaIndex = args.CurrentMediaIndex;

            PlayState = PlayableItemPlayState.Playing;

            UpdateCurrentMediaPlayState(controller, args);

            if (_Progress != null)
            {
                try
                {
                    _Progress(this, new GenericEventArgs<PlayableItem>() { Item = this });
                }
                catch (Exception ex)
                {
                    Logger.ReportException("PlayableItem Progress event listener had an error: ", ex);
                }
            } 
        }
        #endregion

        #region PlaybackFinished EventHandler
        volatile EventHandler<GenericEventArgs<PlayableItem>> _PlaybackFinished;
        /// <summary>
        /// Fires when the PlaybackController reports playback finished
        /// </summary>
        public event EventHandler<GenericEventArgs<PlayableItem>> PlaybackFinished
        {
            add
            {
                _PlaybackFinished += value;
            }
            remove
            {
                _PlaybackFinished -= value;
            }
        }

        protected void OnItemFinished(Media media, long pos)
        {
                try
                {
                    Logger.ReportVerbose("Reporting stopped to server for {0}",media.Name);
                    var newStatus = Application.CurrentInstance.ReportPlaybackStopped(media.ApiId, pos);

                    // Update our status with what was returned from server if valid
                    if (newStatus != null)
                    {
                        // we have to search the loaded library for all occurrences of this item and set the status there
                        foreach (var item in Kernel.Instance.FindItems(media.Id).OfType<Media>())
                        {
                            Logger.ReportVerbose(string.Format("Setting new status on {0} with parent of {1}", item.Name, item.Parent != null ? item.Parent.Name : "<Unknown>"));
                            media.PlaybackStatus.LastPlayed = PlaybackStartTime;
                            item.PlaybackStatus.PositionTicks = newStatus.PositionTicks;
                            item.PlaybackStatus.WasPlayed = newStatus.WasPlayed;
                        }
                        if (newStatus.WasPlayed && !media.PlaybackStatus.WasPlayed && media.Parent != null)
                        {
                            //update our parent's unwatched count
                            media.Parent.AdjustUnwatched(-1);
                        }
                        media.PlaybackStatus.LastPlayed = PlaybackStartTime;
                        media.PlaybackStatus.PositionTicks = newStatus.PositionTicks;
                        media.PlaybackStatus.WasPlayed = newStatus.WasPlayed;
                    }
                    else
                    {
                        Logger.ReportVerbose("New status was null");
                    }
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error attempting to update status on server", ex);
                }

            if (Config.Instance.RecentItemOption == "watched" || Config.Instance.RecentItemOption == "unwatched")
            {
                // go ahead and force these lists to re-build
                var top = media.TopParent;
                if (top != null) top.OnQuickListChanged(null);
            }

            //MarkWatchedIfNeeded();
        }

        internal void OnPlaybackFinished(BasePlaybackController controller, PlaybackStateEventArgs args)
        {
            if (args.Item == this && HasMediaItems)
            {
                // If there's still a valid position, fire progress one last time
                if (args.Position > 0)
                {
                    OnProgress(controller, args);
                }
                //update any previously played items to fully watched
                for (var i = 0; i < args.Item.CurrentMediaIndex; i++)
                    OnItemFinished(args.Item.MediaItems.ElementAt(i), long.MaxValue);

                //and then the current one with current state
                OnItemFinished(CurrentMedia, args.Position);
            }

            PlaybackStoppedByUser = args.StoppedByUser;


            PlayState = PlayableItemPlayState.Stopped;

            //DisplayUtil.RevertRefreshRate();

            // Fire finished event
            if (_PlaybackFinished != null)
            {
                Async.Queue(Async.ThreadPoolName.PlayableItemPlaybackFinished, () =>
                {
                    _PlaybackFinished(this, new GenericEventArgs<PlayableItem>() { Item = this });
                }); 
            }

            if (RaiseGlobalPlaybackEvents)
            {
                Application.CurrentInstance.RunPostPlayProcesses(this);
            }

            if (UnmountISOAfterPlayback)
            {
                Application.CurrentInstance.UnmountIso();
            }
        }
        #endregion

        private Guid _Id = Guid.NewGuid();
        /// <summary>
        /// A new random Guid is generated for every PlayableItem. 
        /// Since there could be multiple PlayableItems queued up, having some sort of Id 
        /// is the most accurate way to know which one is playing at a given time.
        /// </summary>
        public Guid Id { get { return _Id; } }

        public virtual bool SupportsStreamedContent { get { return true; } }

        private IEnumerable<Media> _MediaItems = new List<Media>();
        /// <summary>
        /// If playback is based on Media items, this will hold the list of them
        /// </summary>
        public IEnumerable<Media> MediaItems { get { return _MediaItems; } internal set { _MediaItems = value; } }

        /// <summary>
        /// If Playback is Folder Based this will hold a reference to the Folder object
        /// </summary>
        public Folder Folder { get; internal set; }

        private IEnumerable<string> _Files = new List<string>();
        /// <summary>
        /// If the playback is based purely on file paths, this will hold the list of them
        /// </summary>
        public IEnumerable<string> Files { get { return _Files; } internal set { _Files = value; } }

        /// <summary>
        /// Internal  use only. The PlaybackController will use this property to store the list playable files, after formatting them for entry to the player.
        /// </summary>
        internal IEnumerable<string> FilesFormattedForPlayer { get; set; }

        /// <summary>
        /// Determines if the item should be queued, as opposed to played immediately
        /// </summary>
        public bool QueueItem { get; set; }

        /// <summary>
        /// If true, the PlayableItems will be shuffled before playback
        /// </summary>
        public bool Shuffle { get; set; }

        /// <summary>
        /// If true, Playback will be resumed from the last known position
        /// </summary>
        public bool Resume { get; set; }

        private long? _StartPositionTicks = null;
        /// <summary>
        /// Gets or sets the position in ticks from which playback should start.
        /// Unless explicitly set this will be driven off of Resume and Playstate settings
        /// </summary>
        public long StartPositionTicks
        {
            get
            {
                if (_StartPositionTicks.HasValue)
                {
                    return _StartPositionTicks.Value;
                }

                if (Resume)
                {
                    return MediaItems.First().PlaybackStatus.PositionTicks;
                }

                return 0;
            }
            set
            {
                _StartPositionTicks = value;
            }
        }

        private int? _StartPlaylistPosition = null;
        /// <summary>
        /// Gets or sets the playlist position from which playback should start.
        /// Unless explicitly set this will be driven off of Resume and Playstate settings
        /// </summary>
        public int StartPlaylistPosition
        {
            get
            {
                if (_StartPlaylistPosition.HasValue)
                {
                    return _StartPlaylistPosition.Value;
                }

                if (Resume)
                {
                    return MediaItems.First().PlaybackStatus.PlaylistPosition;
                }

                return 0;
            }
            set
            {
                _StartPlaylistPosition = value;
            }
        }

        /// <summary>
        /// Holds the time that playback was started
        /// </summary>
        public DateTime PlaybackStartTime { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating if a mounted ISO should be unmounted after playback
        /// </summary>
        public bool UnmountISOAfterPlayback { get; set; }

        /// <summary>
        /// If true, playback will not be sent to a PlaybackController and will instead just allow autoplay to take over
        /// This should only set true after mounting an iso
        /// </summary>
        public bool UseAutoPlay { get; set; }

        /// <summary>
        /// If we're not able to track playstate at all, we'll at least mark watched once playback stops
        /// </summary>
        private bool HasUpdatedPlayState { get; set; }

        /// <summary>
        /// Determines playstate should be saved for this item
        /// </summary>
        public bool EnablePlayStateSaving { get; set; }

        /// <summary>
        /// Determines if global pre/post play events should fire
        /// </summary>
        public bool RaiseGlobalPlaybackEvents { get; set; }

        /// <summary>
        /// Determines whether or not the PlaybackController should show the now playing view during playback
        /// Note that this depends on PlaybackController implementation and support
        /// </summary>
        public bool ShowNowPlayingView { get; set; }

        /// <summary>
        /// Determines whether or not the PlaybackController should go full screen upon beginning playback
        /// </summary>
        public bool GoFullScreen { get; set; }

        /// <summary>
        /// Determines whether or not the PlaybackController should allow this to play without going full screen and not in the custom player
        /// </summary>
        public bool PlayInBackground { get; set; }

        /// <summary>
        /// Determines whether or not the PlaybackController should use our custom player UI upon beginning playback
        /// </summary>
        public bool UseCustomPlayer { get; set; }

        private BasePlaybackController _PlaybackController = null;
        /// <summary>
        /// Gets the PlaybackController for this Playable
        /// </summary>
        public BasePlaybackController PlaybackController
        {
            get
            {
                if (_PlaybackController == null)
                {
                    _PlaybackController = GetPlaybackController();

                    // If it's still null, create it
                    if (_PlaybackController == null)
                    {
                        _PlaybackController = Activator.CreateInstance(PlaybackControllerType) as BasePlaybackController;

                        Logger.ReportVerbose("Creating a new instance of " + PlaybackControllerType.Name);
                        Kernel.Instance.PlaybackControllers.Add(_PlaybackController);
                    }
                }

                return _PlaybackController;
            }
        }

        /// <summary>
        /// Determines if all playback across MB should be stopped before playing
        /// </summary>
        protected virtual bool StopAllPlaybackBeforePlaying
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets or sets the current playback stage
        /// </summary>
        public PlayableItemPlayState PlayState { get; internal set; }

        /// <summary>
        /// Gets the Media Items that have actually been played up to this point
        /// </summary>
        public IEnumerable<Media> PlayedMediaItems
        {
            get
            {
                return MediaItems.Where(p => p.PlaybackStatus.LastPlayed.Equals(PlaybackStartTime));
            }
        }

        /// <summary>
        /// Once playback is complete this value will indicate if the player was allowed to finish or if it was explicitly stopped by the user
        /// </summary>
        public bool PlaybackStoppedByUser { get; private set; }

        /// <summary>
        /// Helper to determine if this Playable has MediaItems or if it is based on file paths
        /// </summary>
        public bool HasMediaItems { get { return MediaItems.Any(); } }

        /// <summary>
        /// Gets the index of the current media item being played.
        /// </summary>
        public int CurrentMediaIndex { get; private set; }

        /// <summary>
        /// Gets the current Media being played
        /// </summary>
        public Media CurrentMedia
        {
            get
            {
                return MediaItems.ElementAtOrDefault(CurrentMediaIndex);
            }
        }

        /// <summary>
        /// Gets or sets the overall playlist position of the current playing file.
        /// That is, with respect to all files from all Media items
        /// </summary>
        public int CurrentFileIndex { get; private set; }

        /// <summary>
        /// Gets the current file being played
        /// </summary>
        public string CurrentFile
        {
            get
            {
                return Files.ElementAtOrDefault(CurrentFileIndex);
            }
        }

        /// <summary>
        /// Gets the name of this item that can be used for display or logging purposes
        /// </summary>
        public string DisplayName
        {
            get
            {
                // If playback is folder-based, use the name of the folder
                if (Folder != null)
                {
                    return Folder.Name;
                }

                // Otherwise if we're playing Media items, use the name of the current one
                if (HasMediaItems)
                {
                    return CurrentMedia.Name;
                }

                // Playback is file-based so use the current file
                return Files.Any() ? CurrentFile : string.Empty;
            }
        }

        /// <summary>
        /// Gets the primary BaseItem object that was playback was initiated on
        /// If playback is folder-based, this will return the Folder
        /// Otherwise it will return the first Media object (or null if playback is path-based).
        /// </summary>
        private BaseItem PrimaryBaseItem
        {
            get
            {
                // If playback is folder-based, return the Folder
                if (Folder != null)
                {
                    return Folder;
                }

                // Return the first item
                return MediaItems.FirstOrDefault();
            }
        }

        /// <summary>
        /// Determines whether or not this item is restricted by parental controls
        /// </summary>
        public bool ParentalAllowed
        {
            get
            {
                BaseItem item = PrimaryBaseItem;

                return item == null || item.ParentalAllowed;
            }
        }

        /// <summary>
        /// Gets the parental control pin that would need to be entered in order to play the item
        /// </summary>
        public string ParentalControlPin
        {
            get
            {
                BaseItem item = PrimaryBaseItem;

                return item == null ? string.Empty : item.CustomPIN;
            }
        }

        /// <summary>
        /// Determines whether or not the PlayableItem has any video files
        /// </summary>
        public bool HasVideo
        {
            get
            {
                if (HasMediaItems)
                {
                    return MediaItems.Any(IsVideo);
                }
                else
                {
                    // File-based playback - use new api if there are any videos found
                    return Files.Any(IsVideo);
                }
            }
        }

        public PlayableItem()
        {
            EnablePlayStateSaving = true;
            RaiseGlobalPlaybackEvents = true;
            ShowNowPlayingView = true;
        }

        #region AddMedia
        public void AddMedia(string file)
        {
            AddMedia(new string[] { file });
        }

        public void AddMedia(IEnumerable<string> filesToAdd)
        {
            List<string> newList = Files.ToList();
            newList.AddRange(filesToAdd);

            Files = newList;
        }

        public void AddMedia(Media media)
        {
            AddMedia(new Media[] { media });
        }
        public void AddMedia(IEnumerable<Media> itemsToAdd)
        {
            List<Media> newList = MediaItems.ToList();
            newList.AddRange(itemsToAdd);

            MediaItems = newList;
        }
        #endregion

        #region CanPlay
        /// <summary>
        /// Subclasses will have to override this if they want to be able to play a list of files
        /// </summary>
        public virtual bool CanPlay(IEnumerable<string> files)
        {
            if (files.Count() == 1)
            {
                return CanPlay(files.First());
            }

            return false;
        }

        /// <summary>
        /// Subclasses will have to override this if they want to be able to play a list of Media objects
        /// </summary>
        public virtual bool CanPlay(IEnumerable<Media> mediaList)
        {
            if (mediaList.Count() == 1)
            {
                return CanPlay(mediaList.First());
            }

            return false;
        }

        /// <summary>
        /// Subclasses will have to override this if they want to be able to play a Media object
        /// </summary>
        public virtual bool CanPlay(Media media)
        {
            return false;
        }

        /// <summary>
        /// Subclasses will have to override this if they want to be able to play based on a path
        /// </summary>
        public virtual bool CanPlay(string path)
        {
            return false;
        }
        #endregion

        /// <summary>
        /// Determines if this PlayableItem can play a given Media object within a playlist
        /// </summary>
        protected virtual bool IsPlaylistCapable(Media media)
        {
            Video video = media as Video;
            if (video != null)
            {
                return !video.ContainsRippedMedia;
            }
            return true;
        }

        internal void Play()
        {
            if (Microsoft.MediaCenter.UI.Application.ApplicationThread != System.Threading.Thread.CurrentThread)
            {
                Application.UIDeferredInvokeIfRequired(()=> Play());
                return;
            }
            System.Diagnostics.Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == Thread.CurrentThread);
            Prepare();

            if (!MediaItems.Any() && !Files.Any())
            {
                Microsoft.MediaCenter.MediaCenterEnvironment ev = Application.MediaCenterEnvironment;
                ev.Dialog(Application.CurrentInstance.StringData("NoContentDial"), Application.CurrentInstance.StringData("Playstr"), Microsoft.MediaCenter.DialogButtons.Ok, 500, true);
                return;
            }
            if (CurrentMedia != null)
                Logger.ReportInfo(string.Format("{0} about to play {1}({2}) ", PlaybackController.ControllerName, DisplayName, CurrentMedia.Files.FirstOrDefault()));

            // If the controller already has active playable items, stop it and wait for it to flush out
            if (!QueueItem)
            {
                PlaybackController.StopAndWait();
            }

            // Run all pre-play processes
            RunPrePlayProcesses();

            if (!QueueItem && StopAllPlaybackBeforePlaying)
            {
                StopAllApplicationPlayback();
            }

            if (UseAutoPlay)
            {
                Logger.ReportVerbose("Playing with autoplay. Marking watched since we have no way of getting status on this.");

                MarkWatchedIfNeeded();
            }
            else
            {
                //AdjustRefreshRate();
                PlaybackController.Play(this);
            }
        }

        protected virtual void StopAllApplicationPlayback()
        {
            Application.CurrentInstance.StopAllPlayback(true);
        }

        /// <summary>
        /// Performs any necessary housekeeping before playback
        /// </summary>
        protected virtual void Prepare()
        {
            if (QueueItem && !PlaybackController.IsPlaying)
            {
                QueueItem = false;

                // This is for music
                GoFullScreen = false;
                UseCustomPlayer = false;
            }

            // Filter for IsPlaylistCapable
            if (MediaItems.Count() > 1)
            {
                // First filter out items that can't be queued in a playlist
                _MediaItems = GetPlaylistCapableItems(MediaItems);

            }

            if (Shuffle)
            {
                ShufflePlayableItems();
            }

            PlaybackStartTime = DateTime.Now;
        }

        public void AdjustRefreshRate()
        {
            // Set refresh rate if requested
            if (true)
            {
                var first = _MediaItems.FirstOrDefault();
                if (first != null && !string.IsNullOrEmpty(first.MediaInfo.VideoFPS))
                {
                    try
                    {
                        var rate = Convert.ToInt32(first.MediaInfo.VideoFPS.Substring(0, first.MediaInfo.VideoFPS.IndexOf('.')));
                        if (rate >= 23 && rate <= 25) rate = 24;
                        else if (rate >= 29 && rate <= 31) rate = 60;
                        else rate = 0;

                        if (rate > 0 && !DisplayUtil.ChangeRefreshRate(rate))
                        {
                            Async.Queue(Async.ThreadPoolName.RefreshRateError, () => Application.CurrentInstance.MessageBox("Could not change refresh rate to " + rate));
                        }
                        //test
                        Logger.ReportInfo("***************** refresh rate reported as: {0}", DisplayUtil.GetCurrentRefreshRate());

                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error trying to adjust refresh rate.", e);
                    }
                }
            }
            
        }

        /// <summary>
        /// Filters a list of media by returning the ones that are playlist-capable
        /// </summary>
        private IEnumerable<Media> GetPlaylistCapableItems(IEnumerable<Media> mediaItems)
        {
            foreach (Media media in mediaItems)
            {
                if (IsPlaylistCapable(media))
                {
                    yield return media;
                }
            }
        }

        /// <summary>
        /// Runs preplay processes and aborts playback if one of them returns false
        /// </summary>
        private void RunPrePlayProcesses()
        {
            PlayState = Playables.PlayableItemPlayState.Preplay;

            if (RaiseGlobalPlaybackEvents)
            {
                Application.CurrentInstance.RunPrePlayProcesses(this);
            }
        }

        /// <summary>
        /// Gets the Type of PlaybackController that this Playable uses
        /// </summary>
        protected abstract Type PlaybackControllerType
        {
            get;
        }

        /// <summary>
        /// Gets the PlaybackController for this PlayableItem
        /// </summary>
        protected virtual BasePlaybackController GetPlaybackController()
        {
            return Kernel.Instance.PlaybackControllers.FirstOrDefault(p => p.GetType() == PlaybackControllerType);
        }

        /// <summary>
        /// Shuffles the list of playable items
        /// </summary>
        private void ShufflePlayableItems()
        {
            Random rnd = new Random();

            // If playback is based on Media objects
            if (HasMediaItems)
            {
                MediaItems = MediaItems.OrderBy(i => rnd.Next()).ToList();
            }
            else
            {
                // Otherwise if playback is based on a list of files
                Files = Files.OrderBy(i => rnd.Next()).ToList();
            }
        }

        private void UpdateCurrentMediaPlayState(BasePlaybackController controller, PlaybackStateEventArgs args)
        {
            if (CurrentMedia != null)
            {
                CurrentMedia.PlaybackStatus.PositionTicks = args.Position;
                Application.CurrentInstance.UpdatePlayState(CurrentMedia, CurrentMedia.PlaybackStatus, controller.IsPaused, EnablePlayStateSaving);
            }
        }

        /// <summary>
        /// Goes through each Media object within PlayableMediaItems and updates Playstate for each individually
        /// </summary>
        private void UpdatePlayStates(BasePlaybackController controller, PlaybackStateEventArgs args)
        {
            string currentFile = CurrentFile;

            for (int i = 0; i < MediaItems.Count(); i++)
            {
                Media media = MediaItems.ElementAt(i);

                bool isCurrentMedia = i == CurrentMediaIndex;

                long currentPositionTicks = 0;
                int currentPlaylistPosition = 0;

                if (isCurrentMedia)
                {
                    // If this is where playback is, update position and playlist
                    currentPlaylistPosition = controller.GetPlayableFiles(media).ToList().IndexOf(currentFile);
                    media.PlaybackStatus.PositionTicks = currentPositionTicks = args.Position;
                }

                Application.CurrentInstance.UpdatePlayState(media, media.PlaybackStatus, controller.IsPaused, EnablePlayStateSaving);

                if (isCurrentMedia)
                {
                    break;
                }
            }

            HasUpdatedPlayState = true;
        }

        /// <summary>
        /// Marks all Media objects as watched, if progress has not been saved at all yet
        /// </summary>
        private void MarkWatchedIfNeeded()
        {
            if (!HasUpdatedPlayState)
            {
                foreach (Media media in MediaItems)
                {
                    if (EnablePlayStateSaving)
                    {
                        Logger.ReportVerbose("Marking watched: " + media.Name);
                    }

                    Application.CurrentInstance.UpdatePlayState(media, media.PlaybackStatus, false, EnablePlayStateSaving);
                }
            }
        }

        /// <summary>
        /// Stops playback on the current PlaybackController
        /// </summary>
        public void StopPlayback()
        {
            PlaybackController.Stop();
        }

        /// <summary>
        /// Waits for the PlayableItem to reach a given state and then returns
        /// </summary>
        public void WaitForPlayState(PlayableItemPlayState state)
        {
            while (PlayState != state)
            {
                Logger.ReportVerbose("Waiting for {0} to reach {1} state", DisplayName, state.ToString());
                Thread.Sleep(1000);
            }
        }

        public static bool IsVideo(Media media)
        {
            Video video = media as Video;

            if (video != null)
            {
                // See if it has a known video type 
                if (video.MediaType != MediaType.Unknown)
                {
                    return true;
                }

                // Hack alert
                if (video.GetType().Name == "Song")
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public static bool IsVideo(string path)
        {
            MediaType type = MediaTypeResolver.DetermineType(path);

            // Assume video if type is not unknown
            if (type != MediaType.Unknown || Helper.IsVideo(path))
            {
                return true;
            }

            return false;
        }

    }

    /// <summary>
    /// Represents all of the stages of the lifecycle of a PlayableItem
    /// </summary>
    public enum PlayableItemPlayState
    {
        /// <summary>
        /// The PlayableItem has been created, but has not been passed into Application.Play
        /// </summary>
        Created = 0,

        /// <summary>
        /// The PlayableItem is currently running preplay processes and events
        /// </summary>
        Preplay = 1,

        /// <summary>
        /// Tthe PlayableItem has been sent to the player, but is not currently playing.
        /// </summary>
        Queued = 2,

        /// <summary>
        /// The PlayableItem is playing right now
        /// </summary>
        Playing = 3,

        /// <summary>
        /// The PlayableItem has finished playback and is performing post-play actions
        /// </summary>
        Stopped = 4,

        /// <summary>
        /// The PlayableItem has completed all post-play processes and events
        /// </summary>
        PostPlayActionsComplete = 5
    }

}
