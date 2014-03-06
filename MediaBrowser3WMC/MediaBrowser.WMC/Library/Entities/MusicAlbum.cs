using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities
{
    /// <summary>
    /// Class MusicAlbum
    /// </summary>
    public class MusicAlbum : Folder, IShow, IContainer
    {

        public string AspectRatio { get; set; }

        public int? ProductionYear { get; set; }

        public string AlbumArtist { get; set; }

        public List<Actor> Actors { get; set; }
        public List<string> Directors { get; set; }

        public List<string> Genres { get; set; }

        public float? ImdbRating { get; set; }
        public string MpaaRating { get; set; }

        public int? RunningTime { get; set; }

        public List<string> Studios { get; set; }

    }
}
