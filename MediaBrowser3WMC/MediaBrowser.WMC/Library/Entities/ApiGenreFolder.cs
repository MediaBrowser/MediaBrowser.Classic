using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ApiGenreFolder : ApiSourcedFolder<ItemQuery>
    {
        public ApiGenreFolder() : base()
        {}

        public ApiGenreFolder(BaseItem item, string searchParentId = null, string[] includeTypes = null, string[] excludeTypes = null) : base(item, searchParentId, includeTypes, excludeTypes)
        {
        }

        public override ItemQuery Query
        {
            get
            {
                return new ItemQuery
                           {
                               UserId = Kernel.CurrentUser.ApiId,
                               ParentId = SearchParentId,
                               IncludeItemTypes = IncludeItemTypes,
                               ExcludeItemTypes = ExcludeItemTypes,
                               Recursive = true,
                               IsPlayed = Filters.IsUnWatched ? false : (bool?)null,
                               Filters = GetFilterArray(),
                               Fields = MB3ApiRepository.StandardFields,
                               Genres = new[] {HttpUtility.UrlEncode(Name)}
                           };
            }
        }

        /// <summary>
        /// This is used to construct a display prefs id and we want it to reflect the item types we contain
        /// </summary>
        public override string DisplayMediaType
        {
            get
            {
                return "Genre-" + (IncludeItemTypes != null ? IncludeItemTypes.FirstOrDefault() : Parent.ApiId);
            }
            set
            {
                base.DisplayMediaType = value;
            }
        }

        public override string[] RalIncludeTypes
        {
            get
            {
                return IncludeItemTypes;
            }
            set { base.RalIncludeTypes = value; }
        }


    }
}
