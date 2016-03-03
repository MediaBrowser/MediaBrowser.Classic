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

        protected override List<BaseItem> ActualChildren
        {
            get
            {
                return base.ActualChildren.Concat(virtualChildren).OrderBy(i => i.SortName).OfType<Folder>().Where(i => !Config.Instance.UseLegacyFolders || i.CollectionType != "boxsets").Cast<BaseItem>().ToList();
            }
        }

        public override bool ShowUnwatchedCount
        {
            get { return false; }
        }

        public BaseItem FindVirtualChild(Guid id)
        {
            return virtualChildren.Find(i => i.Id == id);
        }

        protected override bool HideEmptyFolders
        {
            get { return false; }
        }


        //public override bool RefreshMetadata(MediaBrowser.Library.Metadata.MetadataRefreshOptions options) {
        //    // these are root folders they support no metadata
        //    return false;
        //}
    }
}
