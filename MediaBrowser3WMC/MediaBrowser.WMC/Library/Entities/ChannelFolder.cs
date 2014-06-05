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
            return Kernel.Instance.MB3ApiRepository.RetrieveChannelChildren(ParentChannel.ApiId, ApiId).ToList();
        }

        private BaseItem _parentChannel;
        protected BaseItem ParentChannel
        { get { return _parentChannel ?? (_parentChannel = FindParent<Channel>() ?? Parent); } }
    }
}