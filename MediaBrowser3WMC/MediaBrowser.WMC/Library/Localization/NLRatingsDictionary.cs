using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Localization
{
    public class NLRatingsDictionary : Dictionary<string, int>
    {
        public NLRatingsDictionary()
        {
            this.Add("NL-AL", 1);
            this.Add("NL-MG6", 2);
            this.Add("NL-6", 3);
            this.Add("NL-9", 5);
            this.Add("NL-12", 6);
            this.Add("NL-16", 8);
        }
    }
}
