using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ApiGenreFolder : ApiSourcedFolder
    {
        public ApiGenreFolder() : base()
        {}

        public ApiGenreFolder(BaseItem item, string[] includeTypes = null, string[] excludeTypes = null) : base(item, includeTypes, excludeTypes)
        {
        }

        public override ItemQuery Query
        {
            get
            {
                return new ItemQuery
                           {
                               UserId = Kernel.CurrentUser.ApiId,
                               ParentId = Kernel.Instance.RootFolder.ApiId,
                               IncludeItemTypes = IncludeItemTypes,
                               ExcludeItemTypes = ExcludeItemTypes,
                               Recursive = true,
                               Fields = MB3ApiRepository.StandardFields,
                               Genres = new[] {Name}
                           };
            }
        }

        protected override string[] RalIncludeTypes
        {
            get
            {
                return IncludeItemTypes;
            }
        }


    }
}
