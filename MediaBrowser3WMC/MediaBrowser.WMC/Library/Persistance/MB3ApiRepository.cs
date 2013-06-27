using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Localization;
using MediaBrowser.Library.Logging;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Persistance
{
    public class MB3ApiRepository
    {
        protected static class Mb3Translator
        {
            public static Dictionary<string, Type> TypeMap = new Dictionary<string, Type>
                                                                 {
                                                                     {"Folder", typeof (Folder)},
                                                                     {"Movie", typeof (Movie)},
                                                                     {"Trailer", typeof (RemoteTrailer)},
                                                                     {"Series", typeof (Series)},
                                                                     {"Season", typeof (Season)},
                                                                     {"Episode", typeof (Episode)},
                                                                     {"Video", typeof (Movie)},
                                                                     {"BoxSet", typeof (BoxSet)},
                                                                     {"Person", typeof (Person)},
                                                                     {"Genre", typeof (Genre)},
                                                                     {"MusicGenre", typeof (MusicGenre)},
                                                                     {"IndexFolder", typeof(IndexFolder)},
                                                                     {"MusicAlbum", typeof(MusicAlbum)},
                                                                     {"MusicAlbumDisc", typeof(MusicAlbum)},
                                                                     {"MusicArtist", typeof(MusicArtist)},
                                                                     {"Audio", typeof(Song)},
                                                                     {"MusicVideo", typeof(MusicVideo)},
                                                                     {"AggregateFolder", typeof (AggregateFolder)},
                                                                     {"CollectionFolder", typeof (Folder)},
                                                                     {"YoutubeCollectionFolder", typeof (Folder)},
                                                                     {"YoutubeVideo", typeof (Movie)},
                                                                     {"TrailerCollectionFolder", typeof (Folder)},
                                                                 };

        }

        public void AddRegisteredType(string key, Type type)
        {
            Mb3Translator.TypeMap[key] = type;
        }

        public IEnumerable<IMetadataProvider> RetrieveProviders(Guid guid)
        {
            throw new NotImplementedException();
        }

        public void SaveProviders(Guid guid, IEnumerable<IMetadataProvider> providers)
        {
            throw new NotImplementedException();
        }

        public void SaveItem(BaseItem item)
        {
            //throw new NotImplementedException();
        }

        public AggregateFolder RetrieveRoot()
        {
            // Retrieve the root for current user
            var root = Kernel.ApiClient.GetRootFolder(Kernel.CurrentUser.Id);
            return root != null ? (AggregateFolder)GetItem(root, "AggregateFolder") : null;
            
        }

        public BaseItem RetrieveItem(Guid id)
        {
            var dto = Kernel.ApiClient.GetItem(id.ToString());
            return dto != null ? GetItem(dto, dto.Type) : null;
        }

        public Genre RetrieveGenre(string name)
        {
            var dto = Kernel.ApiClient.GetGenre(name);
            return dto != null ? GetItem(dto, "Genre") as Genre : null;
        }

        public Person RetrievePerson(string name)
        {
            var dto = Kernel.ApiClient.GetPerson(name);
            return dto != null ? GetIbnItem(dto, "Person") as Person : null;
        }

        protected BaseItem GetIbnItem(BaseItemDto mb3Item, string itemType)
        {
            var item = InstantiateItem(itemType);
            if (item != null)
            {
                item.Name = mb3Item.Name;
                item.Id = new Guid(mb3Item.Id);

                if (mb3Item.HasPrimaryImage)
                {
                    switch (itemType)
                    {
                        case "Person":
                            item.PrimaryImagePath = Kernel.ApiClient.GetPersonImageUrl(mb3Item.Name, new ImageOptions {ImageType = ImageType.Primary});
                            break;
                        case "Genre":
                            item.PrimaryImagePath = Kernel.ApiClient.GetGenreImageUrl(mb3Item.Name, new ImageOptions {ImageType = ImageType.Primary});
                            break;

                    }
                }
            }

            return item;
        }

        protected BaseItem InstantiateItem(string itemType)
        {
            try
            {
                Type typ;
                if (Mb3Translator.TypeMap.TryGetValue(itemType, out typ))
                {
                    return (BaseItem)Activator.CreateInstance(typ);
                }
                else
                {
                    return Serializer.Instantiate<BaseItem>(itemType);
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error trying to create instance of type: " + itemType, e);
                return null;
            }
            
        }

        protected BaseItem GetItem(BaseItemDto mb3Item, string itemType)
        {
            var item = InstantiateItem(itemType);

            if (item != null)
            {
                item.Name = mb3Item.Name;
                //Logger.ReportVerbose("Item {0} is {1}", item.Name, item.GetType().Name);
                item.Path = mb3Item.Path;
                item.DateCreated = (mb3Item.DateCreated ?? DateTime.MinValue).ToLocalTime();
                //item.DateModified = (mb3Item.DateModified ?? DateTime.MinValue).ToLocalTime();
                item.DisplayMediaType = mb3Item.DisplayMediaType;
                item.Overview = mb3Item.Overview;
                item.SortName = mb3Item.SortName;
                item.TagLine = mb3Item.Taglines != null && mb3Item.Taglines.Count > 0 ? mb3Item.Taglines[0] : null;
                item.UserData = mb3Item.UserData;
                item.ApiParentId = mb3Item.ParentId;
                //if (item.ApiParentId == null) Logger.ReportVerbose("Parent Id is null for {0}",item.Name);

                var runTimeTicks = IsRippedMedia(mb3Item.VideoType ?? VideoType.VideoFile) ? mb3Item.OriginalRunTimeTicks ?? mb3Item.RunTimeTicks : mb3Item.RunTimeTicks;

                var index = item as IndexFolder;
                if (index != null)
                {
                    index.Id = mb3Item.Id.GetMD5();
                    index.IndexId = mb3Item.Id;
                }
                else
                {
                    item.Id = new Guid(mb3Item.Id);
                }

                if (mb3Item.ImageTags != null)
                {
                    foreach (var tag in mb3Item.ImageTags)
                    {
                        var url = item is MusicGenre ? Kernel.ApiClient.GetMusicGenreImageUrl(mb3Item.Name, new ImageOptions {ImageType = tag.Key, Tag = tag.Value}) :
                                    item is Genre ? Kernel.ApiClient.GetGenreImageUrl(mb3Item.Name, new ImageOptions {ImageType = tag.Key, Tag = tag.Value}) :
                                                Kernel.ApiClient.GetImageUrl(mb3Item.Id, new ImageOptions {ImageType = tag.Key, Tag = tag.Value, CropWhitespace = false});
                        switch (tag.Key)
                        {
                            case ImageType.Primary:
                                item.PrimaryImagePath = url;
                                break;

                            case ImageType.Logo:
                                item.LogoImagePath = url;
                                break;

                            case ImageType.Art:
                                item.ArtImagePath = url;
                                break;

                            case ImageType.Banner:
                                item.BannerImagePath = url;
                                break;

                            case ImageType.Thumb:
                                item.ThumbnailImagePath = url;
                                break;

                            case ImageType.Disc:
                                item.DiscImagePath = url;
                                break;

                        }
                    }
                }

                if (mb3Item.BackdropImageTags != null)
                {
                    var ndx = 0;
                    item.BackdropImagePaths = new List<string>();
                    foreach (var bd in mb3Item.BackdropImageTags)
                    {
                        item.BackdropImagePaths.Add(Kernel.ApiClient.GetImageUrl(mb3Item.Id, new ImageOptions { ImageType = ImageType.Backdrop, Tag = bd, ImageIndex = ndx}));
                        ndx++;
                    }
                }

                var folder = item as Folder;
                if (folder != null)
                {
                    // Fill in display prefs and indexby options
                    folder.DisplayPreferencesId = mb3Item.DisplayPreferencesId;
                    folder.IndexByOptions = mb3Item.IndexOptions != null ? mb3Item.IndexOptions.ToDictionary(o => o) : 
                        new Dictionary<string, string> {{LocalizedStrings.Instance.GetString("NoneDispPref"), ""}};

                    // recursive media count
                    folder.ApiRecursiveItemCount = mb3Item.RecursiveItemCount;

                    // don't replace this with ?? until after the server implementing this has been released...
                    if (mb3Item.RecursiveUnplayedItemCount != null) folder.UnwatchedCount = mb3Item.RecursiveUnplayedItemCount.Value;
                }

                var video = item as Video;
                if (video != null && video.Path != null)
                {
                    video.ContainsTrailers = mb3Item.HasTrailer;
                    if (mb3Item.Video3DFormat != null)
                    {
                        video.VideoFormat = mb3Item.Video3DFormat == Video3DFormat.FullSideBySide || mb3Item.Video3DFormat == Video3DFormat.HalfSideBySide ? "Sbs3D" : "Digital3D";
                    }
                    else
                    {
                        video.VideoFormat = "Standard";
                    }
                }

                var media = item as Media;
                if (media != null)
                {
                    if (mb3Item.MediaType == Model.Entities.MediaType.Video)
                    {
                        if (mb3Item.VideoType == VideoType.VideoFile)
                        {
                            media.MediaType = MediaTypeResolver.DetermineType(media.Path);
                        }
                        else
                        {
                            switch (mb3Item.VideoType)
                            {
                                case VideoType.BluRay:
                                    media.MediaType = MediaType.BluRay;
                                    break;
                                case VideoType.Dvd:
                                    media.MediaType = MediaType.DVD;
                                    break;
                                case VideoType.Iso:
                                    media.MediaType = MediaType.ISO;
                                    break;
                                default:
                                    media.MediaType = MediaType.Unknown;
                                    break;
                            }
                            
                        }
                    }
                    else
                    {
                        media.MediaType = MediaTypeResolver.DetermineType(media.Path);
                    }

                    if (mb3Item.MediaStreams != null)
                    {
                        var vidStream = mb3Item.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                        var audStream = mb3Item.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Audio);
                        var subtStream = mb3Item.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Subtitle);
                        media.MediaStreams = mb3Item.MediaStreams;
                        media.AspectRatio = !string.IsNullOrEmpty(mb3Item.AspectRatio) ? mb3Item.AspectRatio : null;
                        media.SubTitle = subtStream != null ? subtStream.Language : null;

                        media.MediaInfo = new MediaInfoData
                                              {
                                                  OverrideData = new MediaInfoData.MIData
                                                                     {
                                                                         AudioStreamCount = mb3Item.MediaStreams.Count(s => s.Type == MediaStreamType.Audio),
                                                                         AudioBitRate = audStream != null ? audStream.BitRate ?? 0 : 0,
                                                                         AudioChannelCount = audStream != null ? TranslateAudioChannels(audStream.Channels ?? 0) : "",
                                                                         AudioFormat = audStream != null ? audStream.Codec == "dca" ? audStream.Profile : audStream.Codec : "",
                                                                         VideoBitRate = vidStream != null ? vidStream.BitRate ?? 0 : 0,
                                                                         VideoCodec = vidStream != null ? vidStream.Codec : "",
                                                                         VideoFPS = vidStream != null ? vidStream.AverageFrameRate.ToString() : "",
                                                                         Width = vidStream != null ? vidStream.Width ?? 0 : 0,
                                                                         Height = vidStream != null ? vidStream.Height ?? 0 : 0,
                                                                         Subtitles = subtStream != null ? subtStream.Language : "",
                                                                         RunTime = runTimeTicks != null ? Convert.ToInt32(runTimeTicks / TimeSpan.TicksPerMinute) : 0
                                                                     }

                                              };
                    }
                    if (mb3Item.UserData != null)
                    {
                        media.PlaybackStatus = PlaybackStatusFactory.Instance.Create(media.Id);
                        media.PlaybackStatus.PositionTicks = mb3Item.UserData.PlaybackPositionTicks;
                        media.PlaybackStatus.PlayCount = mb3Item.UserData.PlayCount;
                        media.PlaybackStatus.WasPlayed = mb3Item.UserData.Played;
                        media.PlaybackStatus.LastPlayed = (mb3Item.UserData.LastPlayedDate ?? DateTime.MinValue).ToLocalTime();
                    }
                }

                var show = item as IShow;
                if (show != null)
                {
                    show.MpaaRating = mb3Item.OfficialRating;
                    show.ImdbRating = mb3Item.CommunityRating;
                    show.RunningTime =  runTimeTicks != null ? (int?)Convert.ToInt32(runTimeTicks/TimeSpan.TicksPerMinute) : null;
                    show.ProductionYear = mb3Item.ProductionYear;

                    if (mb3Item.Genres != null)
                    {
                        show.Genres = new List<string>(mb3Item.Genres);
                    }

                    if (mb3Item.People != null)
                    {
                        show.Actors = new List<Actor>( mb3Item.People.Where(p => p.Type == PersonType.Actor || p.Type == PersonType.GuestStar).Select(a => new Actor {Name = a.Name, Role = a.Role ?? (a.Type == PersonType.GuestStar ? "Guest Star" : "")}));
                        show.Directors = new List<string>(mb3Item.People.Where(p => p.Type == PersonType.Director).Select(a => a.Name));
                    }

                    if (mb3Item.Studios != null)
                    {
                        show.Studios = new List<string>(mb3Item.Studios.Select(s => s.Name));
                        foreach (var studio in mb3Item.Studios.Where(s => s != null)) Studio.AddToCache(studio);
                    }
                }

                var episode = item as Episode;
                if (episode != null)
                {
                    episode.SortName = episode.EpisodeNumber = (mb3Item.IndexNumber ?? 0).ToString("000");
                    episode.Name = mb3Item.IndexNumber != null && mb3Item.IndexNumber > 0 ? (mb3Item.IndexNumber.ToString() + " - " + episode.Name) : episode.Name; 
                    episode.SeasonNumber = mb3Item.ParentIndexNumber != null ? mb3Item.ParentIndexNumber.Value.ToString("#00") : null;
                    episode.SeriesId = mb3Item.SeriesId;
                    episode.FirstAired = mb3Item.PremiereDate != null ? mb3Item.PremiereDate.Value.ToString("ddd d MMM, yyyy") : null;
                }

                var series = item as Series;
                if (series != null)
                {
                    series.Status = mb3Item.Status.ToString();
                    series.AirTime = mb3Item.AirTime;
                    series.AirDay = mb3Item.AirDays != null ? mb3Item.AirDays.FirstOrDefault().ToString() : null;
                }

                var season = item as Season;
                if (season != null)
                {
                    season.SeasonNumber = (mb3Item.IndexNumber ?? 0).ToString("000");
                }

            }
            else
            {
                Logger.ReportWarning("Ignoring invalid item " + itemType + ".  Would not instantiate in current environment.");
            }

            // Finally, any custom values
            item.FillCustomValues(mb3Item);

            return item;
        }

        protected bool IsRippedMedia(VideoType type)
        {
            return type == VideoType.BluRay || type == VideoType.Dvd || type == VideoType.Iso || type == VideoType.HdDvd;
        }

        protected string TranslateAudioChannels(int totalChannels)
        {
            switch (totalChannels)
            {
                case 5:
                    return "6";
                case 6:
                    return "6";
                case 7:
                    return "8";
                default:
                    return totalChannels.ToString();
            }
        }

        public void SaveChildren(Guid ownerName, IEnumerable<Guid> children)
        {
            //throw new NotImplementedException();
        }

        public IEnumerable<BaseItem> RetrieveChildren(string id)
        {
            return RetrieveChildren(id, null);
        }

        public IEnumerable<BaseItem> RetrieveChildren(string id, string indexBy)
        {
            if (id == Guid.Empty.ToString() || string.IsNullOrEmpty(id)) return null;  //some dummy items have blank ids

            var dtos = Kernel.ApiClient.GetItems(new ItemQuery
                                                     {
                                                         UserId = Kernel.CurrentUser.Id.ToString(),
                                                         ParentId = id,
                                                         IndexBy = indexBy,
                                                         Fields = new[] {ItemFields.Overview, ItemFields.Path, ItemFields.ParentId, ItemFields.DisplayPreferencesId, 
                                                            ItemFields.UserData, ItemFields.DateCreated, ItemFields.IndexOptions, ItemFields.ItemCounts, ItemFields.OriginalRunTimeTicks, 
                                                            ItemFields.MediaStreams, ItemFields.DisplayMediaType, ItemFields.SortName, ItemFields.SeriesInfo, ItemFields.Taglines,  }
                                                     });

            return dtos == null ? null : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
        }

        public static ItemFields[] StandardFields = new[]
                                                        {
                                                            ItemFields.Overview, ItemFields.Genres, ItemFields.People, ItemFields.Studios, ItemFields.OriginalRunTimeTicks, 
                                                            ItemFields.Path, ItemFields.DisplayPreferencesId, ItemFields.UserData, ItemFields.DateCreated, ItemFields.Taglines, 
                                                            ItemFields.MediaStreams, ItemFields.SeriesInfo, ItemFields.ParentId, ItemFields.ItemCounts, 
                                                        };

        public IEnumerable<BaseItem> RetrieveItems(ItemQuery query)
        {
            var dtos = Kernel.ApiClient.GetItems(query);

            return dtos == null ? new BaseItem[] {} : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
            
        }

        public IEnumerable<BaseItem> RetrieveGenres(ItemQuery query)
        {
            var dtos = Kernel.ApiClient.GetGenres(query);

            return dtos == null ? new BaseItem[] {} : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
            
        }

        public IEnumerable<BaseItem> RetrieveMusicGenres(ItemQuery query)
        {
            var dtos = Kernel.ApiClient.GetMusicGenres(query);

            return dtos == null ? new BaseItem[] {} : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
            
        }

        public IEnumerable<BaseItem> RetrieveSimilarItems(SimilarItemsQuery query, string type)
        {
            var dtos = Kernel.ApiClient.GetSimilarItems(query, type);

            return dtos == null ? new BaseItem[] {} : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
            
        }

        public IEnumerable<BaseItem> RetrieveSpecificItems(string[] ids)
        {
            return RetrieveItems(new ItemQuery
                                     {
                                         UserId = Kernel.CurrentUser.Id.ToString(),
                                         Ids = ids,
                                         Fields = StandardFields
                                     });
        }

        public IList<Index> RetrieveIndex(Folder folder, string property, Func<string, BaseItem> constructor)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> RetrieveChildList(Guid id)
        {
            throw new NotImplementedException();
        }

        public List<BaseItem> RetrieveSubIndex(string childTable, string property, object value)
        {
            throw new NotImplementedException();
        }

        public bool BackupDatabase()
        {
            throw new NotImplementedException();
        }

        public PlaybackStatus RetrievePlayState(Guid id)
        {
            var mb3Item = Kernel.ApiClient.GetItem(id.ToString());

            var pb = PlaybackStatusFactory.Instance.Create(id);
            if (mb3Item != null && mb3Item.UserData != null)
            {
                pb.PositionTicks = mb3Item.UserData.PlaybackPositionTicks;
                pb.PlayCount = mb3Item.UserData.PlayCount;
                pb.WasPlayed = mb3Item.UserData.Played;
                pb.LastPlayed = (mb3Item.UserData.LastPlayedDate ?? DateTime.MinValue).ToLocalTime();
                Debugger.Break();
            }
            return pb;
        }

        public DisplayPreferences RetrieveDisplayPreferences(DisplayPreferences dp)
        {
            throw new NotImplementedException();
        }

        public ThumbSize RetrieveThumbSize(Guid id)
        {
            throw new NotImplementedException();
        }

        public void MigratePlayState(ItemRepository repo)
        {
            throw new NotImplementedException();
        }

        public void MigrateDisplayPrefs(ItemRepository repo)
        {
            throw new NotImplementedException();
        }

        public void MigrateItems()
        {
            throw new NotImplementedException();
        }

        public void SavePlayState(PlaybackStatus playState)
        {
            Kernel.ApiClient.ReportPlaybackProgress(playState.Id.ToString(), Kernel.CurrentUser.Id, playState.PositionTicks);
        }

        public void SaveDisplayPreferences(string prefsId, Model.Entities.DisplayPreferences prefs)
        {
            Kernel.ApiClient.UpdateDisplayPreferences(prefsId, prefs);
        }

        public void SaveDisplayPreferences(DisplayPreferences prefs)
        {
            throw new NotImplementedException();
        }

        public void ShutdownDatabase()
        {
            //throw new NotImplementedException();
        }

        public int ClearCache(string objType)
        {
            //throw new NotImplementedException();
            return 0;
        }

        public bool ClearEntireCache()
        {
            return false;
        }
    }
}
