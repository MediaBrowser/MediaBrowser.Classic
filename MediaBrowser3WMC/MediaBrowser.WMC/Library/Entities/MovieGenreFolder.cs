using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class MovieGenreFolder : ApiSourcedCollectionFolder
    {
        public string Genre { get; set; }

        public MovieGenreFolder() : base()
        {
        }

        public MovieGenreFolder(BaseItem genre)
        {
            Genre = genre.Name;
            Id = genre.Id;
            PrimaryImagePath = genre.PrimaryImagePath;
            BackdropImagePaths = genre.BackdropImagePaths;

        }

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
                               Fields = MB3ApiRepository.StandardFields,
                               Genres = new[] {Genre}
                           };
            }
        }

        protected override string[] RalIncludeTypes
        {
            get
            {
                return new[] {"Movie", "BoxSet"};
            }
        }


    }
}
