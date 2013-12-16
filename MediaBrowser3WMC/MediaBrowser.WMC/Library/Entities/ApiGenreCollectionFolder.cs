using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ApiGenreCollectionFolder : ApiSourcedFolder<ItemQuery>
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
                               Fields = new[] {ItemFields.SortName },
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

            switch (GenreType)
            {
                case GenreType.Movie:
                    return counts.MovieCount;

                    case GenreType.Music:
                    return counts.SongCount;

                default:
                    return 0;
            }
        }

        public GenreType GenreType { get; set; }

        public override string DisplayMediaType
        {
            get
            {
                return GenreType.ToString();
            }
            set
            {
                base.DisplayMediaType = value;
            }
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
            var ret = GenreType == GenreType.Music ?
                Kernel.Instance.MB3ApiRepository.RetrieveMusicGenres(Query).Select(g => new ApiGenreFolder(g, Kernel.Instance.RootFolder.ApiId, IncludeItemTypes)).Cast<BaseItem>().ToList() :
                Kernel.Instance.MB3ApiRepository.RetrieveGenres(Query).Select(g => new ApiGenreFolder(g, Kernel.Instance.RootFolder.ApiId, IncludeItemTypes)).Cast<BaseItem>().ToList();
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
