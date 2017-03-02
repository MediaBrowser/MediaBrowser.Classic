
using System.Collections.Generic;

namespace MediaBrowser.Library.Entities
{
    /// <summary>
    /// Class MusicArtist
    /// </summary>
    public class MusicArtist : Folder
    {
        public Dictionary<string, string> AlbumCovers { get; set; }

        public override bool ShowUnwatchedCount
        {
            get { return false; }
        }
    }
}
