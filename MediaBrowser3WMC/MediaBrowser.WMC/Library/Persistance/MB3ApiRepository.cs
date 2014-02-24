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
                                                                     {"PhotoFolder", typeof (PhotoFolder)},
                                                                     {"Photo", typeof (Photo)},
                                                                     {"Movie", typeof (Movie)},
                                                                     {"Trailer", typeof (Movie)},
                                                                     {"AdultVideo", typeof (Movie)},
                                                                     {"Series", typeof (Series)},
                                                                     {"Season", typeof (Season)},
                                                                     {"Episode", typeof (Episode)},
                                                                     {"Video", typeof (Movie)},
                                                                     {"BoxSet", typeof (BoxSet)},
                                                                     {"Person", typeof (Person)},
                                                                     {"Genre", typeof (Genre)},
                                                                     {"Year", typeof(Year)},
                                                                     {"Studio", typeof(Studio)},
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
                                                                     {"VodCastVideo", typeof (VodCastVideo)},
                                                                     {"VodCastAudio", typeof (PodCastAudio)},
                                                                     {"VodCast", typeof (VodCast)},
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
            if (id == Guid.Empty) return null;

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
            var item = InstantiateItem(itemType, mb3Item);
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

        protected BaseItem InstantiateItem(string itemType, BaseItemDto mb3Item)
        {
            try
            {
                // Special handling for Apple trailers
                if (itemType.Equals("trailer", StringComparison.OrdinalIgnoreCase) && mb3Item.Path != null && mb3Item.Path.IndexOf("apple.com", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return new AppleTrailer();
                }

                Type typ;
                if (Mb3Translator.TypeMap.TryGetValue(itemType, out typ))
                {
                    return (BaseItem)Activator.CreateInstance(typ);
                }
                else
                {
                    if (itemType.EndsWith("Folder", StringComparison.OrdinalIgnoreCase))
                    {
                        return new Folder();
                    }
                    else
                    {
                        // Try media type
                        if (Mb3Translator.TypeMap.TryGetValue(mb3Item.MediaType, out typ))
                        {
                            return (BaseItem)Activator.CreateInstance(typ);
                        }

                        // fallback
                        return Serializer.Instantiate<BaseItem>(itemType);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error trying to create instance of type: " + itemType, e);
                return null;
            }
            
        }

        public string GetImageUrl(BaseItem item, ImageOptions options)
        {
            return item is MusicGenre ? Kernel.ApiClient.GetMusicGenreImageUrl(item.Name, options) :
                                    item is Genre ? Kernel.ApiClient.GetGenreImageUrl(item.Name, options) :
                                                Kernel.ApiClient.GetImageUrl(item.ApiId, options);
        }

        public BaseItem GetItem(BaseItemDto mb3Item, string itemType)
        {
            var item = InstantiateItem(itemType, mb3Item);

            if (item != null)
            {
                item.Name = mb3Item.Name;
                //Logger.ReportVerbose("Item {0} is {1}", item.Name, item.GetType().Name);
                item.Path = mb3Item.Path;
                item.DateCreated = (mb3Item.DateCreated ?? DateTime.MinValue).ToLocalTime();
                item.DisplayMediaType = mb3Item.DisplayMediaType;
                item.Overview = mb3Item.Overview;
                item.SortName = mb3Item.SortName;
                item.TagLine = mb3Item.Taglines != null && mb3Item.Taglines.Count > 0 ? mb3Item.Taglines[0] : null;
                item.UserData = mb3Item.UserData;
                item.PremierDate = mb3Item.PremiereDate ?? DateTime.MinValue;
                //Logger.ReportInfo("*********** Premier Date for {0} is {1}",item.Name,item.PremierDate);
                item.ApiParentId = mb3Item.ParentId;
                //if (item.ApiParentId == null) Logger.ReportVerbose("Parent Id is null for {0}",item.Name);
                item.LocationType = mb3Item.LocationType;
                // recursive media count
                item.ApiRecursiveItemCount = mb3Item.RecursiveItemCount;
                item.ApiItemCount = mb3Item.ChildCount;

                var runTimeTicks = mb3Item.RunTimeTicks;

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
                        switch (tag.Key)
                        {
                            case ImageType.Primary:
                                if (mb3Item.HasPrimaryImage)
                                item.PrimaryImagePath = GetImageUrl(item, new ImageOptions { ImageType = tag.Key, Tag = tag.Value, Quality = Kernel.Instance.CommonConfigData.JpgImageQuality, MaxWidth = Kernel.Instance.CommonConfigData.MaxPrimaryWidth, CropWhitespace = false });
                                break;

                            case ImageType.Logo:
                                if (mb3Item.HasLogo)
                                item.LogoImagePath = GetImageUrl(item, new ImageOptions { ImageType = tag.Key, Tag = tag.Value, Quality = Kernel.Instance.CommonConfigData.JpgImageQuality, MaxWidth = Kernel.Instance.CommonConfigData.MaxLogoWidth, CropWhitespace = false });
                                break;

                            case ImageType.Art:
                                if (mb3Item.HasArtImage)
                                item.ArtImagePath = GetImageUrl(item, new ImageOptions { ImageType = tag.Key, Tag = tag.Value, Quality = Kernel.Instance.CommonConfigData.JpgImageQuality, MaxWidth = Kernel.Instance.CommonConfigData.MaxArtWidth, CropWhitespace = false });
                                break;

                            case ImageType.Banner:
                                if (mb3Item.HasBanner)
                                item.BannerImagePath = GetImageUrl(item, new ImageOptions { ImageType = tag.Key, Tag = tag.Value, Quality = Kernel.Instance.CommonConfigData.JpgImageQuality, MaxWidth = Kernel.Instance.CommonConfigData.MaxBannerWidth, CropWhitespace = false });
                                break;

                            case ImageType.Thumb:
                                if (mb3Item.HasThumb)
                                item.ThumbnailImagePath = GetImageUrl(item, new ImageOptions { ImageType = tag.Key, Tag = tag.Value, Quality = Kernel.Instance.CommonConfigData.JpgImageQuality, MaxWidth = Kernel.Instance.CommonConfigData.MaxThumbWidth, CropWhitespace = false });
                                break;

                            case ImageType.Disc:
                                if (mb3Item.HasDiscImage)
                                item.DiscImagePath = GetImageUrl(item, new ImageOptions { ImageType = tag.Key, Tag = tag.Value, Quality = Kernel.Instance.CommonConfigData.JpgImageQuality, MaxWidth = Kernel.Instance.CommonConfigData.MaxDiscWidth, CropWhitespace = false });
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
                        item.BackdropImagePaths.Add(Kernel.ApiClient.GetImageUrl(mb3Item.Id, new ImageOptions { ImageType = ImageType.Backdrop, Quality = Kernel.Instance.CommonConfigData.JpgImageQuality, MaxWidth = Kernel.Instance.CommonConfigData.MaxBackgroundWidth, Tag = bd, ImageIndex = ndx}));
                        ndx++;
                    }
                }

                var folder = item as Folder;
                if (folder != null)
                {
                    // Collection Type
                    folder.CollectionType = mb3Item.CollectionType;
                    // Fill in display prefs and indexby options
                    folder.DisplayPreferencesId = mb3Item.DisplayPreferencesId;
                    folder.IndexByOptions = mb3Item.IndexOptions != null ? mb3Item.IndexOptions.ToDictionary(o => o) : 
                        new Dictionary<string, string> {{LocalizedStrings.Instance.GetString("NoneDispPref"), ""}};

                    // cumulative runtime
                    if (mb3Item.CumulativeRunTimeTicks != null)
                    {
                        folder.RunTime =  (int)(mb3Item.CumulativeRunTimeTicks/TimeSpan.TicksPerMinute);
                    }

                    // unwatched count
                    if (mb3Item.RecursiveUnplayedItemCount != null)
                    {
                        folder.UnwatchedCount = mb3Item.RecursiveUnplayedItemCount.Value;
                    }

                    // don't replace this with ?? until after the server implementing this has been released...
                    if (mb3Item.RecursiveUnplayedItemCount != null) folder.UnwatchedCount = mb3Item.RecursiveUnplayedItemCount.Value;
                }

                var video = item as Video;
                if (video != null && video.Path != null)
                {
                    video.ContainsTrailers = mb3Item.LocalTrailerCount > 0;
                    if (mb3Item.Video3DFormat != null)
                    {
                        video.VideoFormat = mb3Item.Video3DFormat == Video3DFormat.FullSideBySide || mb3Item.Video3DFormat == Video3DFormat.HalfSideBySide ? "Sbs3D" : "Digital3D";
                    }
                    else
                    {
                        video.VideoFormat = "Standard";
                    }

                    // Chapters
                    if (mb3Item.Chapters != null)
                    {
                        var ndx = 0;
                        video.Chapters = mb3Item.Chapters.Select(c => new Chapter {ApiParentId = mb3Item.Id, PositionTicks = c.StartPositionTicks, Name = c.Name, PrimaryImagePath = c.HasImage ? GetImageUrl(video, new ImageOptions {Tag = c.ImageTag, ImageType = ImageType.Chapter, ImageIndex = ndx++}) : null}).ToList();
                    }
                }

                var media = item as Media;
                if (media != null)
                {
                    if (mb3Item.MediaType == Model.Entities.MediaType.Video)
                    {
                        if (mb3Item.VideoType == VideoType.VideoFile && media.Path != null)
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
                        var subtStreams = mb3Item.MediaStreams.Where(s => s.Type == MediaStreamType.Subtitle && !string.IsNullOrEmpty(s.Language)).Select(s => s.Language).ToArray();
                        media.MediaStreams = mb3Item.MediaStreams;
                        media.AspectRatio = !string.IsNullOrEmpty(mb3Item.AspectRatio) ? mb3Item.AspectRatio : null;
                        media.SubTitle = subtStreams.Any() ? string.Join(", ", subtStreams) : null;

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
                                                                         Subtitles = subtStreams.Any() ? string.Join(", ", subtStreams) : null,
                                                                         RunTime = runTimeTicks != null ? ConvertToTicksToMinutes(runTimeTicks) : 0
                                                                     }

                                              };
                    }
                    if (mb3Item.UserData != null)
                    {
                        media.PlaybackStatus = PlaybackStatusFactory.Instance.Create(media.Id);
                        media.PlaybackStatus.PositionTicks = mb3Item.UserData.PlaybackPositionTicks;
                        media.PlaybackStatus.PlayCount = mb3Item.UserData.PlayCount;
                        media.PlaybackStatus.WasPlayed = mb3Item.UserData.Played || mb3Item.LocationType == LocationType.Virtual;
                        media.PlaybackStatus.LastPlayed = (mb3Item.UserData.LastPlayedDate ?? DateTime.MinValue).ToLocalTime();
                    }
                }

                var show = item as IShow;
                if (show != null)
                {
                    show.MpaaRating = mb3Item.OfficialRating;
                    show.ImdbRating = mb3Item.CommunityRating;
                    show.RunningTime =  runTimeTicks != null ? (int?)ConvertToTicksToMinutes(runTimeTicks) : null;
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
                    var indexDisplay = mb3Item.IndexNumber != null && mb3Item.IndexNumber > 0 ? mb3Item.IndexNumber + (mb3Item.IndexNumberEnd != null ? "-" + mb3Item.IndexNumberEnd : "") + " - " : "";
                    episode.Name = indexDisplay != "" ? indexDisplay + episode.Name : episode.Name;
                    episode.EpisodeNumber = mb3Item.IndexNumber != null ? mb3Item.IndexNumber.Value.ToString("#00") : null;
                    episode.SeasonNumber = mb3Item.ParentIndexNumber != null ? mb3Item.ParentIndexNumber.Value.ToString("#00") : null;
                    episode.SeriesId = mb3Item.SeriesId;
                    episode.SeasonId = mb3Item.SeasonId;
                    episode.FirstAired = mb3Item.PremiereDate != null ? mb3Item.PremiereDate.Value.ToLocalTime().ToString("ddd d MMM, yyyy") : null;
                    if (mb3Item.AirsAfterSeasonNumber != null)
                    {
                        episode.SortName = mb3Item.AirsAfterSeasonNumber.Value.ToString("000") + "-999999" + mb3Item.SortName;
                    } 
                    else 
                        if (mb3Item.AirsBeforeSeasonNumber != null && mb3Item.AirsBeforeEpisodeNumber != null)
                        {
                            episode.SortName = mb3Item.AirsBeforeSeasonNumber.Value.ToString("000") + "-" + ((int)(mb3Item.AirsBeforeEpisodeNumber - 1)).ToString("0000") + ".5" + mb3Item.SortName;
                        }
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

                // Finally, any custom values
                item.FillCustomValues(mb3Item);
            }
            else
            {
                Logger.ReportWarning("Ignoring invalid item " + itemType + ".  Would not instantiate in current environment.");
            }


            return item;
        }

        protected int ConvertToTicksToMinutes(long? ticks)
        {
            if (ticks == null) return 0;

            try
            {
                return Convert.ToInt32(ticks/TimeSpan.TicksPerMinute);
            }
            catch (OverflowException e)
            {
                Logger.ReportException("Error converting tick value: {0}", e, ticks);
                return 0;
            }
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


        public IEnumerable<BaseItem> RetrieveChildren(string id, string indexBy = null, ItemFilter[] filters = null, bool? isPlayed = null)
        {
            if (id == Guid.Empty.ToString() || string.IsNullOrEmpty(id)) return new List<BaseItem>();  //some dummy items have blank ids

            var dtos = Kernel.ApiClient.GetItems(new ItemQuery
                                                     {
                                                         UserId = Kernel.CurrentUser.Id.ToString(),
                                                         ParentId = id,
                                                         Filters = filters,
                                                         IsPlayed = isPlayed,
                                                         Fields = new[] {ItemFields.Overview, ItemFields.Path, ItemFields.ParentId, ItemFields.DisplayPreferencesId, 
                                                            ItemFields.DateCreated, ItemFields.IndexOptions, 
                                                            ItemFields.MediaStreams, ItemFields.SortName, ItemFields.Taglines,  }
                                                     });

            return dtos == null ? new List<BaseItem>() : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
        }

        public static ItemFields[] StandardFields = new[]
                                                        {
                                                            ItemFields.Overview, ItemFields.IndexOptions, ItemFields.SortName, 
                                                            ItemFields.Path, ItemFields.DisplayPreferencesId, ItemFields.DateCreated, ItemFields.Taglines, 
                                                            ItemFields.MediaStreams, ItemFields.ParentId, 
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

        public IEnumerable<BaseItem> RetrievePersons(PersonsQuery query)
        {
            var dtos = Kernel.ApiClient.GetPersons(query);

            return dtos == null ? new BaseItem[] {} : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
            
        }

        public IEnumerable<BaseItem> RetrieveMusicGenres(ItemQuery query)
        {
            var dtos = Kernel.ApiClient.GetMusicGenres(query);

            return dtos == null ? new BaseItem[] {} : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
            
        }

        public IEnumerable<BaseItem> RetrieveIbnItems(string ibnName, ItemsByNameQuery query)
        {
            var dtos = Kernel.ApiClient.GetIbnItems(ibnName, query);

            return dtos == null ? new BaseItem[] {} : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
            
        }

        public IEnumerable<BaseItem> RetrieveMusicArtists(ItemsByNameQuery query)
        {
            var dtos = Kernel.ApiClient.GetArtists(query);

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

        public IEnumerable<Media> RetrieveIntros(string id)
        {
            var dtos = Kernel.ApiClient.GetIntros(id, Kernel.CurrentUser.Id);
            return dtos == null ? new Media[] { } : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null).Cast<Media>();
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
