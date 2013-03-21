using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Library.Entities {

    // our provider seem to think a series is a type of season, so the entity is reflecting that
    // further down the line this may change

    public class Season : Series {

        [Persist]
        public string SeasonNumber { get; set; }

        public override string OfficialRating
        {
            get
            {
                if (string.IsNullOrEmpty(MpaaRating))
                {
                    var aSeries = this.Parent as Series;
                    if (aSeries != null)
                    {
                        return aSeries.OfficialRating;
                    }
                    else return "None";
                }
                else 
                    return MpaaRating;
            }
        }

        public override Series OurSeries
        {
            get
            {
                return this.Parent as Series ?? Series.BlankSeries;
            }
        }
    }
}
