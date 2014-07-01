using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ChannelCollectionFolder : LocalIbnSourcedFolder
    {
        protected override bool HideEmptyFolders
        {
            get
            {
                return false;
            }
        }

        protected override IEnumerable<BaseItem> GetLatestItems(string recentItemOption, int maxItems)
        {
            switch (recentItemOption)
            {
                case "watched":
                    return Kernel.Instance.MB3ApiRepository.RetrieveLatestChannelItems(null, new[] { ItemFilter.IsPlayed, }).ToList();


                case "unwatched":
                    return Kernel.Instance.MB3ApiRepository.RetrieveLatestChannelItems(null, new[] { ItemFilter.IsUnplayed, }).ToList();

                default:
                    return Kernel.Instance.MB3ApiRepository.RetrieveLatestChannelItems().ToList();

            }
        }

    }
}