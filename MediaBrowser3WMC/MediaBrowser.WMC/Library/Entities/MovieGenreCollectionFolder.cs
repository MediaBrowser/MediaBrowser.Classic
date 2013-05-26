using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class MovieGenreCollectionFolder : ApiSourcedCollectionFolder
    {
        protected override ItemQuery Query
        {
            get
            {
                return new ItemQuery
                           {
                               UserId = Kernel.CurrentUser.ApiId,
                               ParentId = Kernel.Instance.RootFolder.ApiId,
                               IncludeItemTypes = new[] {"Movie", "BoxSet"},
                               Recursive = true,
                               Fields = new[] {ItemFields.SortName, },
                               SortBy = new[] {"SortName"}
                           };
            }
        }

        protected override List<BaseItem> ActualChildren
        {
            get { return Kernel.Instance.MB3ApiRepository.RetrieveGenres(Query).Select(g => new MovieGenreFolder(g)).Cast<BaseItem>().ToList(); }
        }

        protected override string[] RalIncludeTypes
        {
            get
            {
                return new[] {"Movie", "BoxSet"};
            }
        }

        protected override string DefaultPrimaryImagePath
        {
            get
            {
                return "resx://MediaBrowser/MediaBrowser.Resources/Favorites";
            }
        }

        public override string Name
        {
            get
            {
                return Kernel.Instance.ConfigData.MovieGenreFolderName;
            }
            set
            {
                base.Name = value;
            }
        }

        //protected override string DefaultPrimaryImagePath
        //{
        //    get
        //    {
        //        return "resx://MediaBrowser/MediaBrowser.Resources/Favorites";
        //    }
        //}

    }
}
