using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    class ApiArtistsCollectionFolder : ApiSourcedFolder<ItemsByNameQuery>
    {
        public override ItemsByNameQuery Query
        {
            get
            {
                return new ItemsByNameQuery
                {
                    UserId = Kernel.CurrentUser.ApiId,
                    Recursive = true,
                    ParentId = SearchParentId,
                    Fields = MB3ApiRepository.StandardFields,
                    SortBy = new[] { "SortName" }
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
            var ret = IncludeItemTypes.Contains("MusicArtist") ? Kernel.Instance.MB3ApiRepository.RetrieveMusicArtists(Query).ToList() :
                                                                Kernel.Instance.MB3ApiRepository.RetrieveAlbumArtists(Query).ToList();
            ApiRecursiveItemCount = ret.Count;
            return ret;
        }

    }
}