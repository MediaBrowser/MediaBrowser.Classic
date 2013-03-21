using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities
{
    public class LocalCacheFolder : IndexFolder
    {
        public LocalCacheFolder() : base()
        {
        }

        public LocalCacheFolder(List<BaseItem> list) : base(list)
        {
        }

        protected override List<BaseItem> GetCachedChildren()
        {
            List<BaseItem> items = null;
            //using (new MediaBrowser.Util.Profiler(this.Name + " child retrieval"))
            {
                //Logger.ReportInfo("Getting Children for: "+this.Name);
                var children = Kernel.Instance.LocalRepo.RetrieveChildren(Id);
                items = children != null ? children.ToList() : null;
            }
            return items;
        }
    }
}
