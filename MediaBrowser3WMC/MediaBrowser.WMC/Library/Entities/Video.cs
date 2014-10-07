using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaBrowser.ApiInteraction;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Playables;
using MediaBrowser.Library.Streaming;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using VideoOptions = MediaBrowser.Model.Dlna.VideoOptions;

namespace MediaBrowser.Library.Entities {
    public class Video : Media {

        [Persist]
        public string VideoFormat { get; set; }

        public bool Is3D { get; set; }

        public override bool AssignFromItem(BaseItem item) {
            bool changed = this.MediaType != ((Video)item).MediaType;
            this.MediaType = ((Video)item).MediaType;
            return changed | base.AssignFromItem(item);
        }

        public bool ForceStaticStream
        {
            get
            {
                var channel = FindParent<Channel>();
                return channel != null && channel.ForceStaticStream;
            }
        }

        public override IEnumerable<string> Files
        {
            get { return VideoFiles; }
        }
        
        public override PlaybackStatus PlaybackStatus {
            get {

                if (playbackStatus != null) return playbackStatus;

                //playbackStatus = Kernel.Instance.ItemRepository.RetrievePlayState(this.Id);
                if (playbackStatus == null) {
                    playbackStatus = PlaybackStatusFactory.Instance.Create(Id); // initialise an empty version that items can bind to
                    if (DateCreated <= Config.Instance.AssumeWatchedBefore || !IsPlayable)
                        playbackStatus.PlayCount = 1;
                    //Kernel.Instance.SavePlayState(this, playbackStatus);  //removed this so we don't create files until we actually play something -ebr
                }
                return playbackStatus;
            }

            set { base.PlaybackStatus = value; }
        }

        public override bool PassesFilter(Query.FilterProperties filters)
        {
            return (playbackStatus.WasPlayed == filters.IsUnWatched) && base.PassesFilter(filters);
        }

        public override int RunTime
        {
            get
            {
                return RunningTime ?? 0;
            }
        }

        public virtual IEnumerable<string> VideoFiles {
            get
            {
                if (LocationType == LocationType.FileSystem && Path != null && (Path == System.IO.Path.GetPathRoot(Path) || Directory.Exists(System.IO.Path.GetDirectoryName(Path ?? "") ?? "")))
                {
                    yield return Path;
                }
                else
                {
                    var bitrate = Kernel.ApiClient.GetMaxBitRate();
                    if (ForceStaticStream && bitrate > 10000000)
                    {
                        Logger.ReportInfo("Unable to access {0}.  Will try to stream statically.", Path);
                        yield return Kernel.ApiClient.GetVideoStreamUrl(new VideoStreamOptions
                                                              {
                                                                  ItemId = ApiId,
                                                                  Static = true

                                                              });
                    }
                    else
                    {
                        Logger.ReportInfo("Unable to access {0}.  Will try to stream at {1}bps.", Path, bitrate);
                        yield return PlaybackControllerHelper.BuildStreamingUrl(this, bitrate);
                    }

                }
            }
        }

        /// <summary>
        /// Returns true if the Video is from ripped media (DVD , BluRay , HDDVD or ISO)
        /// </summary>
        public bool ContainsRippedMedia {
            get {
                return IsRippedMedia(MediaType);
            }
        }

        public IEnumerable<string> IsoFiles
        {
            get
            {
                if (MediaLocation is IFolderMediaLocation)
                {
                    return Helper.GetIsoFiles(Path);
                }

                return new string[] { Path };
            }
        }

        public bool ContainsTrailers { get; set; }
        private IEnumerable<string> _trailerFiles;

        public IEnumerable<string> TrailerFiles
        {
            get { return _trailerFiles ?? (_trailerFiles = Kernel.ApiClient.GetLocalTrailers(Kernel.CurrentUser.Id, this.Id.ToString()).Select(t => t.Path)); }
            set { _trailerFiles = value; }
        }

        public static bool IsRippedMedia(MediaType type)
        {
            return type == MediaType.BluRay ||
               type == MediaType.DVD ||
               type == MediaType.ISO ||
               type == MediaType.HDDVD;
        }

    }
}
