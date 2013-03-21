using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Entities;
using System.Data.SQLite;
using MediaBrowser.Library.Configuration;
using System.IO;
using MediaBrowser.Library.Logging;
using System.Reflection;
using System.Threading;
using MediaBrowser.Library.Threading;

namespace MediaBrowser.Library.Persistance
{
    static class SqliteExtensions
    {

        public static bool TableExists(this SQLiteConnection cnn, string table)
        {
            var cmd = cnn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE name=@name";
            cmd.Parameters.Add(new SQLiteParameter("@name", table));
            using (var reader = cmd.ExecuteReader())
            {
                return reader.Read();
            }
        }

        public static int Exec(this SQLiteConnection cnn, string sql)
        {
            var cmd = cnn.CreateCommand();
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }

        public static int Exec(this SQLiteCommand cmd, string sql)
        {
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }

        public static SQLiteParameter AddParam(this SQLiteCommand cmd, string param)
        {
            var sqliteParam = new SQLiteParameter(param);
            cmd.Parameters.Add(sqliteParam);
            return sqliteParam;
        }

        public static SQLiteParameter AddParam(this SQLiteCommand cmd, string param, object data)
        {
            var sqliteParam = AddParam(cmd, param);
            sqliteParam.Value = data;
            return sqliteParam;
        }


        public static byte[] GetBytes(this SQLiteDataReader reader, int col)
        {
            byte[] buffer = new byte[8000];
            using (var ms = new MemoryStream())
            {
                long read = 0;
                long offset = 0;
                while ((read = reader.GetBytes(col, offset, buffer, 0, buffer.Length)) > 0)
                {
                    offset += read;
                    ms.Write(buffer, 0, (int)read);
                }
                return ms.GetBuffer();
            }
        }
    }

    public class SQLiteRepository
    {
        protected static System.Reflection.Assembly sqliteAssembly;
        protected static System.Reflection.Assembly SqliteResolver(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("System.Data.SQLite,"))
            {
                Logger.ReportInfo(args.Name + " is being resolved to "+sqliteAssembly.FullName);
                return sqliteAssembly;
            }
            return null;
        }

        protected SQLiteConnection connection;
        protected List<SQLiteCommand> delayedCommands = new List<SQLiteCommand>();
        protected const int MAX_RETRIES = 5;

        protected virtual bool ConnectToDB(string dbPath)
        {
            SQLiteConnectionStringBuilder connectionstr = new SQLiteConnectionStringBuilder();
            connectionstr.PageSize = 4096;
            connectionstr.CacheSize = 4096;
            connectionstr.SyncMode = SynchronizationModes.Normal;
            connectionstr.DataSource = dbPath;
            connectionstr.JournalMode = SQLiteJournalModeEnum.Delete;
            connection = new SQLiteConnection(connectionstr.ConnectionString);
            int retries = 0;
            bool connected = false;
            while (!connected && retries < MAX_RETRIES)
            {
                try
                {
                    connection.Open();
                    connected = true;
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error connecting to database "+dbPath+"! Will retry " + MAX_RETRIES + " times.", e);
                    retries++;
                    Thread.Sleep(250);
                }
            }

            return connected;
        }

        protected string SchemaVersion(string tableName)
        {
            string version = "";
            var cmd = connection.CreateCommand();
            cmd.CommandText = "select version from schema_version where table_name = @1";
            cmd.AddParam("@1", tableName);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    version = reader.GetString(0);
                }
            }
            return version;
        }

        protected void SetSchemaVersion(string tableName, string version)
        {
            connection.Exec("replace into schema_version (table_name, version) values('" + tableName + "','" + version + "')");
        }

        protected virtual bool RunQueries(string[] queries)
        {
            bool success = true;
            foreach (var query in queries)
            {
                try
                {

                    connection.Exec(query);
                }
                catch (Exception e)
                {
                    Logger.ReportInfo(e.ToString());
                    success = false;
                }
            }
            return success;
        }
                        

        public virtual void ShutdownDatabase()
        {
            alive = false;
            Thread.Sleep(1000); //wait for it to shutdown
            try
            {
                FlushWriter();
                connection.Close();
            }
            catch { }
        }

        protected void QueueCommand(SQLiteCommand cmd)
        {
            lock (delayedCommands)
            {
                delayedCommands.Add((cmd.Clone() as SQLiteCommand));
            }
        }

        ManualResetEvent flushing = new ManualResetEvent(false);

        protected bool alive = true;

        protected void DelayedWriter()
        {
            while (alive)
            {
                flushing.Reset();
                InternalFlush();
                flushing.Set();

                Thread.Sleep(1000);
            }
        }

        private void InternalFlush()
        {
            try
            {

                if (delayedCommands.Count == 0)
                {
                    // Logger.ReportInfo("Delayed writer idle");
                    return;  //no reason to go through this if no cmds...
                }
                List<SQLiteCommand> copy;
                lock (delayedCommands)
                {
                    copy = delayedCommands.ToList();
                    delayedCommands.Clear();
                }

                lock (connection)
                {
                    //Logger.ReportInfo("Executing " + copy.Count + " commands");
                    //using (new MediaBrowser.Util.Profiler("SQL Stmts"))
                    {
                        using (var tran = connection.BeginTransaction())
                        {
                            foreach (var command in copy)
                            {
                                //command.Transaction = tran;
                                //Logger.ReportInfo("About to execute for:  "+ command.Parameters[1].Value+"  "+command.Parameters[14].Value + command.CommandText);
                                try
                                {
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception e)
                                {
                                    string parameters = "";
                                    foreach (SQLiteParameter parm in command.Parameters)
                                        parameters += " " + parm.Value;
                                    Logger.ReportException("Failed to execute SQL Stmt: " + command.CommandText + " Parms: "+parameters, e);
                                }
                            }
                            try
                            {
                                //Logger.ReportInfo("Committing " + copy.Count + " statements...");
                                tran.Commit();
                                //Logger.ReportInfo("Done.");
                            }
                            catch (Exception e)
                            {
                                Logger.ReportException("Failed to commit transaction.", e);
                                tran.Rollback();
                            }
                        }
                    }
                }

            }
            catch (Exception e)
            {
                Logger.ReportException("Critical Exception Failed to Flush: ", e);
            }
        }

        public void FlushWriter()
        {
            flushing.Reset();
            InternalFlush();
            flushing.Set();
        }


    }
}
