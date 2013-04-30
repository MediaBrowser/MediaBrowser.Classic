using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Query
{
    public class FilterProperties
    {
        public int RatedLessThan { get; set; }
        public int RatedGreaterThan { get; set; }
        public bool? IsFavorite { get; set; }
        public bool? IsWatched { get; set; }
        public IEnumerable<string> OfTypes { get; set; } 

        public FilterProperties()
        {
            RatedGreaterThan = 0;
            RatedLessThan = 1000;
            OfTypes = new string[] {};
        }
    }
}
