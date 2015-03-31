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
    public class ApiStudioFolder : ApiSourcedFolder<ItemQuery>
    {
        public ApiStudioFolder() : base()
        {}

        public ApiStudioFolder(BaseItem item, string searchParentId = null, string[] includeTypes = null, string[] excludeTypes = null, Folder parent = null)
            : base(item, searchParentId, includeTypes, excludeTypes, parent)
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
                               Fields = MB3ApiRepository.StandardFields,
                               StudioIds = new[] {ApiId}
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
                return "Studio-" + Parent.ApiId;
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
