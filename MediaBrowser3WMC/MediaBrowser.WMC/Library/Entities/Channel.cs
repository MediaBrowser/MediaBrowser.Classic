using System.Linq;

namespace MediaBrowser.Library.Entities
{
    public class Channel : Folder
    {
        protected override System.Collections.Generic.List<BaseItem> GetCachedChildren()
        {
            return Kernel.Instance.MB3ApiRepository.RetrieveChannelChildren(ApiId).ToList();
        }
    }
}