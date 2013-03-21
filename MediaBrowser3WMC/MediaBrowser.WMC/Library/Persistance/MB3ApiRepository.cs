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
using MediaBrowser.Library.Logging;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Persistance
{
    public class MB3ApiRepository: IItemRepository
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
            return (AggregateFolder)GetItem(root, "AggregateFolder");
            
        }

        public BaseItem RetrieveItem(Guid name)
        {
            return null;
        }

        protected BaseItem GetItem(BaseItemDto mb3Item, string itemType)
        {
            BaseItem item = null;
            try
            {
                Type typ;
                if (Mb3Translator.TypeMap.TryGetValue(itemType, out typ))
                {
                    item = (BaseItem)Activator.CreateInstance(typ);
                }
                else
                {
                    item = Serializer.Instantiate<BaseItem>(itemType);
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error trying to create instance of type: " + itemType, e);
                return null;
            }
            if (item != null)
            {
                //SqliteItemRepository.SQLInfo itemSQL;
                //lock (ClassInfo)
                //{
                //    if (!ClassInfo.TryGetValue(item.GetType(), out itemSQL))
                //    {
                //        itemSQL = new SqliteItemRepository.SQLInfo(item);
                //        ClassInfo.Add(item.GetType(), itemSQL);
                //    }
                //}
                //foreach (var col in itemSQL.AtomicColumns)
                //{
                //    var data = Mb3Translator.Translate(mb3Item, col);
                //    if (data != null)
                //        if (col.MemberType == MemberTypes.Property)
                //            col.PropertyInfo.SetValue(item, data, null);
                //        else
                //            col.FieldInfo.SetValue(item, data);

                //}

                item.Name = mb3Item.Name;
                item.Path = mb3Item.Path;
                item.DateCreated = mb3Item.DateCreated ?? DateTime.MinValue;
                item.DisplayMediaType = mb3Item.DisplayMediaType;
                item.Id = new Guid(mb3Item.Id);
                item.Overview = mb3Item.Overview;
                item.SortName = mb3Item.SortName;
                item.TagLine = mb3Item.Taglines != null && mb3Item.Taglines.Count > 0 ? mb3Item.Taglines[0] : null;

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
                    // Fill in display prefs
                    folder.DisplayPreferences = mb3Item.DisplayPreferences;
                }

                var video = item as Video;
                if (video != null && video.Path != null)
                {
                    video.MediaType = MediaTypeResolver.DetermineType(video.Path);
                    if (mb3Item.UserData != null)
                    {
                        video.PlaybackStatus = PlaybackStatusFactory.Instance.Create(video.Id);
                        video.PlaybackStatus.PositionTicks = mb3Item.UserData.PlaybackPositionTicks;
                        video.PlaybackStatus.PlayCount = mb3Item.UserData.PlayCount;
                        video.PlaybackStatus.WasPlayed = mb3Item.UserData.Played;
                        video.PlaybackStatus.LastPlayed = mb3Item.UserData.LastPlayedDate ?? DateTime.MinValue;
                    }
                }

                var show = item as IShow;
                if (show != null)
                {
                    show.MpaaRating = mb3Item.OfficialRating;
                    show.ImdbRating = mb3Item.CommunityRating;

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

                // and our list columns
                ////this is an optimization - we go get all the list values for this item in one statement
                //var listCmd = connection.CreateCommand();
                //listCmd.CommandText = "select property, value from list_items where guid = @guid and property != 'ActorName' order by property, sort_order";
                //listCmd.AddParam("@guid", item.Id);
                //string currentProperty = "";
                //System.Collections.IList list = null;
                //SqliteItemRepository.SQLInfo.ColDef column = new SqliteItemRepository.SQLInfo.ColDef();
                //using (var listReader = listCmd.ExecuteReader())
                //{
                //    while (listReader.Read())
                //    {
                //        string property = listReader.GetString(0);
                //        if (property != currentProperty)
                //        {
                //            //new column...
                //            if (list != null)
                //            {
                //                //fill in the last one
                //                if (column.MemberType == MemberTypes.Property)
                //                    column.PropertyInfo.SetValue(item, list, null);
                //                else
                //                    column.FieldInfo.SetValue(item, list);
                //            }
                //            currentProperty = property;
                //            column = itemSQL.Columns.Find(c => c.ColName == property);
                //            list = (System.Collections.IList)column.ColType.GetConstructor(new Type[] { }).Invoke(null);
                //            //Logger.ReportVerbose("Added list item '" + listReader[0] + "' to " + col.ColName);
                //        }
                //        try
                //        {
                //            list.Add(SqliteItemRepository.SQLizer.Extract(listReader, new SqliteItemRepository.SQLInfo.ColDef() { ColName = "value", ColType = column.InternalType }));
                //        }
                //        catch (Exception e)
                //        {
                //            Logger.ReportException("Error adding item to list " + column.ColName + " on item " + item.Name, e);
                //        }
                //    }
                //    if (list != null)
                //    {
                //        //fill in the last one
                //        if (column.MemberType == MemberTypes.Property)
                //            column.PropertyInfo.SetValue(item, list, null);
                //        else
                //            column.FieldInfo.SetValue(item, list);
                //    }
                //}
            }
            else
            {
                Logger.ReportWarning("Ignoring invalid item " + itemType + ".  Would not instantiate in current environment.");
            }
            return item;
        }

        public void SaveChildren(Guid ownerName, IEnumerable<Guid> children)
        {
            //throw new NotImplementedException();
        }

        public IEnumerable<BaseItem> RetrieveChildren(Guid id)
        {
            var dtos = Kernel.ApiClient.GetItems(new ItemQuery
                                                     {
                                                         UserId = Kernel.CurrentUser.Id,
                                                         ParentId = id.ToString(),
                                                         Fields = new[] {ItemFields.Overview, ItemFields.Genres, ItemFields.People, ItemFields.Studios, ItemFields.Path, ItemFields.DisplayPreferences, ItemFields.UserData, ItemFields.DateCreated,  }
                                                     });
            if (dtos == null)
            {
                //if (Debugger.IsAttached) Debugger.Break();
                return null;
            }

            return dtos.Items.Select(dto => GetItem(dto, dto.Type)).Where(item => item != null);
        }

        public IList<Index> RetrieveIndex(Folder folder, string property, Func<string, BaseItem> constructor)
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
                pb.LastPlayed = mb3Item.UserData.LastPlayedDate ?? DateTime.MinValue;
                Debugger.Break();
            }
            return pb;
            //throw new NotImplementedException();
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
            //throw new NotImplementedException();
        }

        public void SaveDisplayPreferences(Guid itemId, DisplayPreferences prefs)
        {
            Kernel.ApiClient.UpdateDisplayPreferences(Kernel.CurrentUser.Id, itemId.ToString(), new Model.Entities.DisplayPreferences 
            {IndexBy = prefs.IndexBy, SortBy = prefs.SortOrder, RememberIndexing = Kernel.Instance.ConfigData.RememberIndexing, 
                ViewType = prefs.ViewType.Chosen.ToString(), UserId = Kernel.CurrentUser.Id});
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
