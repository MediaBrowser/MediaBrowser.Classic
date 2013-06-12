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
                               Fields = new[] {ItemFields.SortName, ItemFields.ItemCounts, },
                               SortBy = new[] {"SortName"}
                           };
            }
        }

        public GenreType GenreType { get; set; }

        protected override List<BaseItem> GetCachedChildren()
        {
            var ret = GenreType == GenreType.Music ?
                Kernel.Instance.MB3ApiRepository.RetrieveMusicGenres(Query).Select(g => new ApiGenreFolder(g, IncludeItemTypes)).Cast<BaseItem>().ToList() :
                Kernel.Instance.MB3ApiRepository.RetrieveGenres(Query).Select(g => new ApiGenreFolder(g, IncludeItemTypes)).Cast<BaseItem>().ToList();
            ApiRecursiveItemCount = ret.Count;
            return ret;
        }

    }

    public enum GenreType
    {
        Music,
        Movie
    }
}
