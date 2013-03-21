using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Library.Entities {

    [SkipSerializationValidation]
    public class PlaybackStatus : IPersistableChangeNotifiable {

        public event EventHandler<EventArgs> WasPlayedChanged;

        public bool WasPlayed {
            get { return (PlayCount > 0); }
            set
            {
                if (value && !WasPlayed) {
                    PlayCount = 1;
                } else if (!value && WasPlayed) {
                    PlayCount = 0;
                    //PositionTicks = 0;
                    //PlaylistPosition = 0;
                } 

            }
        }

        public bool CanResume {
            get { return this.PositionTicks > 0 || PlaylistPosition > 0; }
        }

        [Persist]
        public Guid Id { get; set; }

        [Persist]
        int playCount;
        public int PlayCount { 
            get {
                return playCount;
            }
            set {
                bool oldWasPlayed = WasPlayed;
                playCount = value;
                if (oldWasPlayed != WasPlayed && WasPlayedChanged != null) {
                    WasPlayedChanged(this, null);
                }
            }
        }

        [Persist]
        public long PositionTicks { get; set; }

        [Persist]
        public int PlaylistPosition {  get; set; }

        [Persist]
        public DateTime LastPlayed { get; set; }


        bool? wasPlayedCache = null;
        public void OnChanged() {
            if (WasPlayedChanged != null && (wasPlayedCache == null || wasPlayedCache.Value != WasPlayed)) {
                WasPlayedChanged(this, null);
            }
            wasPlayedCache = WasPlayed;
        }
    }
}

