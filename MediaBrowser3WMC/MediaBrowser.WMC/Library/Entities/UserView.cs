using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Localization;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    class UserView : IbnSourcedFolder
    {
        protected override bool ForceIbn
        {
            get { return Parent != Kernel.Instance.RootFolder; }
        }

        protected override bool HideEmptyFolders
        {
            get { return false; }
        }

        public override string[] RalIncludeTypes
        {
            get
            {
                switch (CollectionType)
                {
                    case "movies":
                        return new[] {"movie"};
                    case "tvshows":
                        return new[] {"episode"};
                    case "music":
                        return new[] {"audio"};
                    case "boxsets":
                        return new[] {"boxset"};
                    case "musicvideos":
                        return new[] { "musicvideo" };
                    case "photos":
                        return new[] { "photo", "video" };
                    case "homevideos":
                        return new[] { "video" };
                    case "playlists":
                        return new[] { "playlist" };
                    default:
                        return null;
                }
            }
        }

        public override string[] RalExcludeTypes
        {
            get
            {
                switch (CollectionType)
                {
                    case "boxsets":
                        return new[] {"series", "season", "musicalbum", "musicartist", "folder","movie","audio","episode"};
                    default:
                        return base.RalExcludeTypes;
                }
            }
        }

        protected override bool CollapseBoxSets
        {
            get {
                switch ((CollectionType ?? "").ToLower())
                {
                    case "moviegenre":
                    case "musicgenre":
                    case "tvgenre":
                        return false;
                    default:
                        return base.CollapseBoxSets;
                }
            }
        }

        public override bool ShowUnwatchedCount
        {
            get { return false; }
        }

        public override Dictionary<string, string> IndexByOptions
        {
            get
            {
                if (!Config.Instance.ShowMovieSubViews) return base.IndexByOptions;

                switch ((CollectionType ?? "").ToLower())
                {
                    case "moviemovies":
                    case "tvshowseries":
                        return base.IndexByOptions;
                    default:
                        return new Dictionary<string, string> { { LocalizedStrings.Instance.GetString("NoneDispPref"), "" } };
                }
            }
        }

        public override void OnNavigatingInto()
        {
            switch ((CollectionType ?? "").ToLower())
            {
                case "tvnextup":
                    Logger.ReportVerbose("Reloading next up tv");
                    ReloadChildren();
                    break;
            }

            base.OnNavigatingInto();
        }

        protected override List<BaseItem> GetCachedChildren()
        {
            if (!Config.Instance.UseLegacyFolders && !Config.Instance.ShowMovieSubViews && CollectionType == "movies")
            {
                //Just get all movies under us instead of the split- out views that will be our children
                return Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                {
                    UserId = Kernel.CurrentUser.ApiId,
                    ParentId = ApiId,
                    Recursive = true,
                    IncludeItemTypes = new[] { "Movie" },
                    Fields = MB3ApiRepository.StandardFields,
                }).ToList();

            }

            if (!Config.Instance.UseLegacyFolders && !Config.Instance.ShowTvSubViews && CollectionType == "tvshows")
            {
                //Just get all series under us instead of the split- out views that will be our children
                return Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                {
                    UserId = Kernel.CurrentUser.ApiId,
                    ParentId = ApiId,
                    Recursive = true,
                    IncludeItemTypes = new[] { "Series" },
                    Fields = MB3ApiRepository.StandardFields,
                }).ToList();

            }
                            
            // Otherwise get our children which will be the split views

            // since we have our own latest implementation, exclude those from these views.
            // also eliminate flat songs view since that will probably not perform well
            var ret = base.GetCachedChildren().Where(c => !(c is UserView && (c.Name.Equals("Latest", StringComparison.OrdinalIgnoreCase) || c.Name.Equals("Songs", StringComparison.OrdinalIgnoreCase)))).ToList();
            if (CollectionType == "tvshows")
            {
                ret.Add(new UpcomingTvFolder { Id = new Guid("63CFD844-61AE-42E6-878D-916BC2372173"), Name = LocalizedStrings.Instance.GetString("UTUpcomingTv") });
            }

            return ret;
        }

        public override string DisplayPreferencesId
        {
            get
            {
                return Parent == Kernel.Instance.RootFolder ? ApiId : (CollectionType + Kernel.CurrentUser.Name).GetMD5().ToString();
            }
            set
            {
                base.DisplayPreferencesId = value;
            }
        }
    }
}