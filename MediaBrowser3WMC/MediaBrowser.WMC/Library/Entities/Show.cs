using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Library.Entities {
    public class Show : Video, IShow {

        [Persist]
        public string MpaaRating { get; set; }
        
        [Persist]
        public Single? ImdbRating { get; set; }

        [Persist]
        public string ImdbID { get; set; }

        [Persist]
        public List<Actor> Actors { get; set; }

        [Persist]
        public List<string> Directors { get; set; }

        [Persist]
        public List<string> Genres { get; set; }

        [Persist]
        public virtual List<string> Studios { get; set; }

        [Persist]
        public List<string> Writers { get; set; }

        [Persist]
        public int? ProductionYear { get; set; }

        [Persist]
        public string AspectRatio { get; set; }

        [Persist]
        public override string TagLine { get; set; }

        [Persist]
        public string Plot { get; set; }

        public override string ShortDescription
        {
            get { return Plot; }
            set { Plot = value; }
        }

        public override string OfficialRating { get { return MpaaRating ?? ""; }}
    }
}
