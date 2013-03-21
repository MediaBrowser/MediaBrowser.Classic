using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities
{
    /// <summary>
    /// This exists just so that we can have separate display prefs
    /// </summary>
    public class SearchResultFolder : IndexFolder
    {
        public SearchResultFolder()
            : base()
        { }

        public SearchResultFolder(List<BaseItem> children)
            : base(children)
        { }
    }
}
