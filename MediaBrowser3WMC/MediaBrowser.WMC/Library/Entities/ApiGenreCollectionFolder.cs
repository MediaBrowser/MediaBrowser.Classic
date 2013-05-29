using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ApiGenreCollectionFolder : ApiSourcedFolder
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
                               Fields = new[] {ItemFields.SortName, },
                               SortBy = new[] {"SortName"}
                           };
            }
        }

        protected override List<BaseItem> GetCachedChildren()
        {
            return Kernel.Instance.MB3ApiRepository.RetrieveGenres(Query).Select(g => new ApiGenreFolder(g, IncludeItemTypes)).Cast<BaseItem>().ToList(); 
        }

    }
}
