using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Localization
{
    public class AURatingsDictionary : Dictionary<string, int>
    {
        public AURatingsDictionary()
        {
            this.Add("AU-G", 1);
            this.Add("AU-PG", 5);
            this.Add("AU-M", 6);
            this.Add("AU-M15+", 7);
            this.Add("AU-R18+", 9);
            this.Add("AU-X18+", 10);
        }
    }
}
