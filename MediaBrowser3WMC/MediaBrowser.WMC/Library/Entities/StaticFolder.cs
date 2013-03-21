using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities
{
    public class StaticFolder : IndexFolder
    {
        public StaticFolder(List<BaseItem> children) : base(children)
        {
        }

        protected override List<BaseItem> GetCachedChildren()
        {
            return new List<BaseItem>();
        }
    }
}
