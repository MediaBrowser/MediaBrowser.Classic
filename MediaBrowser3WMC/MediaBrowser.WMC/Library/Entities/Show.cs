using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;

namespace MediaBrowser.Library.Entities {
    public class Show : Video, IShow {

        [Persist]
        public string MpaaRating { get; set; }

        [Persist]
        public string ImdbID { get; set; }

        protected bool SpecialFeaturesLoaded { get; set; }

        private readonly object _detailLock = new object();

        private List<Video> _specialFeatures;
        public List<Video> SpecialFeatures
        {
            get { return _specialFeatures ?? (_specialFeatures = GetSpecialFeatures()); }
        }

        private List<Actor> _actors;
        public List<Actor> Actors
        {
            get
            {
                if (!FullDetailsLoaded)
                {
                    LoadFullDetails();
                }
                return _actors;
            }

            set { _actors = value; }
        }

        private List<string> _directors;

        [Persist]
        public List<string> Directors
        {
            get
            {
                if (_directors == null && !FullDetailsLoaded)
                {
                    LoadFullDetails();
                }
                return _directors;
            }

            set { _directors = value; }
        }

        private List<string> _genres;

        [Persist]
        public List<string> Genres
        {
            get
            {
                if (!FullDetailsLoaded)
                {
                    LoadFullDetails();
                }
                return _genres;
            }

            set { _genres = value; }
        }

        private List<string> _studios;

        [Persist]
        public virtual List<string> Studios
        {
            get
            {
                if (!FullDetailsLoaded)
                {
                    LoadFullDetails();
                }
                return _studios;
            }

            set { _studios = value; }
        }

        private List<string> _writers;

        [Persist]
        public List<string> Writers
        {
            get
            {
                if (!FullDetailsLoaded)
                {
                    LoadFullDetails();
                }
                return _writers;
            }

            set { _writers = value; }
        }

        public override List<Chapter> Chapters
        {
            get
            {
                if (!FullDetailsLoaded)
                {
                    LoadFullDetails();
                }

                return base.Chapters;
            }
            set
            {
                base.Chapters = value;
            }
        }

        [Persist]
        public override string TagLine { get; set; }

        [Persist]
        public string Plot { get; set; }

        public override string ShortDescription
        {
            get { return Plot; }
            set { Plot = value; }
        }

        public override string OfficialRating { get { return MpaaRating ?? ""; } set { MpaaRating = value; }}

        protected List<Video> GetSpecialFeatures()
        {
            return Kernel.Instance.MB3ApiRepository.RetrieveSpecialFeatures(ApiId).ToList();
        }

        public void LoadFullDetails()
        {
            lock (_detailLock)
            {
                if (FullDetailsLoaded) return;

                Logger.ReportVerbose("Loading full details for {0}",Name);
                var temp = Kernel.Instance.MB3ApiRepository.RetrieveItem(this.Id) as Show;
                if (temp != null)
                {
                    temp.FullDetailsLoaded = true;
                    Actors = temp.Actors;
                    Directors = temp.Directors;
                    Writers = temp.Writers;
                    Genres = temp.Genres;
                    Studios = temp.Studios;
                    Chapters = temp.Chapters;
                    CriticRatingSummary = temp.CriticRatingSummary;
                    PlaybackAllowed = temp.PlaybackAllowed;
                    if (IsOffline)
                    {
                        //ensure we are still offline
                        if (!Directory.Exists(System.IO.Path.GetDirectoryName(Path) ?? ""))
                        {
                            LocationType = LocationType.Offline;
                        }
                        else
                        {
                            LocationType = LocationType.FileSystem;
                            IsOffline = false;
                        }
                    }
                }
                FullDetailsLoaded = true;
            }
        }
    }
}
