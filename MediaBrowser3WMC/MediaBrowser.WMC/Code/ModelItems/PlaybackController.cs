using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Events;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Playables;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Util;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Model.Dto;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.Hosting;
using Microsoft.MediaCenter.UI;
using MediaType = Microsoft.MediaCenter.MediaType;

namespace MediaBrowser
{
    /// <summary>
    /// Plays content using the internal WMC video player.
    /// Don't inherit from this unless you're playing using the internal WMC player
    /// </summary>
    public class PlaybackController : BasePlaybackController
    {
        // After calling MediaCenterEnvironment.PlayMedia, playback will begin with a state of Stopped and position 0
        // We'll record it when we see it so we don't get tripped up into thinking playback has actually stopped
        private bool _HasStartedPlaying = false;
        private DateTime _LastTransportUpdateTime = DateTime.Now;
        private Microsoft.MediaCenter.PlayState _CurrentPlayState;
        protected PlayableItem Playable { get; set; }

        public override string ControllerName
        {
            get { return "Internal Player"; }
        }

        public override void ToggleZoomMode()
        {
            var current = ZoomMode;
            if (current < 3)
            {
                ZoomMode++;
            }
            else
            {
                ZoomMode = 0;
            }
        }

        public override Vector3 Zoom
        {
            get
            {
                switch (ZoomMode)
                {
                    case 0:
                        return new Vector3(1,1,1);
                    case 1:
                        return new Vector3(1.33f,1,1);
                    case 2:
                        return new Vector3(1,1.33f,1);
                    case 3:
                        return new Vector3(1.33f,1.33f,1);
                }

                return new Vector3(1,1,1);
            }
        }

        protected override void ResetPlaybackProperties()
        {
            base.ResetPlaybackProperties();

            CurrentMediaCollection = null;
            _HasStartedPlaying = false;
            _CurrentPlayState = Microsoft.MediaCenter.PlayState.Undefined;
            _LastTransportUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// Plays Media
        /// </summary>
        protected override void PlayMediaInternal(PlayableItem playable)
        {
            if (playable.QueueItem)
            {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => QueuePlayableItem(playable));
            }
            else
            {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => PlayPlayableItem(playable));
            }
        }

        /// <summary>
        /// Plays or queues Media
        /// </summary>
        protected virtual void PlayPlayableItem(PlayableItem playable)
        {
            // Prevent sleep/screen saver
            Helper.PreventSleep();

            this.Playable = playable;
            _HasStartedPlaying = false;

            // Get this now since we'll be using it frequently
            MediaCenterEnvironment mediaCenterEnvironment = AddInHost.Current.MediaCenterEnvironment;

            try
            {
                // Attach event handler to MediaCenterEnvironment
                // We need this because if you press stop on a dvd menu without ever playing, Transport.PropertyChanged will never fire
                mediaCenterEnvironment.PropertyChanged -= MediaCenterEnvironment_PropertyChanged;
                mediaCenterEnvironment.PropertyChanged += MediaCenterEnvironment_PropertyChanged;

                if (!CallPlayMediaForPlayableItem(mediaCenterEnvironment, playable))
                {
                    mediaCenterEnvironment.PropertyChanged -= MediaCenterEnvironment_PropertyChanged;

                    OnErrorPlayingItem(playable, "PlayMedia returned false");
                    return;
                }

                MediaExperience exp = mediaCenterEnvironment.MediaExperience ?? PlaybackControllerHelper.GetMediaExperienceUsingReflection();

                if (exp != null)
                {
                    MediaTransport transport = exp.Transport;

                    if (transport != null)
                    {
                        transport.PropertyChanged -= MediaTransport_PropertyChanged;
                        transport.PropertyChanged += MediaTransport_PropertyChanged;

                        // If using the legacy api we have to resume manually
                        if (CurrentMediaCollection == null)
                        {
                            long startPosition = playable.StartPositionTicks;

                            if (startPosition > 0)
                            {
                                TimeSpan startPos = TimeSpan.FromTicks(startPosition);

                                Logger.ReportVerbose("Seeking to " + startPos.ToString());

                                transport.Position = startPos;
                            }
                        }
                    }
                    else
                    {
                        Logger.ReportWarning("PlayPlayableItem: MediaTransport is null");
                    }

                    if (playable.GoFullScreen || (!playable.HasMediaItems && !playable.PlayInBackground) 
                        || ( Config.Instance.DisableCustomPlayerForDvd && playable.CurrentMedia.MediaType == Library.MediaType.DVD))
                    {
                        Logger.ReportVerbose("Going fullscreen");
                        exp.GoToFullScreen();
                    }
                    else if (playable.UseCustomPlayer)
                    {
                        Logger.ReportVerbose("Using custom player interface");
                        Application.CurrentInstance.OpenCustomPlayerUi();
                    }

                }
                else
                {
                    Logger.ReportWarning("PlayPlayableItem: MediaExperience is null");
                }
            }
            catch (Exception ex)
            {
                OnErrorPlayingItem(playable, ex);
            }
        }

        /// <summary>
        /// Calls PlayMedia using either a MediaCollection or a single file
        /// </summary>
        private bool CallPlayMediaForPlayableItem(MediaCenterEnvironment mediaCenterEnvironment, PlayableItem playable)
        {
            if (PlaybackControllerHelper.UseLegacyApi(playable))
            {
                bool success = CallPlayMediaLegacy(mediaCenterEnvironment, playable);
                CurrentMediaCollection = null;
                return success;
            }
            else
            {
                return CallPlayMediaUsingMediaCollection(mediaCenterEnvironment, playable);
            }
        }

        private bool CallPlayMediaUsingMediaCollection(MediaCenterEnvironment mediaCenterEnvironment, PlayableItem playable)
        {
            var coll = new MediaCollection();

            // Create a MediaCollectionItem for each file to play
            if (playable.HasMediaItems)
            {
                PlaybackControllerHelper.PopulateMediaCollectionUsingMediaItems(this, coll, playable);
            }
            else
            {
                PlaybackControllerHelper.PopulateMediaCollectionUsingFiles(coll, playable);
            }

            // Set starting position if we're resuming
            if (playable.Resume)
            {
                var playstate = playable.MediaItems.First().PlaybackStatus;

                coll.CurrentIndex = playstate.PlaylistPosition;
                coll[playstate.PlaylistPosition].Start = new TimeSpan(playstate.PositionTicks);
            }

            CurrentMediaCollection = coll;

            bool success = PlaybackControllerHelper.CallPlayMedia(mediaCenterEnvironment, MediaType.MediaCollection, CurrentMediaCollection, false);

            if (!success)
            {
                CurrentMediaCollection = null;
            }

            return success;
        }

        /// <summary>
        /// Calls PlayMedia
        /// </summary>
        private bool CallPlayMediaLegacy(MediaCenterEnvironment mediaCenterEnvironment, PlayableItem playable)
        {
            Microsoft.MediaCenter.MediaType type = PlaybackControllerHelper.GetMediaType(playable);

            bool playedWithPlaylist = false;

            // Need to create a playlist
            if (PlaybackControllerHelper.RequiresWPL(playable))
            {
                IEnumerable<string> files = playable.FilesFormattedForPlayer;

                string playlistFile = PlaybackControllerHelper.CreateWPLPlaylist(playable.Id.ToString(), files, playable.StartPlaylistPosition);

                if (!PlaybackControllerHelper.CallPlayMedia(mediaCenterEnvironment, type, playlistFile, false))
                {
                    return false;
                }

                playedWithPlaylist = true;
            }

            // If we're playing a dvd and the last item played was a MediaCollection, we need to make sure the MediaCollection has
            // fully cleared out of the player or there will be quirks such as ff/rew remote buttons not working
            if (playable.HasMediaItems)
            {
                Video video = playable.MediaItems.First() as Video;

                Microsoft.MediaCenter.Extensibility.MediaType lastMediaType = PlaybackControllerHelper.GetCurrentMediaType();

                if (video != null && video.MediaType == Library.MediaType.DVD && (lastMediaType == Microsoft.MediaCenter.Extensibility.MediaType.MediaCollection || lastMediaType == Microsoft.MediaCenter.Extensibility.MediaType.Unknown))
                {
                    System.Threading.Thread.Sleep(500);
                }
            }

            if (!playedWithPlaylist)
            {
                bool queue = false;

                foreach (string fileToPlay in playable.FilesFormattedForPlayer)
                {
                    if (!PlaybackControllerHelper.CallPlayMedia(mediaCenterEnvironment, type, fileToPlay, queue))
                    {
                        return false;
                    }

                    queue = true;
                }
            }

            return true;
        }

        protected virtual void QueuePlayableItem(PlayableItem playable)
        {
            if (CurrentMediaCollection == null)
            {
                QueuePlayableItemLegacy(playable);
            }
            else
            {
                QueuePlayableItemIntoMediaCollection(playable);
            }
        }

        private void QueuePlayableItemIntoMediaCollection(PlayableItem playable)
        {
            try
            {
                // Create a MediaCollectionItem for each file to play
                if (playable.HasMediaItems)
                {
                    PlaybackControllerHelper.PopulateMediaCollectionUsingMediaItems(this, CurrentMediaCollection, playable);
                }
                else
                {
                    PlaybackControllerHelper.PopulateMediaCollectionUsingFiles(CurrentMediaCollection, playable);
                }
            }
            catch (Exception ex)
            {
                OnErrorPlayingItem(playable, ex);
            }
        }

        private void QueuePlayableItemLegacy(PlayableItem playable)
        {
            Microsoft.MediaCenter.MediaType type = MediaType.Audio;

            bool success = true;

            foreach (string file in playable.FilesFormattedForPlayer)
            {
                if (!PlaybackControllerHelper.CallPlayMedia(AddInHost.Current.MediaCenterEnvironment, type, file, true))
                {
                    success = false;
                    break;
                }
            }

            if (!success)
            {
                OnErrorPlayingItem(playable, "PlayMedia returned false");
            }
        }

        /// <summary>
        /// Handles the MediaCenterEnvironment.PropertyChanged event
        /// </summary>
        protected void MediaCenterEnvironment_PropertyChanged(IPropertyObject sender, string property)
        {
            Logger.ReportVerbose("MediaCenterEnvironment_PropertyChanged: " + property);

            MediaCenterEnvironment env = sender as MediaCenterEnvironment;

            MediaExperience exp = env.MediaExperience;

            if (exp != null)
            {
                MediaTransport transport = exp.Transport;

                if (transport != null)
                {
                    transport.PropertyChanged -= MediaTransport_PropertyChanged;
                    transport.PropertyChanged += MediaTransport_PropertyChanged;

                    HandlePropertyChange(env, exp, transport, property);
                }
                else
                {
                    Logger.ReportWarning("MediaCenterEnvironment_PropertyChanged: MediaTransport is null");
                }
            }
            else
            {
                Logger.ReportWarning("MediaCenterEnvironment_PropertyChanged: MediaExperience is null");
            }
        }

        /// <summary>
        /// Handles the MediaTransport.PropertyChanged event, which most of the time will be due to Position
        /// </summary>
        protected void MediaTransport_PropertyChanged(IPropertyObject sender, string property)
        {
            MediaTransport transport = sender as MediaTransport;

            MediaCenterEnvironment env = AddInHost.Current.MediaCenterEnvironment;

            MediaExperience exp = env.MediaExperience;

            HandlePropertyChange(env, exp, transport, property);
        }

        private Guid lastItemId;

        private void HandlePropertyChange(MediaCenterEnvironment env, MediaExperience exp, MediaTransport transport, string property)
        {
            PlayState state;
            long positionTicks = 0;

            // If another application is playing the content, such as the WMC autoplay handler, we will
            // not have permission to access Transport properties
            // But we can look at MediaExperience.MediaType to determine if something is playing
            try
            {
                state = transport.PlayState;
                positionTicks = transport.Position.Ticks;
            }
            catch (InvalidOperationException)
            {
                Logger.ReportVerbose("HandlePropertyChange was not able to access MediaTransport. Defaulting values.");
                state = exp.MediaType == Microsoft.MediaCenter.Extensibility.MediaType.Unknown ? Microsoft.MediaCenter.PlayState.Undefined : Microsoft.MediaCenter.PlayState.Playing;
            }

            bool playstateChanged = state != _CurrentPlayState;

            _CurrentPlayState = state;

            // Determine if playback has stopped. Per MSDN documentation, Finished is no longer used with Windows 7
            bool isStopped = state == Microsoft.MediaCenter.PlayState.Finished || state == Microsoft.MediaCenter.PlayState.Stopped || state == Microsoft.MediaCenter.PlayState.Undefined;

            // Don't get tripped up at the initial state of Stopped with position 0
            if (!_HasStartedPlaying)
            {
                if (!isStopped)
                {
                    Logger.ReportVerbose("HandlePropertyChange has recognized that playback has started");
                    _HasStartedPlaying = true;
                    IsStreaming = Playable.CurrentFile.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                    if (Playable.HasMediaItems)
                    {
                        Application.CurrentInstance.CurrentlyPlayingItemId = lastItemId = Playable.CurrentMedia.Id;
                        Application.CurrentInstance.ReportPlaybackStart(Playable.CurrentMedia.ApiId, IsStreaming);
                    }
                }
                else
                {
                    return;
                }
            }

            // protect against really agressive calls
            if (property == "Position")
            {
                Application.CurrentInstance.CurrentlyPlayingItem.CurrentPlaybackPosition = positionTicks;
                var diff = (DateTime.Now - _LastTransportUpdateTime).TotalMilliseconds;

                // Only cancel out Position reports
                if (diff < 3000 && diff >= 0)
                {
                    return;
                }
            }

            _LastTransportUpdateTime = DateTime.Now;

            // Get metadata from player
            MediaMetadata metadata = exp.MediaMetadata;

            string metadataTitle = PlaybackControllerHelper.GetTitleOfCurrentlyPlayingMedia(metadata);
            long metadataDuration = PlaybackControllerHelper.GetDurationOfCurrentlyPlayingMedia(metadata);

            PlaybackStateEventArgs eventArgs = GetCurrentPlaybackState(metadataTitle, metadataDuration, positionTicks);

            // Only fire the progress handler while playback is still active, because once playback stops position will be reset to 0
            OnProgress(eventArgs);

            if (eventArgs.Item != null && eventArgs.Item.HasMediaItems && eventArgs.Item.CurrentMedia.Id != lastItemId)
            {
                // started playing a new item - update
                Application.CurrentInstance.CurrentlyPlayingItemId = lastItemId = eventArgs.Item.MediaItems.ElementAt(eventArgs.CurrentMediaIndex).Id;
            }


            Application.CurrentInstance.ShowNowPlaying = eventArgs.Item == null || eventArgs.Item.ShowNowPlayingView;

            if (playstateChanged)
            {
                FirePropertyChanged("IsPaused");

                if (state == Microsoft.MediaCenter.PlayState.Paused)
                {
                    // allow screensavers/sleep
                    Helper.AllowSleep();
                }
                else if (state == Microsoft.MediaCenter.PlayState.Playing || state == Microsoft.MediaCenter.PlayState.Buffering)
                {
                    // disallow again
                    Helper.PreventSleep();
                }

                // Get the title from the PlayableItem, if it's available. Otherwise use MediaMetadata
                string title = eventArgs.Item == null ? metadataTitle : (eventArgs.Item.HasMediaItems ? eventArgs.Item.MediaItems.ElementAt(eventArgs.CurrentMediaIndex).Name : eventArgs.Item.Files.ElementAt(eventArgs.CurrentFileIndex));

                Logger.ReportVerbose("Playstate changed to {0} for {1}, PositionTicks:{2}, Playlist Index:{3}", state, title, positionTicks, eventArgs.CurrentFileIndex);
                //Logger.ReportVerbose("Refresh rate is {0}", DisplayUtil.GetCurrentRefreshRate());

                PlayStateChanged();
                Logger.ReportVerbose("Back from PlayStateChanged");
            }

            if (isStopped)
            {
                Logger.ReportVerbose("Calling HandleStopedState");
                HandleStoppedState(env, exp, transport, eventArgs);
            }
        }

        /// <summary>
        /// Handles a change of Playstate by firing various events and post play processes
        /// </summary>
        private void HandleStoppedState(MediaCenterEnvironment env, MediaExperience exp, MediaTransport transport, PlaybackStateEventArgs e)
        {
            Logger.ReportVerbose("In HandleStoppedState");
            // Stop listening to the events
            env.PropertyChanged -= MediaCenterEnvironment_PropertyChanged;
            transport.PropertyChanged -= MediaTransport_PropertyChanged;

            Logger.ReportVerbose("Events unhooked");

            // This will prevent us from getting in here twice after playback stops and calling post-play processes more than once.
            _HasStartedPlaying = false;

            CurrentMediaCollection = null;

            var mediaType = exp.MediaType;


            // Check if internal wmc player is still playing, which could happen if the user launches live tv while playing something
            if (mediaType != Microsoft.MediaCenter.Extensibility.MediaType.TV)
            {
                Logger.ReportVerbose("Turning off NPV");
                Application.CurrentInstance.ShowNowPlaying = false;

                if (mediaType == Microsoft.MediaCenter.Extensibility.MediaType.Audio || mediaType == Microsoft.MediaCenter.Extensibility.MediaType.DVD)
                {
                    PlaybackControllerHelper.ReturnToApplication(true);
                }
            }
            else
            {
                Logger.ReportVerbose("Not turning off NPV because Live TV is playing.");
            }

            Helper.AllowSleep();

            // Fire the OnFinished event for each item
            Async.Queue("Playback Finished", () => OnPlaybackFinished(e));
        }

        /// <summary>
        /// Retrieves the current playback item using properties from MediaExperience and Transport
        /// </summary>
        private PlaybackStateEventArgs GetCurrentPlaybackState(string metadataTitle, long metadataDuration, long positionTicks)
        {
            int filePlaylistPosition;
            int currentMediaIndex;
            PlayableItem currentPlayableItem;

            if (CurrentMediaCollection == null)
            {
                currentPlayableItem = PlaybackControllerHelper.GetCurrentPlaybackItemUsingMetadataTitle(this, CurrentPlayableItems, metadataTitle, out filePlaylistPosition, out currentMediaIndex);
            }
            else
            {
                currentPlayableItem = PlaybackControllerHelper.GetCurrentPlaybackItemFromMediaCollection(CurrentPlayableItems, CurrentMediaCollection, out filePlaylistPosition, out currentMediaIndex);

                // When playing multiple files with MediaCollections, if you allow playback to finish, CurrentIndex will be reset to 0, but transport.Position will be equal to the duration of the last item played
                if (filePlaylistPosition == 0 && positionTicks >= metadataDuration)
                {
                    positionTicks = 0;
                }
            }

            return new PlaybackStateEventArgs()
            {
                Position = positionTicks,
                CurrentFileIndex = filePlaylistPosition,
                DurationFromPlayer = metadataDuration,
                Item = currentPlayableItem,
                CurrentMediaIndex = currentMediaIndex
            };
        }

        /// <summary>
        /// Puts the player into fullscreen mode
        /// </summary>
        public override void GoToFullScreen()
        {
            if (Playable != null && Playable.UseCustomPlayer)
            {
                Application.CurrentInstance.OpenCustomPlayerUi();
            }
            else
            {
                var mce = MediaExperience ?? PlaybackControllerHelper.GetMediaExperienceUsingReflection();

                if (mce != null)
                {
                    Logger.ReportVerbose("Going fullscreen...");
                    mce.GoToFullScreen();
                }
                else
                {
                    Logger.ReportError("AddInHost.Current.MediaCenterEnvironment.MediaExperience is null, we have no way to go full screen!");
                    AddInHost.Current.MediaCenterEnvironment.Dialog(Application.CurrentInstance.StringData("CannotMaximizeDial"), "", Microsoft.MediaCenter.DialogButtons.Ok, 0, true);
                }
                
            }
        }

        protected MediaExperience MediaExperience
        {
            get
            {
                return AddInHost.Current.MediaCenterEnvironment.MediaExperience;
            }
        }

        /// <summary>
        /// Pauses playback
        /// </summary>
        protected override void PauseInternal()
        {
            var transport = PlaybackControllerHelper.GetCurrentMediaTransport();
            if (transport != null)
            {
                transport.PlayRate = 1;
            }
        }

        /// <summary>
        /// Unpauses playback
        /// </summary>
        protected override void UnPauseInternal()
        {
            var transport = PlaybackControllerHelper.GetCurrentMediaTransport();
            if (transport != null)
            {
                transport.PlayRate = 2;
            }
        }

        /// <summary>
        /// Stops playback
        /// </summary>
        protected override void StopInternal()
        {
            PlaybackControllerHelper.Stop();
        }

        /// <summary>
        /// Takes a Media object and returns the list of files that will be sent to the player
        /// </summary>
        internal override IEnumerable<string> GetPlayableFiles(Media media)
        {
            IEnumerable<string> files = base.GetPlayableFiles(media);

            Video video = media as Video;

            if (video != null)
            {
                if (video.MediaType == Library.MediaType.BluRay)
                {
                    files = files.Select(i => PlaybackControllerHelper.GetBluRayPath(i));
                }
            }

            return ShouldTranscode ? files.Select(f => PlaybackControllerHelper.GetTranscodedPath(f)) : files;
        }

        /// <summary>
        /// When playback is based purely on files, this will take the files that were supplied to the PlayableItem,
        /// and create the actual paths that will be sent to the player
        /// </summary>
        internal override IEnumerable<string> GetPlayableFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                MediaBrowser.Library.MediaType mediaType = MediaBrowser.Library.MediaTypeResolver.DetermineType(file);

                if (mediaType == Library.MediaType.BluRay)
                {
                    yield return PlaybackControllerHelper.GetBluRayPath(file);
                }

                yield return ShouldTranscode ? PlaybackControllerHelper.GetTranscodedPath(file) : file;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            MediaTransport transport = PlaybackControllerHelper.GetCurrentMediaTransport();

            if (transport != null)
            {
                transport.PropertyChanged -= MediaTransport_PropertyChanged;
            }

            base.Dispose(isDisposing);
        }

        protected bool ShouldTranscode
        {
            get
            {
                return Config.Instance.EnableTranscode360 && Application.RunningOnExtender;
            }
        }

        public override void FastForward()
        {
            var rate = AddInHost.Current.MediaCenterEnvironment.MediaExperience.Transport.PlayRate;
            if (rate.Equals(2))
            {
                rate = 3;
            } else if (rate < 5)
            {
                rate++;
            } else if (rate > 6 && rate < 9)
            {
                rate--;
            }
            else
            {
                rate = 2;
            }

            AddInHost.Current.MediaCenterEnvironment.MediaExperience.Transport.PlayRate = rate;
        }

        public override void Rewind()
        {
            var rate = AddInHost.Current.MediaCenterEnvironment.MediaExperience.Transport.PlayRate;
            if (rate.Equals(2))
            {
                rate = 6;
            } else if (rate > 3 && rate < 5)
            {
                rate--;
            } else if (rate > 5 && rate < 8)
            {
                rate++;
            }
            else
            {
                rate = 2;
            }

            AddInHost.Current.MediaCenterEnvironment.MediaExperience.Transport.PlayRate = rate;
        }

        /// <summary>
        /// Skip to next item in collection playback
        /// </summary>
        public override void SkipToNextInCollection()
        {
            if (CanSkipInCollection)
            {
                // fake this by skipping to the end of the current item - I can't find an api call to actually skip ahead in the collection
                Seek(CurrentFileDurationTicks-15000000);

            }
        }

        public override void ResetPlayRate()
        {
            AddInHost.Current.MediaCenterEnvironment.MediaExperience.Transport.PlayRate = 2;
        }

        /// <summary>
        /// Moves the player to a given position
        /// </summary>
        protected override void SeekInternal(long position)
        {
            try
            {
                var mce = AddInHost.Current.MediaCenterEnvironment;
                Logger.ReportVerbose("Trying to seek position :" + new TimeSpan(position).ToString());
                PlaybackControllerHelper.WaitForStream(mce);
                mce.MediaExperience.Transport.Position = new TimeSpan(position);
            }
            catch (Exception e)
            {
                Logger.ReportException("Error attempting to seek",e);
            }
        }

        public override bool IsPaused
        {
            get
            {
                return _CurrentPlayState == Microsoft.MediaCenter.PlayState.Paused;
            }
        }

        public override bool CanPause
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override void DisplayMessage(string header, string message, int timeout)
        {
            AddInHost.Current.MediaCenterEnvironment.Dialog(message, header, DialogButtons.Ok, timeout, false);
        }
    }
}
