using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library {
    public class TripleTapIndex : IComparable<TripleTapIndex> {
        public int Index;
        public string Name;

        public int CompareTo(TripleTapIndex other) {
            return this.Name.CompareTo(other.Name);
        }
    }
}
