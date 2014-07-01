using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class Channel : Folder
    {
        protected override List<BaseItem> GetCachedChildren()
        {
            return Kernel.Instance.MB3ApiRepository.RetrieveChannelChildren(ApiId).ToList();
        }

        protected override bool HideEmptyFolders
        {
            get
            {
                return false;
            }
        }

        public virtual bool ForceStaticStream { get { return false; } }

        protected override IEnumerable<BaseItem> GetLatestItems(string recentItemOption, int maxItems)
        {
            switch (recentItemOption)
            {
                case "watched":
                    return Kernel.Instance.MB3ApiRepository.RetrieveLatestChannelItems(ApiId, new [] {ItemFilter.IsPlayed, }).ToList();


                case "unwatched":
                    return Kernel.Instance.MB3ApiRepository.RetrieveLatestChannelItems(ApiId, new [] {ItemFilter.IsUnplayed, }).ToList();

                default:
                    return Kernel.Instance.MB3ApiRepository.RetrieveLatestChannelItems(ApiId).ToList();

            }
        }

    }
}