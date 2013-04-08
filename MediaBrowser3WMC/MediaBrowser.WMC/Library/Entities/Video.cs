using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities {
    public class Video : Media {

        protected IMediaLocation location;

        public IMediaLocation MediaLocation {
            get {
                if (location == null) {

                    location = Kernel.Instance.GetLocation<IMediaLocation>(Path);
                }
                return location;
            }
        }


        [NotSourcedFromProvider]
        [Persist]
        public MediaType MediaType { get; set; }

        [Persist]
        public int? RunningTime { get; set; }

        [Persist]
        public MediaInfoData MediaInfo { get; set; }

        [Persist]
        public string VideoFormat { get; set; }

        public override bool AssignFromItem(BaseItem item) {
            bool changed = this.MediaType != ((Video)item).MediaType;
            this.MediaType = ((Video)item).MediaType;
            return changed | base.AssignFromItem(item);
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
                    if (DateCreated <= Config.Instance.AssumeWatchedBefore)
                        playbackStatus.PlayCount = 1;
                    //Kernel.Instance.SavePlayState(this, playbackStatus);  //removed this so we don't create files until we actually play something -ebr
                }
                return playbackStatus;
            }

            set { base.PlaybackStatus = value; }
        }

        public override int RunTime
        {
            get
            {
                return RunningTime ?? 0;
            }
        }

        public List<MediaStream> MediaStreams { get; set; } 

        public virtual IEnumerable<string> VideoFiles {
            get {
                if (!ContainsRippedMedia && MediaLocation is IFolderMediaLocation)
                {
                    foreach (var path in GetChildVideos((IFolderMediaLocation)MediaLocation, null))
                    {
                        yield return path;
                    }
                }
                else
                {
                    yield return Path;
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

       public static IEnumerable<string> GetChildVideos(IFolderMediaLocation location, string[] ignore) {
            if (location.Path.EndsWith("$RECYCLE.BIN")) yield break;

            foreach (var child in location.Children)
	        {
                // MCE plays vobs natively 
                if (child.IsVideo() || child.IsVob()) yield return child.Path;
                else if (child is IFolderMediaLocation && Kernel.Instance.ConfigData.EnableNestedMovieFolders) {
                    if (ignore != null && ignore.Any(path => path.ToUpper() == child.Name.ToUpper())) {
                        continue;
                    }
                    foreach (var grandChild in GetChildVideos(child as IFolderMediaLocation, null)) {
                        yield return grandChild;
                    }
                }
	        }
        }

    }
}
