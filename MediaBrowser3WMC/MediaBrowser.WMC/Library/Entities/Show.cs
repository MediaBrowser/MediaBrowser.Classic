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

        private List<Actor> _actors;

        [Persist]
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
                if (!FullDetailsLoaded)
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

        [Persist]
        public int? ProductionYear { get; set; }

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

        protected void LoadFullDetails()
        {
            var temp = Kernel.Instance.MB3ApiRepository.RetrieveItem(this.Id) as Show;
            if (temp != null)
            {
                temp.FullDetailsLoaded = true;
                Actors = temp.Actors;
                Directors = temp.Directors;
                Writers = temp.Writers;
                Genres = temp.Genres;
                Studios = temp.Studios;
            }

            FullDetailsLoaded = true;
        }
    }
}
