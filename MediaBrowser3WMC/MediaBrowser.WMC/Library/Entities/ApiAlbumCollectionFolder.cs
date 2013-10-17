using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ApiAlbumCollectionFolder : ApiSourcedFolder<ItemQuery>
    {

        public override ItemQuery Query
        {
            get
            {
                return new ItemQuery
                           {
                               UserId = Kernel.CurrentUser.ApiId,
                               ParentId = Kernel.Instance.RootFolder.ApiId,
                               IncludeItemTypes = IncludeItemTypes,
                               Recursive = true,
                               Fields = MB3ApiRepository.StandardFields,
                               SortBy = new[] {"SortName"}
                           };
            }
        }

        public override int MediaCount
        {
            get { return ApiRecursiveItemCount ?? (int)(ApiRecursiveItemCount = GetItemCount()); }
        }

        protected int GetItemCount()
        {
            var counts = Kernel.ApiClient.GetItemCounts(Kernel.CurrentUser.Id);
            return counts.AlbumCount;

        }

        protected override List<BaseItem> GetCachedChildren()
        {
            var ret = Kernel.Instance.MB3ApiRepository.RetrieveItems(Query).ToList();
            ApiRecursiveItemCount = ret.Count;
            return ret;
        }

    }

}
