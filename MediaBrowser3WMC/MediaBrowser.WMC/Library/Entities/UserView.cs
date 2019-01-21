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
            if (CollectionType == "movies")
            {
                if (Config.Instance.ShowMovieSubViews)
                {
                    //Create sub-views
                    return new List<BaseItem>
                           {
                               new ApiCollectionFolder {Id = Kernel.Instance.MovieCwFolderGuid, IndexId = ApiId, Name = "Continue Watching", DisplayMediaType = "Movies", IncludeItemTypes = new[] {"Movie"}, ItemFilters = new [] {ItemFilter.IsResumable}, SearchParentId = ApiId},
                               new ApiCollectionFolder {Id = Kernel.Instance.MovieFavoritesFolderGuid, IndexId = ApiId, Name = "Favorites", DisplayMediaType = "Movies", ItemFilters = new [] {ItemFilter.IsFavorite}, SearchParentId = ApiId},
                               new ApiCollectionFolder {Id = Kernel.Instance.MovieFolderGuid, IndexId = ApiId, Name = "Movies", DisplayMediaType = "Movies", IncludeItemTypes = new[] {"Movie"}, SearchParentId = ApiId},
                               new ApiGenreCollectionFolder {Id = Kernel.Instance.MovieGenreFolderGuid, IndexId = ApiId, Name = Kernel.Instance.ConfigData.MovieGenreFolderName, SearchParentId = ApiId, DisplayMediaType = "MovieGenres", IncludeItemTypes = new[] {"Movie"}, GenreType = GenreType.Movie}
                           };

                }
                if (!Config.Instance.UseLegacyFolders)
                {
                    //Just get all movies under us instead of the split- out views that will be our children
                    return Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                    {
                        UserId = Kernel.CurrentUser.ApiId,
                        ParentId = ApiId,
                        Recursive = true,
                        CollapseBoxSetItems = CollapseBoxSets,
                        IncludeItemTypes = new[] { "Movie" },
                        Fields = MB3ApiRepository.StandardFields,
                    }).ToList();
               
                }

            }

            if (CollectionType == "tvshows")
            {
                if (Config.Instance.ShowTvSubViews)
                {
                    
                    //Build views
                    return new List<BaseItem>
                           {
                               new ApiCollectionFolder {Id = Kernel.Instance.TvCwFolderGuid, IndexId = ApiId, Name = "Continue Watching", DisplayMediaType = "Series", IncludeItemTypes = new[] {"Episode"}, ItemFilters = new [] {ItemFilter.IsResumable}, SearchParentId = ApiId},
                               new NextUpFolder {Id = Kernel.Instance.NextUpFolderGuid, Name = "Next Up", DisplayMediaType = "Episode", SearchParentId = ApiId},
                               new ApiCollectionFolder {Id = Kernel.Instance.TvFavoriteShowsFolderGuid, IndexId = ApiId, Name = "Favorite Shows", DisplayMediaType = "Series", IncludeItemTypes = new [] {"Series"}, ItemFilters = new [] {ItemFilter.IsFavorite}, SearchParentId = ApiId},
                               new ApiCollectionFolder {Id = Kernel.Instance.TvFavoriteEpisodesFolderGuid, IndexId = ApiId, Name = "Favorite Episodes", DisplayMediaType = "Episode", IncludeItemTypes = new [] {"Episode"}, ItemFilters = new [] {ItemFilter.IsFavorite}, SearchParentId = ApiId},
                               new ApiCollectionFolder {Id = Kernel.Instance.TvFolderGuid, IndexId = ApiId, Name = "Shows", DisplayMediaType = "Series", IncludeItemTypes = new[] {"Series"}, SearchParentId = ApiId},
                               new ApiGenreCollectionFolder {Id = Kernel.Instance.TvGenresFolderGuid, IndexId = ApiId, Name = "Genres", SearchParentId = ApiId, DisplayMediaType = "TvGenres", IncludeItemTypes = new[] {"Series"}, GenreType = GenreType.Movie},
                               new UpcomingTvFolder { Id = new Guid("63CFD844-61AE-42E6-878D-916BC2372173"), Name = LocalizedStrings.Instance.GetString("UTUpcomingTv") }                           
                           };

                }
                
                if (!Config.Instance.UseLegacyFolders)
                {
                    //Just get all series under us instead of the split- out views that will be our children
                    return Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                    {
                        UserId = Kernel.CurrentUser.ApiId,
                        ParentId = ApiId,
                        Recursive = true,
                        CollapseBoxSetItems = CollapseBoxSets,
                        IncludeItemTypes = new[] { "Series" },
                        Fields = MB3ApiRepository.StandardFields,
                    }).ToList();

                }

            }
                            
            if (!Config.Instance.UseLegacyFolders && CollectionType == "MusicGenres")
            {
                
                return Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                {
                    UserId = Kernel.CurrentUser.ApiId,
                    ParentId = ApiId,
                    Recursive = true,
                    IncludeItemTypes = new[] { "MusicGenre" },
                    Fields = MB3ApiRepository.StandardFields,
                }).Select(g => new ApiGenreFolder(g, null, new []{"MusicAlbum","MusicArtist"})).Cast<BaseItem>().ToList();

            }

            if (CollectionType == "music")
            {
                return new List<BaseItem>
                       {
                           new ApiCollectionFolder {Id = Kernel.Instance.MusicAlbumFolderGuid, IndexId = ApiId, Name = "Albums", DisplayMediaType = "MusicAlbums", IncludeItemTypes = new[] {"MusicAlbum"}, SearchParentId = ApiId},
                           new ApiCollectionFolder {Id = Kernel.Instance.MusicPlaylistsFolderGuid, IndexId = ApiId, Name = "Playlists", DisplayMediaType = "MusicAlbums", IncludeItemTypes = new[] {"Playlist"}, MediaType = "Audio", SearchParentId = ApiId},
                           new ApiCollectionFolder {Id = Kernel.Instance.MusicFavoritesFolderGuid, IndexId = ApiId, Name = "Favorites", DisplayMediaType = "MusicAlbums", ItemFilters = new ItemFilter[] {ItemFilter.IsFavorite}, SearchParentId = ApiId},
                           new ApiArtistsCollectionFolder {Id = Kernel.Instance.MusicArtistsFolderGuid, IndexId = ApiId, Name = "Artists", DisplayMediaType = "MusicArtists", IncludeItemTypes = new[] {"MusicArtist"}, SearchParentId = ApiId},
                           new ApiArtistsCollectionFolder {Id = Kernel.Instance.MusicAlbumArtistsFolderGuid, IndexId = ApiId, Name = "Album Artists", DisplayMediaType = "MusicArtists", IncludeItemTypes = new[] {"AlbumArtist"}, SearchParentId = ApiId},
                           new ApiGenreCollectionFolder {Id = Kernel.Instance.MusicGenreFolderGuid, IndexId = ApiId, Name = Kernel.Instance.ConfigData.MusicGenreFolderName, SearchParentId = ApiId, DisplayMediaType = "MusicGenres", IncludeItemTypes = new[] {"MusicAlbum"}, GenreType = GenreType.Music}
                       };
            }
                            
            // Otherwise get our children which will be whatever the default is

            // since we have our own latest implementation, exclude those from these views.
            // also eliminate flat songs view since that will probably not perform well
            return base.GetCachedChildren().Where(c => !(c is UserView && (c.Name.Equals("Latest", StringComparison.OrdinalIgnoreCase) || c.Name.Equals("Songs", StringComparison.OrdinalIgnoreCase) || c.Name.Equals("Collections", StringComparison.OrdinalIgnoreCase)))).ToList();

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