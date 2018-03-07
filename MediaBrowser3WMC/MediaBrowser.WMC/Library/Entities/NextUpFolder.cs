using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Library.Entities
{
    class NextUpFolder : ApiCollectionFolder
    {
        protected override List<BaseItem> GetCachedChildren()
        {
            return Kernel.Instance.MB3ApiRepository.RetrieveNextUpItems(SearchParentId).ToList();
        }
    }
}
