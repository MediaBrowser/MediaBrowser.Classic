using System;
using System.Collections.Generic;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Library.Entities
{
    public class Photo : Show
    {
        [Persist]
        public string MonthTaken { get; set; }

        [Persist]
        public DateTime DateTaken { get; set; }

        public Photo()
            : base()
        {
            this.playbackStatus = new PlaybackStatus(); // just need something valid here
        }

        public override IEnumerable<string> Files
        {
            get { yield return this.Path; }
        }

        public override bool PlayAction(Item item)
        {
            //assume they want a slide show from here
            MBPhotoController.Instance.SlideShow(item);
            return true;
        }

    }
}
