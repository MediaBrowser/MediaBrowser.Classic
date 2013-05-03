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
                                                                     {"Trailer", typeof (Movie)},
                                                                     {"Series", typeof (Series)},
                                                                     {"Season", typeof (Season)},
                                                                     {"Episode", typeof (Episode)},
                                                                     {"Video", typeof (Movie)},
                                                                     {"BoxSet", typeof (BoxSet)},
                                                                     {"Person", typeof (Person)},
                                                                     {"Genre", typeof (Genre)},
                                                                     {"IndexFolder", typeof(IndexFolder)},
                                                                     {"MusicAlbum", typeof(MusicAlbum)},
                                                                     {"MusicArtist", typeof(MusicArtist)},
                                                                     {"Audio", typeof(Song)},
                                                                     {"AggregateFolder", typeof (AggregateFolder)},
                                                                     {"CollectionFolder", typeof (Folder)},
                                                                     {"YoutubeCollectionFolder", typeof (Folder)},
                                                                     {"YoutubeVideo", typeof (Movie)},
                                                                     {"TrailerCollectionFolder", typeof (Folder)},
                                                                 };

            public static object Translate(BaseItemDto mb3Item, SqliteItemRepository.SQLInfo.ColDef col)
            {
                object data = null;
                try
                {
                    data = typeof (BaseItemDto).GetProperty(col.ColName).GetValue(mb3Item, BindingFlags.Public | BindingFlags.Instance, null, null, null);
                }
                catch (Exception e)
                {
                    try
                    {
                        data = typeof (BaseItemDto).GetProperty(Char.ToUpperInvariant(col.ColName[0]) + col.ColName.Substring(1)).GetValue(mb3Item, BindingFlags.Public | BindingFlags.Instance, null, null, null);
                    }
                    catch (Exception ex)
                    {
                        Logger.ReportException("Error reading data for col: " + col.ColName + " type is: " + col.ColType.Name, ex);
                        return null;
                    }
                }
                if (data is DBNull || data == null) return null;
                var typ = data.GetType();

                //Logger.ReportVerbose("Extracting: " + col.ColName + " Defined Type: "+col.ColType+"/"+col.NullableType + " Actual Type: "+typ.Name+" Value: "+data);

                if (typ == typeof(string))
                {
                    if (col.ColType == typeof(MediaType))
                        return Enum.Parse(typeof(MediaType), (string)data);
                    else
                        if (col.ColType == typeof(MediaBrowser.Library.Network.DownloadPolicy))
                            return Enum.Parse(typeof(MediaBrowser.Library.Network.DownloadPolicy), (string)data);
                        else
                            if (col.ColType == typeof(VideoFormat))
                                return Enum.Parse(typeof(VideoFormat), (string)data);
                            else
                                if (col.ColType == typeof(Guid))
                                {
                                    return new Guid((string)data);
                                }
                                else
                                    return data;
                }
                else if (typ == typeof(DateTime) || typ == typeof(Int32)) return data;
                else if (typ == typeof (Model.Entities.VideoFormat)) return data.ToString();
                else
                    if (typ == typeof(Int64))
                    {
                        if (col.ColType == typeof(DateTime))
                            return new DateTime((Int64)data);
                        else if (col.InternalType == typeof(int) || col.ColType == typeof(int))
                            return Convert.ToInt32(data);
                        else
                            return data;
                    }
                    else
                        if (typ == typeof(Double))
                        {
                            if (col.ColType == typeof(Single) || col.InternalType == typeof(Single))
                                return Convert.ToSingle(data);
                            else
                                return data;
                        }
                        else
                        {
                            var ms = new MemoryStream((byte[])data);
                            return Serializer.Deserialize<object>(ms);
                            //return JsonSerializer.DeserializeFromString((string)reader[col.ColName], col.ColType);
                        }


            }

            public static object Encode(SqliteItemRepository.SQLInfo.ColDef col, object data)
            {
                if (data == null) return null;

                //Logger.ReportVerbose("Encoding " + col.ColName + " as " + col.ColType.Name.ToLower());
                switch (col.ColType.Name.ToLower())
                {
                    case "guid":
                        return data.ToString();
                    case "string":
                        return data;

                    case "datetime":
                        return ((DateTime)data).Ticks;

                    case "mediatype":
                        return ((MediaType)data).ToString();

                    case "videoformat":
                        return ((VideoFormat)data).ToString();

                    case "downloadpolicy":
                        return ((MediaBrowser.Library.Network.DownloadPolicy)data).ToString();

                    case "int":
                    case "int16":
                    case "int32":
                    case "int64":
                    case "long":
                    case "double":
                    case "nullable`1":
                        return data;
                    default:
                        var ms = new MemoryStream();
                        Serializer.Serialize<object>(ms, data);
                        ms.Seek(0, 0);
                        return ms.ReadAllBytes();

                }
            }
        }
        private Dictionary<Type, SqliteItemRepository.SQLInfo> ClassInfo = new Dictionary<Type, SqliteItemRepository.SQLInfo>();

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
                item.Path = mb3Item.Path;
                item.DateCreated = (mb3Item.DateCreated ?? DateTime.MinValue).ToLocalTime();
                item.DisplayMediaType = mb3Item.DisplayMediaType;
                item.Overview = mb3Item.Overview;
                item.SortName = mb3Item.SortName;
                item.TagLine = mb3Item.Taglines != null && mb3Item.Taglines.Count > 0 ? mb3Item.Taglines[0] : null;
                item.UserData = mb3Item.UserData;

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
                        var url = Kernel.ApiClient.GetImageUrl(mb3Item.Id, new ImageOptions {ImageType = tag.Key, Tag = tag.Value});
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
                }

                var video = item as Video;
                if (video != null && video.Path != null)
                {
                    video.MediaType = MediaTypeResolver.DetermineType(video.Path);
                    video.ContainsTrailers = mb3Item.HasTrailer;

                    if (mb3Item.MediaStreams != null)
                    {
                        var vidStream = mb3Item.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                        var audStream = mb3Item.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Audio);
                        var subtStream = mb3Item.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Subtitle);
                        video.MediaStreams = mb3Item.MediaStreams;

                        video.MediaInfo = new MediaInfoData
                                              {
                                                  OverrideData = new MediaInfoData.MIData
                                                                     {
                                                                         AudioStreamCount = mb3Item.MediaStreams.Count(s => s.Type == MediaStreamType.Audio),
                                                                         AudioBitRate = audStream != null ? audStream.BitRate ?? 0 : 0,
                                                                         AudioChannelCount = audStream != null ? TranslateAudioChannels(audStream.Channels ?? 0) : "",
                                                                         AudioFormat = audStream != null ? audStream.Codec : "",
                                                                         VideoBitRate = vidStream != null ? vidStream.BitRate ?? 0 : 0,
                                                                         VideoCodec = vidStream != null ? vidStream.Codec : "",
                                                                         VideoFPS = vidStream != null ? vidStream.AverageFrameRate.ToString() : "",
                                                                         Width = vidStream != null ? vidStream.Width ?? 0 : 0,
                                                                         Height = vidStream != null ? vidStream.Height ?? 0 : 0,
                                                                         Subtitles = subtStream != null ? subtStream.Language : ""
                                                                     }

                                              };
                    }
                    if (mb3Item.UserData != null)
                    {
                        video.PlaybackStatus = PlaybackStatusFactory.Instance.Create(video.Id);
                        video.PlaybackStatus.PositionTicks = mb3Item.UserData.PlaybackPositionTicks;
                        video.PlaybackStatus.PlayCount = mb3Item.UserData.PlayCount;
                        video.PlaybackStatus.WasPlayed = mb3Item.UserData.Played;
                        video.PlaybackStatus.LastPlayed = (mb3Item.UserData.LastPlayedDate ?? DateTime.MinValue).ToLocalTime();
                    }
                }

                var show = item as IShow;
                if (show != null)
                {
                    show.MpaaRating = mb3Item.OfficialRating;
                    show.ImdbRating = mb3Item.CommunityRating;
                    show.RunningTime = Convert.ToInt32(mb3Item.RunTimeTicks/600000000);
                    show.ProductionYear = mb3Item.ProductionYear;

                    if (mb3Item.Genres != null)
                    {
                    show.Genres = new List<string>(mb3Item.Genres);
                    }

                    if (mb3Item.People != null)
                    {
                        show.Actors = new List<Actor>( mb3Item.People.Where(p => p.Type == PersonType.Actor).Select(a => new Actor {Name = a.Name, Role = a.Role}));
                        show.Directors = new List<string>(mb3Item.People.Where(p => p.Type == PersonType.Director).Select(a => a.Name));
                    }

                    if (mb3Item.Studios != null)
                    {
                        show.Studios = new List<string>(mb3Item.Studios);
                    }
                }

                var episode = item as Episode;
                if (episode != null)
                {
                    episode.EpisodeNumber = (mb3Item.IndexNumber ?? 0).ToString("000");
                    episode.SeriesId = mb3Item.SeriesId;
                    episode.FirstAired = mb3Item.PremiereDate.ToString();
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
            return item;
        }

        protected string TranslateAudioChannels(int totalChannels)
        {
            switch (totalChannels)
            {
                case 5:
                    return "5.1";
                case 6:
                    return "6.1";
                case 7:
                    return "7.1";
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
                                                         Fields = new[] {ItemFields.Overview, ItemFields.Path, ItemFields.DisplayPreferencesId, 
                                                            ItemFields.UserData, ItemFields.DateCreated, ItemFields.IndexOptions, 
                                                            ItemFields.MediaStreams, ItemFields.DisplayMediaType, ItemFields.SortName,  }
                                                     });

            return dtos == null ? null : dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
        }

        public static ItemFields[] StandardFields = new[]
                                                        {
                                                            ItemFields.Overview, ItemFields.Genres, ItemFields.People, ItemFields.Studios,
                                                            ItemFields.Path, ItemFields.DisplayPreferencesId, ItemFields.UserData, ItemFields.DateCreated,
                                                            ItemFields.MediaStreams, ItemFields.SeriesInfo
                                                        };

        public IEnumerable<BaseItem> RetrieveItems(ItemQuery query)
        {
            var dtos = Kernel.ApiClient.GetItems(query);

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
