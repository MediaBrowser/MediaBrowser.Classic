using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities {

    /// <summary>
    /// This is special entity for root folders. It aggregates the physical root folder with a virtual list of items that are provided by plugins 
    /// </summary>
    public class AggregateFolder : Folder {

        List<BaseItem> virtualChildren = new List<BaseItem>();

        public List<BaseItem> VirtualChildren
        {
            get
            {
                return virtualChildren;
            }
        }

        public void AddVirtualChild(BaseItem child) {
            virtualChildren.Add(child);
        }

        public void RemoveVirtualChild(BaseItem child)
        {
            virtualChildren.Remove(virtualChildren.Find(c => c.Id == child.Id));
        }

        protected override List<BaseItem> GetNonCachedChildren()
        {
            var list =  base.GetNonCachedChildren();
            list.AddRange(virtualChildren);
            return list;
        }

        public BaseItem FindVirtualChild(Guid id)
        {
            return virtualChildren.Find(i => i.Id == id);
        }

        
        //public override bool RefreshMetadata(MediaBrowser.Library.Metadata.MetadataRefreshOptions options) {
        //    // these are root folders they support no metadata
        //    return false;
        //}
    }
}
