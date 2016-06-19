using System.Linq;

namespace MediaBrowser.Library.Entities
{
    public class ChannelFolder : Folder
    {
        protected override bool HideEmptyFolders
        {
            get
            {
                return false;
            }
        }

        protected override System.Collections.Generic.List<BaseItem> GetCachedChildren()
        {
            //protect against channel providing ourselves as a child
            return Kernel.Instance.MB3ApiRepository.RetrieveChannelChildren(ParentChannel.ApiId, ApiId).Where(c => c.Id != Id && Parent != null && c.Id != Parent.Id).ToList();
        }

        protected override int GetUnwatchedCount()
        {
            if (this.ChildrenLoaded)
            {
                return base.GetUnwatchedCount();
            }

            return 0;
        }

        private BaseItem _parentChannel;
        protected BaseItem ParentChannel
        { get { return _parentChannel ?? (_parentChannel = FindParent<Channel>() ?? Parent); } }
    }
}