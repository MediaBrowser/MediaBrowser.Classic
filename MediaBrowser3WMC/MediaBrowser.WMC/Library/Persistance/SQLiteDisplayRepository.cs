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

namespace MediaBrowser.Library.Persistance
{
    public class SQLiteDisplayRepository : SQLiteRepository
    {

        const string CURRENT_SCHEMA_VERSION = "2.6.1.0";

        public SQLiteDisplayRepository(string dbPath)
        {


            if (!ConnectToDB(dbPath)) throw new ApplicationException("CRITICAL ERROR - Unable to connect to database: " + dbPath + ".  Program cannot function.");


            string[] queries = {
                               "create table if not exists display_prefs (guid primary key, view_type, show_labels, vertical_scroll, sort_order, index_by, use_banner, thumb_constraint_width, thumb_constraint_height, use_coverflow, use_backdrop )",
                               "create index if not exists idx_display on display_prefs (guid)",
                               "create table if not exists custom_display_prefs (guid, parm_key, parm_value, primary key (guid, parm_key))",
                               "create index if not exists idx_custom on custom_display_prefs (guid, parm_key)",
                               "create table if not exists schema_version (table_name primary key, version)",
                               };


            RunQueries(queries);
            MigrateCustomDisplayPrefs();
            alive = true; // tell writer to keep going
            Async.Queue("Sqlite Display Writer", DelayedWriter);

        }


        private void MigrateCustomDisplayPrefs()
        {
            if (SchemaVersion("custom_display_prefs") != CURRENT_SCHEMA_VERSION)
            {
                Logger.ReportInfo("*** Migrating custom display prefs to version " + CURRENT_SCHEMA_VERSION);
                string[] queries = 
                {
                    "drop table custom_display_prefs",
                               "create table if not exists custom_display_prefs (guid, parm_key, parm_value, primary key (guid, parm_key))",
                               "create index if not exists idx_custom on custom_display_prefs (guid, parm_key)",
                };
                if (RunQueries(queries)) SetSchemaVersion("custom_display_prefs", CURRENT_SCHEMA_VERSION);
            }
        }

        public DisplayPreferences RetrieveDisplayPreferences(DisplayPreferences dp)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "select view_type, show_labels, vertical_scroll, sort_order, index_by, use_banner, thumb_constraint_width, thumb_constraint_height, use_coverflow, use_backdrop from display_prefs where guid = @guid";
            cmd.AddParam("@guid", dp.Id);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    dp.StopListeningForChanges(); //turn this off or we'll trigger the save routine before everything is filled in
                    try
                    {
                        dp.ViewType.Chosen = ViewTypeNames.GetName((ViewType)Enum.Parse(typeof(ViewType), reader.GetString(0)));
                    }
                    catch
                    {
                        dp.ViewType.Chosen = ViewTypeNames.GetName(MediaBrowser.Library.ViewType.Poster);
                    }
                    dp.ShowLabels.Value = reader.GetBoolean(1);
                    dp.VerticalScroll.Value = reader.GetBoolean(2);
                    try
                    {
                        dp.SortOrder = reader.GetString(3);
                    }
                    catch { }
                    if (Config.Instance.RememberIndexing)
                        dp.IndexBy = reader.GetString(4);
                    else
                        dp.IndexBy = Localization.LocalizedStrings.Instance.GetString("NoneDispPref");

                    dp.UseBanner.Value = reader.GetBoolean(5);
                    dp.ThumbConstraint.Value = new Microsoft.MediaCenter.UI.Size(reader.GetInt32(6), reader.GetInt32(7));
                    dp.UseCoverflow.Value = reader.GetBoolean(8);
                    dp.UseBackdrop.Value = reader.GetBoolean(9);

                    dp.ListenForChanges(); //turn back on
                }
            }

            cmd = connection.CreateCommand();
            cmd.CommandText = "select parm_key, parm_value from custom_display_prefs where guid = @guid";
            cmd.AddParam("@guid", dp.Id);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    try
                    {
                        dp.CustomParms[reader.GetString(0)] = reader.GetString(1);
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error reading custom display prefs.", e);
                    }
                }
            }

            return dp;
        }

        public void SaveDisplayPreferences(DisplayPreferences dp)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "replace into display_prefs (guid, view_type, show_labels, vertical_scroll, sort_order, index_by, use_banner, thumb_constraint_width, thumb_constraint_height, use_coverflow, use_backdrop) values (@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11)";
            cmd.AddParam("@1", dp.Id);
            cmd.AddParam("@2", ViewTypeNames.GetEnum((string)dp.ViewType.Chosen).ToString());
            cmd.AddParam("@3", dp.ShowLabels.Value);
            cmd.AddParam("@4", dp.VerticalScroll.Value);
            cmd.AddParam("@5", dp.SortOrder.ToString());
            cmd.AddParam("@6", dp.IndexByString);
            cmd.AddParam("@7", dp.UseBanner.Value);
            cmd.AddParam("@8", dp.ThumbConstraint.Value.Width);
            cmd.AddParam("@9", dp.ThumbConstraint.Value.Height);
            cmd.AddParam("@10", dp.UseCoverflow.Value);
            cmd.AddParam("@11", dp.UseBackdrop.Value);

            QueueCommand(cmd);

            //custom prefs
            //var delCmd = connection.CreateCommand();
            //delCmd.CommandText = "delete from custom_display_prefs where guid = @guid";
            //delCmd.AddParam("@guid", dp.Id);
            //delCmd.ExecuteNonQuery();
            var insCmd = connection.CreateCommand();
            insCmd.CommandText = "insert or replace into custom_display_prefs(guid, parm_key, parm_value) values(@guid, @key, @value)"; 
            insCmd.AddParam("@guid", dp.Id);
            SQLiteParameter val = new SQLiteParameter("@value");
            insCmd.Parameters.Add(val);
            SQLiteParameter key = new SQLiteParameter("@key");
            insCmd.Parameters.Add(key);
            foreach (var pair in dp.CustomParms)
            {
                key.Value = pair.Key;
                val.Value = pair.Value;
                QueueCommand(insCmd);
            }

        }

        public int MigrateDisplayPrefs()
        {
            //direct migration from file to db so we can do it in the service (can't create dp out of core)
            string path = Path.Combine(ApplicationPaths.AppUserSettingsPath, "display");
            if (!Directory.Exists(path)) return 0; //nothing to migrate

            var cmd = connection.CreateCommand();
            cmd.CommandText = "replace into display_prefs (guid, view_type, show_labels, vertical_scroll, sort_order, index_by, use_banner, thumb_constraint_width, thumb_constraint_height, use_coverflow, use_backdrop) values (@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11)";
            cmd.Parameters.Add("@1", DbType.Guid);
            cmd.Parameters.Add("@2", DbType.String);
            cmd.Parameters.Add("@3", DbType.Boolean);
            cmd.Parameters.Add("@4", DbType.Boolean);
            cmd.Parameters.Add("@5", DbType.String);
            cmd.Parameters.Add("@6", DbType.String);
            cmd.Parameters.Add("@7", DbType.Boolean);
            cmd.Parameters.Add("@8", DbType.Int32);
            cmd.Parameters.Add("@9", DbType.Int32);
            cmd.Parameters.Add("@10", DbType.Boolean);
            cmd.Parameters.Add("@11", DbType.Boolean);

            int cnt = 0;
            lock (connection)
            {
                var tran = connection.BeginTransaction();
                foreach (var file in Directory.GetFiles(path))
                {
                    using (Stream fs = MediaBrowser.Library.Filesystem.ProtectedFileStream.OpenSharedReader(file))
                    {
                        var reader = new BinaryReader(fs);
                        byte version = reader.ReadByte();
                        cmd.Parameters[0].Value = new Guid(Path.GetFileName(file));
                        cmd.Parameters[1].Value = reader.SafeReadString();
                        cmd.Parameters[2].Value = reader.ReadBoolean();
                        cmd.Parameters[3].Value = reader.ReadBoolean();
                        cmd.Parameters[4].Value = reader.SafeReadString();
                        cmd.Parameters[5].Value = reader.SafeReadString();
                        cmd.Parameters[6].Value = reader.ReadBoolean();
                        cmd.Parameters[7].Value = reader.ReadInt32();
                        cmd.Parameters[8].Value = reader.ReadInt32();
                        cmd.Parameters[9].Value = version >= 2 ? reader.ReadBoolean() : false;
                        cmd.Parameters[10].Value = version >= 3 ? reader.ReadBoolean() : false;
                        cmd.ExecuteNonQuery();
                        cnt++;
                    }
                }
                tran.Commit();
            }
            return cnt;
        }

        public ThumbSize RetrieveThumbSize(Guid id)
        {
            int w = 0, h = 0;
            var cmd = connection.CreateCommand();
            cmd.CommandText = "select thumb_constraint_width, thumb_constraint_height from display_prefs where guid = @guid";
            cmd.AddParam("@guid", id);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    w = reader.GetInt32(0);
                    h = reader.GetInt32(1);
                }
            }
            return new ThumbSize(w, h);
        }

    }
}
