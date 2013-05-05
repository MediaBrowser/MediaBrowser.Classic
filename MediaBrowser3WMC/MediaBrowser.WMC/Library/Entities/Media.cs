using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities {
    public abstract class Media : BaseItem, IHasMediaStreams
    {
        protected PlaybackStatus playbackStatus;
        public virtual PlaybackStatus PlaybackStatus { get { return playbackStatus; } set { playbackStatus = value; } }
        public abstract IEnumerable<string> Files {get;}
        protected IMediaLocation location;


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
                return true;
            }
        }

        public virtual int RunTime
        {
            get { return 0; }
        }

        public override bool CanResume
        {
            get
            {
                return PlaybackStatus != null && PlaybackStatus.CanResume;
            }
        }

        public List<MediaStream> MediaStreams { get; set; }

        [Persist]
        public MediaInfoData MediaInfo { get; set; }

    }
}
