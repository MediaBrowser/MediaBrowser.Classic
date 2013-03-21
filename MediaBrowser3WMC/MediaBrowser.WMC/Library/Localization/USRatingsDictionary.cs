using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Localization
{
    public class USRatingsDictionary : Dictionary<string,int>
    {
        public USRatingsDictionary()
        {
            this.Add("G", 1);
            this.Add("E", 1);
            this.Add("EC", 1);
            this.Add("TV-G", 1);
            this.Add("TV-Y", 2);
            this.Add("TV-Y7", 3);
            this.Add("TV-Y7-FV", 4);
            this.Add("PG", 5);
            this.Add("TV-PG", 5);
            this.Add("PG-13", 7);
            this.Add("T", 7);
            this.Add("TV-14", 8);
            this.Add("R", 9);
            this.Add("M", 9);
            this.Add("TV-MA", 9);
            this.Add("NC-17", 10);
            this.Add("AO", 15);
            this.Add("RP", 15);
            this.Add("UR", 15);
            this.Add("NR", 15);
            this.Add("X", 15);
            this.Add("XXX", 100);
        }
    }
}
