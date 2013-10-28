using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities {
    public class Video : Media {

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
            return (filters.IsWatched == null || playbackStatus.WasPlayed == filters.IsWatched) && base.PassesFilter(filters);
        }

        public override int RunTime
        {
            get
            {
                return RunningTime ?? 0;
            }
        }

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
                    if (Directory.Exists(System.IO.Path.GetDirectoryName(Path ?? "") ?? ""))
                    {
                        yield return Path;
                    }
                    else
                    {
                        yield return  ContainsRippedMedia ? Kernel.ApiClient.GetVideoStreamUrl(new VideoStreamOptions
                                                                                  {
                                                                                      ItemId = ApiId,
                                                                                      OutputFileExtension = ".ts",
                                                                                      //AudioStreamIndex = FindAudioStream(Kernel.CurrentUser.Dto.Configuration.AudioLanguagePreference)
                                                                                  })
                        : Kernel.ApiClient.GetVideoStreamUrl(new VideoStreamOptions
                                                                                  {
                                                                                      ItemId = ApiId,
                                                                                      Static = true
                                                                                  });
                    }
                }
            }
        }

        protected static List<string> StreamableCodecs = new List<string> {"DTS", "DTS Express", "AC3", "MP3"}; 

        /// <summary>
        /// Find the first streamable audio stream for the specified language
        /// </summary>
        /// <returns></returns>
        protected int FindAudioStream(string lang = "")
        {
            if (string.IsNullOrEmpty(lang)) lang = "eng";
            Logging.Logger.ReportVerbose("Looking for audio stream in {0}", lang);
            MediaStream stream = null;
            foreach (var codec in StreamableCodecs)
            {
                stream = MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Audio && (s.Language == null || s.Language.Equals(lang, StringComparison.OrdinalIgnoreCase)) 
                    && s.Codec.Equals(codec,StringComparison.OrdinalIgnoreCase));
                if (stream != null) break;
                
            }
            Logging.Logger.ReportVerbose("Requesting audio stream #{0}", stream != null ? stream.Index : 0);
            return stream != null ? stream.Index : 0;
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
