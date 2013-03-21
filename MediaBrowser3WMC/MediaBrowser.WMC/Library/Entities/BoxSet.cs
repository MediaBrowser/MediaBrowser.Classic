using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Localization;

namespace MediaBrowser.Library.Entities
{
    public class BoxSet : Folder, IMovie
    {
        [Persist]
        public string MpaaRating { get; set; }

        [Persist]
        public Single? ImdbRating { get; set; }

        [Persist]
        public List<Actor> Actors { get; set; }

        [Persist]
        public List<string> Directors { get; set; }

        [Persist]
        public List<string> Writers { get; set; }

        [Persist]
        public List<string> Genres { get; set; }

        [Persist]
        public List<string> Studios { get; set; }

        [Persist]
        public int? RunningTime { get; set; }

        [Persist]
        public string ImdbID { get; set; }

        [Persist]
        public string TmdbID { get; set; }

        [Persist]
        public string Status { get; set; }

        [Persist]
        public string TVDBSeriesId { get; set; }

        [Persist]
        public string AspectRatio { get; set; }

        [Persist]
        public int? ProductionYear { get; set; }

        [Persist]
        public MediaInfoData MediaInfo { get; set; }

        [Persist]
        public string Plot { get; set; }

        [Persist]
        public string TrailerPath { get; set; }

        //doesn't make sense to index a box set...
        private Dictionary<string, string> ourIndexOptions = new Dictionary<string, string>() {
            {LocalizedStrings.Instance.GetString("NoneDispPref"), ""}
        };

        public override Dictionary<string, string> IndexByOptions
        {
            get
            {
                return ourIndexOptions;
            }
            set
            {
                ourIndexOptions = value;
            }
        }

        public override string OfficialRating
        {
            get
            {
                return MpaaRating ?? (FirstChild != null ? FirstChild.OfficialRating : "");
            }
        }


    }
}
