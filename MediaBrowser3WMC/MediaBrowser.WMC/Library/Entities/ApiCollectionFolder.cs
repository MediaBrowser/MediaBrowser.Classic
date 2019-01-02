using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ApiCollectionFolder : ApiSourcedFolder<ItemQuery>
    {

        public override ItemQuery Query
        {
            get
            {
                return new ItemQuery
                           {
                               UserId = Kernel.CurrentUser.ApiId,
                               IncludeItemTypes = IncludeItemTypes,
                               Recursive = true,
                               ParentId = SearchParentId,
                               MediaTypes = new [] {MediaType},
                               Filters = ItemFilters,
                               CollapseBoxSetItems = CollapseBoxSets,
                               Fields = MB3ApiRepository.StandardFields,
                               SortBy = new[] {"SortName"}
                           };
            }
        }

        public override int MediaCount
        {
            get { return ApiRecursiveItemCount ?? 0; }
        }

        /// <summary>
        /// This causes severe performance problems on large index folders and should not be necessary
        /// </summary>
        protected override bool HideEmptyFolders
        {
            get
            {
                return false;
            }
        }

        protected override List<BaseItem> GetCachedChildren()
        {
            var ret = Kernel.Instance.MB3ApiRepository.RetrieveItems(Query).ToList();
            ApiRecursiveItemCount = ret.Count;
            return ret;
        }

    }
}
