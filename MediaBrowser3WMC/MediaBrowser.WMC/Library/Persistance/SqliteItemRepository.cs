using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Entities;
using System.Data.SQLite;
using MediaBrowser.Library.Configuration;
using System.IO;
using MediaBrowser.Library.Logging;
using System.Reflection;
using System.Threading;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Extensions;


namespace MediaBrowser.Library.Persistance {


    public class SqliteItemRepository : SQLiteRepository, IItemRepository {

        const string CURRENT_SCHEMA_VERSION = "2.5.0.0";

        private Dictionary<Type, SQLInfo> ItemSQL = new Dictionary<Type, SQLInfo>();

        private string dbFileName;

        protected static class SQLizer
        {
            public static void Init(string path)
            {
                //if (serviceStackAssembly == null)
                //{
                //    serviceStackAssembly = System.Reflection.Assembly.LoadFile(path);
                //    AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(ServiceStackResolver);
                //}
            }

            public static object Extract(SQLiteDataReader reader, SQLInfo.ColDef col) 
            {
                object data = null;
                try
                {
                    data = reader[col.ColName];
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error reading data for col: " + col.ColName + " type is: " + col.ColType.Name, e);
                    return null;
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
                } else
                    if (typ == typeof(Int64)) {
                        if (col.ColType == typeof(DateTime))
                            return new DateTime((Int64)data);
                        else if (col.InternalType == typeof(int) || col.ColType == typeof(int))
                            return Convert.ToInt32(data);
                        else
                            return data;
                    } else
                        if (typ == typeof(Double)) {
                            if (col.ColType == typeof(Single) || col.InternalType == typeof(Single))
                                return Convert.ToSingle(data);
                            else
                                return data;
                        } else
                        {
                            var ms = new MemoryStream((byte[])data);
                            return Serializer.Deserialize<object>(ms);
                            //return JsonSerializer.DeserializeFromString((string)reader[col.ColName], col.ColType);
                        }

                            
            }

            public static object Encode(SQLInfo.ColDef col, object data) 
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
                        Serializer.Serialize<object>(ms,data);
                        ms.Seek(0,0);
                        return ms.ReadAllBytes();

                }
            }
        }


        public class SQLInfo
        {

            public struct ColDef
            {
                public string ColName;
                public Type ColType;
                public Type InternalType;
                public bool ListType;
                public MemberTypes MemberType;
                public PropertyInfo PropertyInfo;
                public FieldInfo FieldInfo;

            }

            protected string ObjType;
            public List<ColDef> Columns = new List<ColDef>();

            public void FixUpSchema(SQLiteConnection connection)
            {
                //make sure all our columns are in the db
                var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(items)";
                List<string> dbCols = new List<string>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dbCols.Add(reader[1].ToString());
                    }
                }
                if (!dbCols.Contains("obj_type")) connection.Exec("Alter table items add column obj_type");
                foreach (var col in this.AtomicColumns)
                {
                    if (!dbCols.Contains(col.ColName))
                    {
                        Logger.ReportInfo("Discovered new attribute: " + col.ColName + " on object type: "+ ObjType+". Adding to schema.");
                        connection.Exec("Alter table items add column "+col.ColName);
                    }
                }
            }
            
            public SQLInfo(BaseItem item) {
                this.ObjType = item.GetType().FullName;
                foreach (var property in item.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.GetCustomAttributes(typeof(PersistAttribute), true).Length > 0)
                .Where(p => p.GetGetMethod(true) != null && p.GetSetMethod(true) != null)) {
                    Type internalType = null;
                    if (property.PropertyType.Name == "Nullable`1")
                    {
                        var fullName = property.PropertyType.FullName;
                        if (fullName.Contains("Int32"))
                            internalType = typeof(int);
                        else if (fullName.Contains("Int64"))
                            internalType = typeof(Int64);
                        else if (fullName.Contains("Single"))
                            internalType = typeof(Single);
                        else if (fullName.Contains("Double"))
                            internalType = typeof(Double);
                        else internalType = property.PropertyType;
                    }
                    bool listType = IsListType(property.PropertyType);
                    if (listType)
                    {
                        internalType = property.PropertyType.GetGenericArguments()[0];
                    }
                    Columns.Add(new ColDef() { ColName = property.Name, ColType = property.PropertyType, InternalType = internalType, ListType = listType, MemberType = property.MemberType, PropertyInfo = property });
                }
                //Properties report all inherited ones but fields do not so we must iterate up the object tree to get them all...
                var type = item.GetType();
                Columns.AddRange(GetFields(type));
                type = type.BaseType;
                while (type != typeof(object) && type != null)
                {
                    foreach (var field in GetFields(type)) //iterate explicitly to eliminate duplicate (overridden) fields
                    {
                        if (Columns.FindIndex(c => c.ColName == field.ColName) == -1)
                        {
                            Columns.Add(field);
                        }
                    }
                    type = type.BaseType;
                }

            }

            private IEnumerable<ColDef> GetFields(Type t) {
                foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.GetCustomAttributes(typeof(PersistAttribute), true).Length > 0)) {
                    Type internalType = null;
                    if (field.FieldType.Name == "Nullable`1")
                    {
                        var fullName = field.FieldType.FullName;
                        if (fullName.Contains("Int32"))
                            internalType = typeof(int);
                        else if (fullName.Contains("Int64"))
                            internalType = typeof(Int64);
                        else if (fullName.Contains("Single"))
                            internalType = typeof(Single);
                        else if (fullName.Contains("Double"))
                            internalType = typeof(Double);
                        else internalType = field.FieldType;
                    }
                    bool listType = IsListType(field.FieldType);
                    if (listType)
                    {
                        internalType = field.FieldType.GetGenericArguments()[0];
                    }
                    yield return new ColDef() {ColName = field.Name, ColType = field.FieldType, InternalType = internalType, ListType = IsListType(field.FieldType), MemberType = field.MemberType, FieldInfo = field};
                }
            }

            private List<ColDef> _atomicColumns;
            public List<ColDef> AtomicColumns
            {
                get
                {
                    if (_atomicColumns == null) {
                        _atomicColumns = this.Columns.Where(c => !c.ListType).ToList();
                    }
                    return _atomicColumns;
                }
            }

            public List<ColDef> ListColumns
            {
                get
                {
                    return this.Columns.Where(c => c.ListType).ToList();
                }
            }

            private bool IsListType(Type t)
            {
                //return (t.Name.StartsWith("List") || t.Name.StartsWith("IList") || t.Name.StartsWith("IEnum"));
                return t.GetInterface("ICollection`1") != null;
            }

            private string _select;
            public string SelectStmt
            {
                get
                {
                    if (_select == null)
                    {
                        var stmt = new StringBuilder();
                        stmt.Append("select guid, ");
                        foreach (var col in AtomicColumns)
                        {
                            stmt.Append(col.ColName + ", ");
                        }
                        stmt.Remove(stmt.Length-2, 2); //remove last comma
                        stmt.Append(" from item ");
                        _select = stmt.ToString();
                    }
                    return _select;
                }
            }

            private string _update;
            public string UpdateStmt
            {
                get
                {
                    if (_update == null)
                    {
                        var stmt = new StringBuilder();
                        stmt.Append("replace into items (guid, obj_type, ");
                        int numCols = 2;
                        foreach (var col in Columns.Where(p => !p.ListType))
                        {
                            stmt.Append(col.ColName + ", ");
                            numCols++;
                        }
                        stmt.Remove(stmt.Length-2, 2); //remove last comma
                        //now values clause
                        stmt.Append(") values(");
                        for (int i = 0; i < numCols; i++)
                        {
                            stmt.Append("@"+i+", ");
                        }
                        stmt.Remove(stmt.Length - 2, 2);
                        stmt.Append(")");
                        _update = stmt.ToString();
                    }
                    return _update;
                }
            }
        }

        public static SqliteItemRepository GetRepository(string dbPath, string sqlitePath) {
            if (sqliteAssembly == null) {
                sqliteAssembly = System.Reflection.Assembly.LoadFile(sqlitePath);
                AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(SqliteResolver);
            }

            //SQLizer.Init(Path.Combine(ApplicationPaths.AppConfigPath, "ServiceStack.Text.dll"));

            return new SqliteItemRepository(dbPath);

        }

        //cascade delete triggers
        protected string triggerSQL =
            @"CREATE TRIGGER if not exists delete_item
                AFTER DELETE
                ON items
                FOR EACH ROW
                BEGIN
                    DELETE FROM children WHERE children.guid = old.id;
                    DELETE FROM children WHERE children.child = old.id;
                    DELETE FROM list_items WHERE list_items.guid = old.id;
                END";

        // Display repo
        SQLiteDisplayRepository displayRepo;
        // Playstate repo
        FileBasedDictionary<PlaybackStatus> playbackStatus;

        private SqliteItemRepository(string dbPath) {

            Logger.ReportInfo("==========Using new SQL Repo========");
            dbFileName = dbPath;


            if (!ConnectToDB(dbPath)) throw new ApplicationException("CRITICAL ERROR - Unable to connect to database: " + dbPath + ".  Program cannot function.");

            displayRepo = new SQLiteDisplayRepository(Path.Combine(ApplicationPaths.AppUserSettingsPath, "display.db"));
            using (new MediaBrowser.Util.Profiler("Playstate initialization"))
            playbackStatus = new FileBasedDictionary<PlaybackStatus>(GetPath("playstate", ApplicationPaths.AppUserSettingsPath));

            //string playStateDBPath = Path.Combine(ApplicationPaths.AppUserSettingsPath, "playstate.db");

            string[] queries = {"create table if not exists provider_data (guid, full_name, data)",
                                "create unique index if not exists idx_provider on provider_data(guid, full_name)",
                                "create table if not exists items (guid primary key)",
                                "create index if not exists idx_items on items(guid)",
                                "create table if not exists children (guid, child)", 
                                "create unique index if not exists idx_children on children(guid, child)",
                                "create table if not exists list_items(guid, property, value, sort_order)",
                                "create index if not exists idx_list on list_items(guid, property)",
                                "create unique index if not exists idx_list_constraint on list_items(guid, property, value)",
                                "create table if not exists schema_version (table_name primary key, version)",
                                //triggers
                                triggerSQL,
                                //pragmas
                                "pragma temp_store = memory",
                               // @"create table display_prefs (guid primary key, view_type, show_labels, vertical_scroll 
                               //        sort_order, index_by, use_banner, thumb_constraint_width, thumb_constraint_height, use_coverflow, use_backdrop )" 
                                //,   "create table play_states (guid primary key, play_count, position_ticks, playlist_position, last_played)"
                               };

            RunQueries(queries);
            alive = true; // tell writer to keep going
            Async.Queue("Sqlite Writer", DelayedWriter);

        }

        public override void ShutdownDatabase()
        {
            //we need to shut down our display repo too...
            displayRepo.ShutdownDatabase();
            playbackStatus.Dispose();
            base.ShutdownDatabase();
        }

        public bool BackupDatabase()
        {
            bool success = true;
            try
            {
                connection.Close();
                File.Copy(dbFileName, dbFileName + ".bak", true);
            }
            catch (Exception e)
            {
                Logger.ReportException("Error attempting to backup db.", e);
                success = false;
            }
            finally
            {
                connection.Open();
            }
            return success;
        }


        private string GetPath(string type, string root) {
            string path = Path.Combine(root, type);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }



        public void MigrateItems()
        {
            var guids = new List<Guid>();
            var cmd = connection.CreateCommand();
            //test to see if it is an old repo (data won't exist in a new one)
            cmd.CommandText = "select data from items";
            try
            {
                using (var reader = cmd.ExecuteReader()) { reader.Read(); }
            }
            catch (Exception e)
            {
                Logger.ReportInfo("Brand new repo db.  Nothing to migrate. " + e.Message);
                return;
            }

            cmd.CommandText = "select guid from items";

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    guids.Add(reader.GetGuid(0));
                }
            }
            int cnt = 0;
            foreach (var id in guids)
            {
                var item = RetrieveItemOld(id);
                if (item != null)
                {
                    Logger.ReportInfo("Migrating " + item.Name);
                    SaveItem(item);
                    cnt++;
                    // the removal of in-progress handling means we don't need the following yet...
                    //if (item is Video && (item as Video).RunningTime != null)
                    //{
                    //    TimeSpan duration = TimeSpan.FromMinutes((item as Video).RunningTime.Value);
                    //    if (duration.Ticks > 0)
                    //    {
                    //        PlaybackStatus ps = RetrievePlayState(id);
                    //        decimal pctIn = Decimal.Divide(ps.PositionTicks, duration.Ticks) * 100;
                    //        if (pctIn > Kernel.Instance.ConfigData.MaxResumePct)
                    //        {
                    //            Logger.ReportInfo("Setting " + item.Name + " to 'Watched' based on last played position.");
                    //            ps.PositionTicks = 0;
                    //            SavePlayState(ps);
                    //        }
                    //    }
                    //}
                    Thread.Sleep(20); //allow the delayed writer to keep up...
                }
            }
            Logger.ReportInfo("==== Migrated " + cnt + " items.");
            SetSchemaVersion("items", CURRENT_SCHEMA_VERSION);
            Logger.ReportInfo("Item migration complete.");
        }


        public void MigratePlayState(ItemRepository itemRepo)
        {
            if (SchemaVersion("play_states") != CURRENT_SCHEMA_VERSION)
            { //haven't migrated
                Logger.ReportInfo("Attempting to migrate playstates to SQL");
                int cnt = 0;
                foreach (PlaybackStatus ps in itemRepo.AllPlayStates)
                {
                    //Logger.ReportVerbose("Saving playstate for " + ps.Id);
                    SavePlayState(ps);
                    cnt++;
                }
                Logger.ReportInfo("Successfully migrated " + cnt + " playstate items.");
                //move this so we don't do it again in a shared situation
                itemRepo.Backup("playstate");
                SetSchemaVersion("play_states", CURRENT_SCHEMA_VERSION);
            }
        }

        public void MigrateDisplayPrefs(ItemRepository itemRepo)
        {
            if (SchemaVersion("display_prefs") != CURRENT_SCHEMA_VERSION)
            {
                //need to migrate
                Logger.ReportInfo("Attempting to migrate display preferences to SQL");
                int num = displayRepo.MigrateDisplayPrefs();
                Logger.ReportInfo("Migrated " + num + " display preferences.");
                //move this so we don't do it again in a shared situation
                itemRepo.Backup("display");
                SetSchemaVersion("display_prefs", CURRENT_SCHEMA_VERSION);
            }
        }

        public PlaybackStatus RetrievePlayState(Guid id)
        {
            return playbackStatus[id];
        }

        //public PlaybackStatus RetrievePlayState(Guid id)
        //{
        //    var cmd = connection.CreateCommand();
        //    cmd.CommandText = "select guid, play_count, position_ticks, playlist_position, last_played from playstate_db.play_states where guid = @guid";
        //    cmd.AddParam("@guid", id);

        //    var state = new PlaybackStatus();
        //    using (var reader = cmd.ExecuteReader())
        //    {
        //        if (reader.Read())
        //        {
        //            state.Id = reader.GetGuid(0);
        //            state.PlayCount = reader.GetInt32(1);
        //            state.PositionTicks = reader.GetInt64(2);
        //            state.PlaylistPosition = reader.GetInt32(3);
        //            state.LastPlayed = reader.GetDateTime(4);
        //        }
        //        else state = null;
        //    }

        //    return state;
        //}

        public ThumbSize RetrieveThumbSize(Guid id)
        {
            return displayRepo.RetrieveThumbSize(id);
        }

        public void SavePlayState(PlaybackStatus playState)
        {
            playbackStatus[playState.Id] = playState;
        }

        //public void SavePlayState(PlaybackStatus playState)
        //{
        //    var cmd = connection.CreateCommand();
        //    cmd.CommandText = "replace into playstate_db.play_states(guid, play_count, position_ticks, playlist_position, last_played) values(@guid, @playCount, @positionTicks, @playlistPosition, @lastPlayed)";
        //    cmd.AddParam("@guid", playState.Id);
        //    cmd.AddParam("@playCount", playState.PlayCount);
        //    cmd.AddParam("@positionTicks", playState.PositionTicks);
        //    cmd.AddParam("@playlistPosition", playState.PlaylistPosition);
        //    cmd.AddParam("@lastPlayed", playState.LastPlayed);

        //    //Logger.ReportInfo("Saving Playstate: " + playState.Id + " / " + playState.PlayCount + " / " + playState.LastPlayed);
        //    QueueCommand(cmd);
        //}


        public void SaveChildren(Guid id, IEnumerable<Guid> children) {

            Guid[] childrenCopy;
            lock (children) {
                childrenCopy = children.ToArray();
            }

            var cmd = connection.CreateCommand();

            cmd.CommandText = "delete from children where guid = @guid";
            cmd.AddParam("@guid", id);

            QueueCommand(cmd);

            foreach (var guid in children) {
                cmd = connection.CreateCommand();
                cmd.AddParam("@guid", id);
                cmd.CommandText = "insert into children (guid, child) values (@guid, @child)";
                var childParam = cmd.Parameters.Add("@child", System.Data.DbType.Guid);

                childParam.Value = guid;
                QueueCommand(cmd);
            }
        }

        public IEnumerable<Guid> RetrieveChildrenOld(Guid id) {

            List<Guid> children = new List<Guid>();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "select child from children where guid = @guid";
            var guidParam = cmd.Parameters.Add("@guid", System.Data.DbType.Guid);
            guidParam.Value = id;

            using (var reader = cmd.ExecuteReader()) {
                while (reader.Read()) {
                    children.Add(reader.GetGuid(0));
                }
            }

            return children.Count == 0 ? null : children;
        }

        // used to track series objects during indexing so we can group episodes in their series
        private Dictionary<string, Dictionary<Guid, IContainer>> ContainerDict = new Dictionary<string, Dictionary<Guid, IContainer>>();

        public IList<Index> RetrieveIndex(Folder folder, string property, Func<string, BaseItem> constructor)
        {
            bool allowEpisodes = property == "Directors" || property == "ProductionYear";
            List<Index> children = new List<Index>();
            var cmd = connection.CreateCommand();

            //we'll build the unknown items now and leave them out of child table
            List<BaseItem> unknownItems = new List<BaseItem>();

            //create a temporary table of this folder's recursive children to use in the retrievals
            string tableName = "["+folder.Id.ToString().Replace("-","")+"_"+property+"]";
            if (connection.TableExists(tableName))
            {
                connection.Exec("delete from " + tableName);
            }
            else
            {
                connection.Exec("create temporary table if not exists " + tableName + "(child)");
            }

            cmd.CommandText = "Insert into "+tableName+" (child) values(@1)";
            var childParam = cmd.Parameters.Add("@1", DbType.Guid);

            var containerList = new Dictionary<Guid, IContainer>();  //initialize this for our index
            SQLInfo.ColDef col = new SQLInfo.ColDef();
            Type currentType = null;

            lock (connection) //can't use delayed writer here - need this table now...
            {
                var tran = connection.BeginTransaction();

                foreach (var child in folder.RecursiveChildren)
                {
                    if (child is IShow && !(child is Season) && ((allowEpisodes && !(child is Series)) || (!allowEpisodes && !(child is Episode))))
                    {
                        if (child is IGroupInIndex)
                        {
                            //add the series object
                            containerList[child.Id] = (child as IGroupInIndex).MainContainer;
                        }

                        //determine if property has any value
                        if (child.GetType() != currentType)
                        {
                            currentType = child.GetType();
                            col = ItemSQL[currentType].Columns.Find(c => c.ColName == property);
                        }
                        object data = null;
                        if (col.MemberType == MemberTypes.Property)
                        {
                            data = col.PropertyInfo.GetValue(child, null);
                        }
                        else if (col.MemberType == MemberTypes.Field)
                        {
                            data = col.FieldInfo.GetValue(child);
                        }
                        if (data != null)
                        {
                            //Logger.ReportVerbose("Adding child " + child.Name + " to temp table");
                            childParam.Value = child.Id;
                            cmd.ExecuteNonQuery();
                        }
                        else
                        {
                            //add to Unknown
                            AddItemToIndex("<Unknown>", unknownItems, containerList, child);
                        }
                    }
                }
                tran.Commit();
            }

            //fill in series
            ContainerDict[tableName] = containerList;
            //create our Unknown Index - if there were any
            if (unknownItems.Count > 0)
                children.Add(new Index(constructor("<Unknown>"), unknownItems));

            //now retrieve the values for the main indicies
            cmd = connection.CreateCommand(); //new command
            property = property == "Actors" ? "ActorName" : property; //re-map to our name entry

            if (col.ListType)
            {
                //need to get values from list table
                cmd.CommandText = "select distinct value from list_items where property = '" + property + "' and guid in (select child from " + tableName + ") order by value";
            }
            else
            {
                cmd.CommandText = "select distinct " + property + " from items where guid in (select child from " + tableName + ") order by "+property;
            }

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    //Logger.ReportVerbose("Creating index " + reader[0].ToString() + " on " + folder.Name);
                    object value = reader[0];
                    children.Add(new Index(constructor(value.ToString()), tableName, property, value));
                }
            }
            return children;
        }

        public List<BaseItem> RetrieveSubIndex(string childTable, string property, object value)
        {
            List<BaseItem> children = new List<BaseItem>();
            Dictionary<Guid, IContainer> containerList = ContainerDict[childTable];

            bool listColumn = false;
            try
            {
                connection.Exec("Select " + property + " from items");
            }
            catch (SQLiteException)
            {
                //must be a list column as it doesn't exist in items...
                listColumn = true;
            }

            var cmd = connection.CreateCommand();

            if (listColumn)
            {
                cmd.CommandText = "select * from items where guid in (select guid from list_items where property = '"+property+"' and value = @1 and guid in (select child from " + childTable + "))";
            }
            else
            {
                cmd.CommandText = "select * from items where " + property + " = @1 and guid in (select child from " + childTable + ")";
            }
            cmd.AddParam("@1", value);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    AddItemToIndex(value.ToString(), children, containerList, GetItem(reader, (string)reader["obj_type"]));
                }
            }
            return children;
        }

        private void AddItemToIndex(string indexName, List<BaseItem> index, Dictionary<Guid, IContainer> seriesList, BaseItem child)
        {
            if (child is IGroupInIndex)
            {
                //we want to group these by their main containers - find or create a head
                IContainer currentContainer = seriesList[child.Id];

                if (currentContainer == null)
                {
                    //couldn't find our container...
                    currentContainer = new Series()
                    {
                        Id = Guid.NewGuid(),
                        Name = "<Unknown>"
                    };
                }
                IndexFolder container = (IndexFolder)index.Find(i => i.Id == (indexName + currentContainer.Name).GetMD5());
                if (container == null)
                {
                    container = new IndexFolder()
                    {
                        Id = (indexName + currentContainer.Name).GetMD5(),
                        Name = currentContainer.Name,
                        Overview = currentContainer.Overview,
                        MpaaRating = currentContainer.MpaaRating,
                        Genres = currentContainer.Genres,
                        ImdbRating = currentContainer.ImdbRating,
                        Studios = currentContainer.Studios,
                        PrimaryImagePath = currentContainer.PrimaryImagePath,
                        SecondaryImagePath = currentContainer.SecondaryImagePath,
                        BannerImagePath = currentContainer.BannerImagePath,
                        BackdropImagePaths = currentContainer.BackdropImagePaths,
                        DisplayMediaType = currentContainer.DisplayMediaType,
                    };
                    index.Add(container);
                }
                container.AddChild(child);
            }
            else
            {
                index.Add(child);
            }
        }

        public IEnumerable<BaseItem> RetrieveChildren(Guid id) {

            List<BaseItem> children = new List<BaseItem>();

            //if (!Kernel.UseNewSQLRepo)
            //{
            //    var cached = RetrieveChildrenOld(id);
            //    if (cached != null)
            //    {
            //        foreach (var guid in cached)
            //        {
            //            var item = RetrieveItem(guid);
            //            if (item != null)
            //            {
            //                children.Add(item);
            //            }
            //        }
            //    }
            //}
            //else
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = "select * from items where guid in (select child from children where guid = @guid)";
                var guidParam = cmd.Parameters.Add("@guid", System.Data.DbType.Guid);
                guidParam.Value = id;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = GetItem(reader, (string)reader["obj_type"]);
                        if (item != null)
                        {
                            children.Add(item);
                        }
                    }
                }
            }
            return children.Count == 0 ? null : children;
        }

        public DisplayPreferences RetrieveDisplayPreferences(DisplayPreferences dp) {
                return displayRepo.RetrieveDisplayPreferences(dp);
        }


        public void SaveDisplayPreferences(DisplayPreferences prefs) {
                displayRepo.SaveDisplayPreferences(prefs);
        }

        public BaseItem RetrieveItemOld(Guid id)
        {
            BaseItem item = null;
            var cmd = connection.CreateCommand();
            cmd.CommandText = "select data from items where guid = @guid";
            cmd.AddParam("@guid", id);

            //Logger.ReportInfo("Retrieving old item " + id);
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    if (!(reader[0] is DBNull)) //during migration we could have some null entries here
                    {
                        var data = reader.GetBytes(0);
                        using (var stream = new MemoryStream(data))
                        {
                            try
                            {
                                item = Serializer.Deserialize<BaseItem>(stream);
                            }
                            catch (Exception e)
                            {
                                Logger.ReportException("Unable to deserialize object during legacy retrieval - probably old data (" + id + ").", e);
                            }
                        }
                    }
                }
            }
            return item;
        }

        public BaseItem RetrieveItem(Guid id) {

            BaseItem item = null;
            //using (new MediaBrowser.Util.Profiler("===========RetrieveItem============="))
            {
                //if (!Kernel.UseNewSQLRepo)
                //{
                //    item = RetrieveItemOld(id);
                //}
                //else
                {
                    var cmd2 = connection.CreateCommand();
                    cmd2.CommandText = "select * from items where guid = @guid";
                    cmd2.AddParam("@guid", id);
                    using (var reader = cmd2.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string itemType = reader["obj_type"].ToString();

                            if (!string.IsNullOrEmpty(itemType))
                            {
                                item = GetItem(reader, itemType);
                            }
                        }
                    }
                }
            }
            
            return item;
        }

        protected BaseItem GetItem(SQLiteDataReader reader, string itemType)
        {
            BaseItem item = null;
            try
            {
                item = Serializer.Instantiate<BaseItem>(itemType);
            }
            catch (Exception e)
            {
                Logger.ReportException("Error trying to create instance of type: " + itemType, e);
                return null;
            }
            if (item != null)
            {
                SQLInfo itemSQL;
                lock (ItemSQL)
                {
                    if (!ItemSQL.TryGetValue(item.GetType(), out itemSQL))
                    {
                        itemSQL = new SQLInfo(item);
                        ItemSQL.Add(item.GetType(), itemSQL);
                        //make sure our schema matches
                        itemSQL.FixUpSchema(connection);
                    }
                }
                foreach (var col in itemSQL.AtomicColumns)
                {
                    var data = SQLizer.Extract(reader, col);
                    if (data != null)
                        if (col.MemberType == MemberTypes.Property)
                            col.PropertyInfo.SetValue(item, data, null);
                        else
                            col.FieldInfo.SetValue(item, data);

                }
                // and our list columns
                //this is an optimization - we go get all the list values for this item in one statement
                var listCmd = connection.CreateCommand();
                listCmd.CommandText = "select property, value from list_items where guid = @guid and property != 'ActorName' order by property, sort_order";
                listCmd.AddParam("@guid", item.Id);
                string currentProperty = "";
                System.Collections.IList list = null;
                SQLInfo.ColDef column = new SQLInfo.ColDef();
                using (var listReader = listCmd.ExecuteReader())
                {
                    while (listReader.Read())
                    {
                        string property = listReader.GetString(0);
                        if (property != currentProperty)
                        {
                            //new column...
                            if (list != null)
                            {
                                //fill in the last one
                                if (column.MemberType == MemberTypes.Property)
                                    column.PropertyInfo.SetValue(item, list, null);
                                else
                                    column.FieldInfo.SetValue(item, list);
                            }
                            currentProperty = property;
                            column = itemSQL.Columns.Find(c => c.ColName == property);
                            list = (System.Collections.IList)column.ColType.GetConstructor(new Type[] { }).Invoke(null);
                            //Logger.ReportVerbose("Added list item '" + listReader[0] + "' to " + col.ColName);
                        }
                        try
                        {
                            list.Add(SQLizer.Extract(listReader, new SQLInfo.ColDef() { ColName = "value", ColType = column.InternalType }));
                        }
                        catch (Exception e)
                        {
                            Logger.ReportException("Error adding item to list " + column.ColName + " on item " + item.Name, e);
                        }
                    }
                    if (list != null)
                    {
                        //fill in the last one
                        if (column.MemberType == MemberTypes.Property)
                            column.PropertyInfo.SetValue(item, list, null);
                        else
                            column.FieldInfo.SetValue(item, list);
                    }
                }
            }
            else
            {
                Logger.ReportWarning("Ignoring invalid item " + itemType + ".  Would not instantiate in current environment.");
            }
            return item;
        }

        public void SaveItem(BaseItem item)
        {
            if (item == null) return;

            if (!ItemSQL.ContainsKey(item.GetType()))
            {
                ItemSQL.Add(item.GetType(), new SQLInfo(item));
                //make sure our schema matches
                ItemSQL[item.GetType()].FixUpSchema(connection);
            }
            var cmd2 = connection.CreateCommand();
            cmd2.CommandText = ItemSQL[item.GetType()].UpdateStmt;


            cmd2.AddParam("@0", item.Id);
            cmd2.AddParam("@1", item.GetType().FullName);
            int colNo = 2; //id was 0 type was 1...
            foreach (var col in ItemSQL[item.GetType()].AtomicColumns)
            {
                if (col.MemberType == MemberTypes.Property)
                    cmd2.AddParam("@" + colNo, SQLizer.Encode(col, col.PropertyInfo.GetValue(item, null)));
                else
                    cmd2.AddParam("@" + colNo, SQLizer.Encode(col, col.FieldInfo.GetValue(item)));
                colNo++;
            }
            QueueCommand(cmd2);

            //and now each of our list members
            var delCmd = connection.CreateCommand();
            delCmd.CommandText = "delete from list_items where guid = @guid";
            delCmd.AddParam("@guid", item.Id);
            delCmd.ExecuteNonQuery();
            foreach (var col in ItemSQL[item.GetType()].ListColumns)
            {
                System.Collections.IEnumerable list = null;

                if (col.MemberType == MemberTypes.Property)
                {
                    //var it = col.PropertyInfo.GetValue(item, null);
                    //Type ittype = it.GetType();
                    list = col.PropertyInfo.GetValue(item, null) as System.Collections.IEnumerable;
                }
                else
                    list = col.FieldInfo.GetValue(item) as System.Collections.IEnumerable;

                if (list != null)
                {
                    var insCmd = connection.CreateCommand();
                    insCmd.CommandText = "insert or ignore into list_items(guid, property, value, sort_order) values(@guid, @property, @value, @order)"; // possible another thread beat us to it...
                    insCmd.AddParam("@guid", item.Id);
                    insCmd.AddParam("@property", col.ColName);
                    SQLiteParameter val = new SQLiteParameter("@value");
                    insCmd.Parameters.Add(val);
                    SQLiteParameter ord = new SQLiteParameter("@order");
                    insCmd.Parameters.Add(ord);

                    //special handling for actors because they are saved serialized - we also need to save them in a query-able form...
                    var insActorCmd = connection.CreateCommand();
                    bool isActor = col.InternalType == typeof(Actor);
                    SQLiteParameter val2 = new SQLiteParameter("@value2");
                    if (isActor)
                    {
                        insActorCmd.CommandText = "insert or ignore into list_items(guid, property, value, sort_order) values(@guid, 'ActorName', @value2, @order)";
                        insActorCmd.AddParam("@guid", item.Id);
                        insActorCmd.Parameters.Add(val2);
                        insActorCmd.Parameters.Add(ord);
                    }

                    int order = 0;
                    foreach (var listItem in list)
                    {
                        val.Value = SQLizer.Encode(new SQLInfo.ColDef() { ColType = col.InternalType, InternalType = listItem.GetType() }, listItem);
                        ord.Value = order;
                        QueueCommand(insCmd);
                        if (isActor)
                        {
                            //Logger.ReportInfo("Saving Actor Name '" + (listItem as Actor).Person.Name+"'");
                            val2.Value = (listItem as Actor).Person.Name;
                            QueueCommand(insActorCmd);
                        }
                        order++;
                    }

                }

                //finally, update the recent list
                //if (item is Media) //don't need to track non-media items
                //{
                //    var recCmd = connection.CreateCommand();
                //    recCmd.CommandText = "replace into recent_list(top_parent, child, date_added) values(@top, @child, @date)";
                //    recCmd.AddParam("@top", item.TopParent);
                //    recCmd.AddParam("@child", item.Id);
                //    recCmd.AddParam("@date", item.DateCreated);
                //    recCmd.ExecuteNonQuery();
                //}
            }
        }

        /// <summary>
        /// Generic routine to retrieve a list of items
        /// </summary>
        /// <param name="selectStmt"></param>
        /// <param name="parms"></param>
        /// <returns></returns>
        private IEnumerable<BaseItem> GetItems(string selectStmt, SQLiteParameter[] parms)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = selectStmt;
            cmd.Parameters.AddRange(parms);
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    string itemType = reader["obj_type"].ToString();

                    if (!string.IsNullOrEmpty(itemType))
                    {
                        yield return GetItem(reader, itemType);
                    }
                }
            }
        }

        public IEnumerable<IMetadataProvider> RetrieveProviders(Guid guid) {
            var providers = new List<IMetadataProvider>();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "select data from provider_data where guid = @guid";
            var guidParam = cmd.Parameters.Add("@guid", System.Data.DbType.Guid);
            guidParam.Value = guid;

            using (var reader = cmd.ExecuteReader()) {
                while (reader.Read()) {
                    //Logger.ReportVerbose("Retrieving Provider: " + reader.GetString(1));
                    using (var ms = new MemoryStream(reader.GetBytes(0))) {

                        try
                        {
                            var data = (IMetadataProvider)Serializer.Deserialize<object>(ms);
                            if (data != null) providers.Add(data);
                        }
                        catch (SerializationException e)
                        {
                            Logger.ReportException("Corrupt provider: " + reader.GetString(0), e);
                        }
                    }
                }
            }

            return providers.Count == 0 ? null : providers;
        }

        public void SaveProviders(Guid guid, IEnumerable<IMetadataProvider> providers) {

            IMetadataProvider[] providerCopy;
            lock (providers) {
                providerCopy = providers.ToArray();
            }
            lock (delayedCommands) {
                var cmd = connection.CreateCommand();

                cmd.CommandText = "delete from provider_data where guid = @guid";
                cmd.AddParam("@guid", guid);
                QueueCommand(cmd);

                foreach (var provider in providerCopy) {
                    cmd = connection.CreateCommand();
                    cmd.CommandText = "insert into provider_data (guid, full_name, data) values (@guid, @full_name, @data)";
                    cmd.AddParam("@guid", guid);
                    cmd.AddParam("@full_name", provider.GetType().FullName);
                    var dataParam = cmd.AddParam("@data");

                    using (var ms = new MemoryStream()) {
                        Serializer.Serialize(ms, (object)provider);
                        dataParam.Value = ms.ToArray();
                        QueueCommand(cmd);
                    }
                }
            }
        }

        public int ClearCache(string objType)
        {
            int ret = -1;
            lock (connection)
            {
                try
                {

                    ret = connection.Exec("delete from items where obj_type like '%" + objType + "'");
                    connection.Exec("vacuum");
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error trying to clear cache of object type: " + objType, e);
                }
            }
            return ret;
        }


        public bool ClearEntireCache() {
            lock (connection) {
                //first drop our cascade delete triggers to speed this up
                connection.Exec("drop trigger delete_item");
                var tran = connection.BeginTransaction();
                connection.Exec("delete from provider_data"); 
                connection.Exec("delete from items");
                connection.Exec("delete from children");
                connection.Exec("delete from list_items");
                //connection.Exec("delete from display_prefs");
                // People will get annoyed if this is lost
                // connection.Exec("delete from play_states");
                tran.Commit(); 
                //and put back the triggers
                connection.Exec(triggerSQL);
                connection.Exec("vacuum");
            }

            return true;
        }

    }
}
