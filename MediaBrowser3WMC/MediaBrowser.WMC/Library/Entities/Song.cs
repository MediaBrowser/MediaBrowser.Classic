using System;
using MediaBrowser.Model.Entities;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MediaBrowser.Library.Entities
{
    /// <summary>
    /// Class Audio
    /// </summary>
    public class Song : Media, IShow, IGroupInIndex
    {
        
        /// <summary>
        /// The unknown album
        /// </summary>
        private static readonly MusicAlbum UnknownAlbum = new MusicAlbum {Name = "<Unknown>", BackdropImagePaths = new List<string>()};

        private static readonly Person UnknownArtist = new Person {Name = "<Unknown>", BackdropImagePaths = new List<string>()};

        /// <summary>
        /// Gets or sets the artist.
        /// </summary>
        /// <value>The artist.</value>
        public string Artist { get; set; }
        /// <summary>
        /// Gets or sets the album.
        /// </summary>
        /// <value>The album.</value>
        public string Album { get; set; }
        /// <summary>
        /// Gets or sets the album id.
        /// </summary>
        /// <value>The album id.</value>
        public string AlbumId { get; set; }
        /// <summary>
        /// Gets or sets the album artist.
        /// </summary>
        /// <value>The album artist.</value>
        public string AlbumArtist { get; set; }

        private MusicAlbum _albumItem;
        private Person _albumArtistPerson;
        public IContainer MainContainer { get { return AlbumItem; } }
        public MusicAlbum AlbumItem { get { return _albumItem ?? (_albumItem = (Parent as MusicAlbum) ?? RetrieveAlbum() ?? UnknownAlbum); } }
        public Person AlbumArtistPerson { get { return _albumArtistPerson ?? (_albumArtistPerson = RetrieveArtist() ?? UnknownArtist); } }
        public List<Actor> Actors { get; set; }
        public List<string> Directors { get; set; }
        public List<string> Genres { get; set; }
        public float? ImdbRating { get; set; }
        public string MpaaRating { get; set; }
        public List<string> Studios { get; set; }
        public override string FirstAired
        {
            get { return base.FirstAired ?? AlbumItem.FirstAired; }
            set { base.FirstAired = value; }
        }

        public override int? ProductionYear
        {
            get { return base.ProductionYear ?? AlbumItem.ProductionYear; }
            set { base.ProductionYear = value; }
        }

        protected MusicAlbum RetrieveAlbum()
        {
            return !string.IsNullOrEmpty(AlbumId) ? Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(AlbumId)) as MusicAlbum : null;
        }

        protected Person RetrieveArtist()
        {
            return !string.IsNullOrEmpty(AlbumArtist) ? Kernel.Instance.MB3ApiRepository.RetrieveArtist(AlbumArtist) : null;
        }

        public override string Overview
        {
            get
            {
                return base.Overview ?? AlbumItem.Overview ?? AlbumArtistPerson.Overview;
            }
            set
            {
                base.Overview = value;
            }
        }

        public override List<string> BackdropImagePaths
        {
            get
            {
                return base.BackdropImagePaths ?? AlbumItem.BackdropImagePaths ?? AlbumArtistPerson.BackdropImagePaths;
            }
            set
            {
                base.BackdropImagePaths = value;
            }
        }
        public override IEnumerable<string> Files
        {
            get { return new[] {Path}; }
        }
    }
}
