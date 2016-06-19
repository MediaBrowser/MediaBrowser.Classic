using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities {
    public abstract class Media : BaseItem
    {
        protected PlaybackStatus playbackStatus;
        public virtual PlaybackStatus PlaybackStatus { get { return playbackStatus; } set { playbackStatus = value; } }
        public abstract IEnumerable<string> Files {get;}
        protected IMediaLocation location;
        public List<MediaSourceInfo> MediaSources { get; set; } 

        public override bool PlayAction(Item item)
        {
            Application.CurrentInstance.Play(item, false, false, false, false); //play with no intros
            return true;
        }

        public IMediaLocation MediaLocation
        {
            get
            {
                if (location == null)
                {

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

        public override bool IsPlayable
        {
            get
            {
                return LocationType != LocationType.Virtual && base.IsPlayable;
            }
        }

        public bool WillStream
        {
            get
            {
                var files = Files.ToList();
                return files.Any() && (files.First().StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || files.First().StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            }
        }

        public virtual int RunTime
        {
            get { return 0; }
        }

        public override bool CanResumeMain
        {
            get
            {
                return PlaybackStatus != null && PlaybackStatus.CanResume;
            }
        }

        public override bool CanResume
        {
            get
            {
                return CanResumeMain || (PartCount > 1 && AdditionalParts.Any(p => p.CanResume));
            }
        }

        public List<MediaStream> MediaStreams { get; set; }

        [Persist]
        public MediaInfoData MediaInfo { get; set; }

        [Persist]
        public string AspectRatio { get; set; }

        public override bool Watched
        {
            get
            {
                return PlaybackStatus.WasPlayed;
            }
            set
            {
                base.Watched = value;
            }
        }

    }
}
