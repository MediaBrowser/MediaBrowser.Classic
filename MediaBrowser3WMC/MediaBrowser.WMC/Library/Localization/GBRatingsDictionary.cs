using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Localization
{
    public class GBRatingsDictionary : Dictionary<string, int>
    {
        public GBRatingsDictionary()
        {
            this.Add("GB-U", 1);
            this.Add("GB-PG", 5);
            this.Add("GB-12", 6);
            this.Add("GB-12A", 7);
            this.Add("GB-15", 8);
            this.Add("GB-18", 9);
            this.Add("GB-R18", 15);
        }
    }
}
