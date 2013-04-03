using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Library.Entities {
    public class Movie : Show, IMovie, ISupportsTrailers {

        [Persist]
        public string TrailerPath {get; set;}

        /// <summary>
        /// This paths of all the parts of the movie. Eg part1.avi, part2.avi
        /// </summary>
        [Persist]
        List<string> VolumePaths { get; set; }

        [Persist]
        public string TmdbID { get; set; }

        public bool ContainsTrailers {
            get {
                return TrailerFiles.Any();
            }
        }

        public IEnumerable<string> TrailerFiles { 
            get {
                return Kernel.Instance.GetTrailers(this);
            }
        }

    }
}
