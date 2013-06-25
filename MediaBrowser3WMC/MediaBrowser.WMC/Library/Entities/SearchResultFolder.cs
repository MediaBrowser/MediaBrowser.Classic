using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Logging;

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

        protected override List<BaseItem> GetCachedChildren()
        {
            return new List<BaseItem>();
        }

        public override string DisplayPreferencesId
        {
            get
            {
                return (DisplayMediaType + Kernel.CurrentUser.Name).GetMD5().ToString();
            }
            set
            {
                base.DisplayPreferencesId = value;
            }
        }

    }
}
