using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using System.Xml;
using MediaBrowser.Library.Logging;
using MediaBrowser;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Threading;

namespace MBMigrate
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //private ServiceConfigData _serviceConfig;
        private ConfigData _config;

        public MainWindow()
        {
            InitializeComponent();
            //_serviceConfig = ServiceConfigData.FromFile(ApplicationPaths.ServiceConfigFile);
            Async.Queue("Migration", () =>
            {
                if (File.Exists(ApplicationPaths.ConfigFile)) _config = ConfigData.FromFile(ApplicationPaths.ConfigFile);
                if (_config == null) // only do this if a fresh install
                {
                    try
                    {
                        Migrate300();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error during migration",e);
                    }
                    
                }
                Dispatcher.Invoke(DispatcherPriority.Background, (System.Windows.Forms.MethodInvoker)(Close));
            });
        }

        static Dictionary<string, string> oldPathMap;

        static readonly string[,] oldTree = { 
                    { "AppConfigPath",       "app_data",         "MediaBrowser"  }, 
                    { "AppCachePath",        "AppConfigPath",    "Cache"         },
                    { "AppUserSettingsPath", "AppConfigPath",    "Cache"           },
                    { "AutoPlaylistPath",    "AppCachePath",     "autoPlaylists" }, 
                    { "AppImagePath",        "AppConfigPath",    "ImageCache"},
                    { "AppInitialDirPath",   "AppConfigPath",    "StartupFolder" },
                    { "AppPluginPath",       "AppConfigPath",    "Plugins" },
                    { "AppRSSPath",          "AppConfigPath",    "RSS"},
                    { "AppLogPath",          "AppConfigPath",    "Logs"},
                    { "DefaultPodcastPath", "AppConfigPath", "Podcasts"    },
                    { "AppLocalizationPath","AppConfigPath", "Localization" },
                    { "PluginConfigPath", "AppPluginPath", "Configurations"},
                    { "CustomImagePath", "AppImagePath", "Custom"}
            };

        static void BuildTree()
        {
            for (var i = 0; i <= oldTree.GetUpperBound(0); i++)
            {
                var e = Path.Combine(oldPathMap[oldTree[i, 1]], oldTree[i, 2]);
                oldPathMap[oldTree[i, 0]] = e;
            }
        }

        public void BackupConfig(Version ver)
        {
            string backupName = Path.Combine(ApplicationPaths.AppConfigPath,
                Path.GetFileNameWithoutExtension(ApplicationPaths.ConfigFile) + " (" + ver.ToString() + ").config");
            if (!File.Exists(backupName))
            {
                try
                {
                    File.Copy(ApplicationPaths.ConfigFile, backupName);
                }
                catch 
                {
                    // no biggie...
                }
            }
        }

        public void Migrate300()
        {
            var knownCompatibleDlls = new List<string> {
                "Ascendancy.dll",
                "BDScreenSaver.dll",
                "Centrality.dll",
                "Chocolate.dll",
                "CoverSS.dll",
                "Destiny.dll",
                "Diamond.dll",
                "Harmony.dll",
                "Imperium.dll",
                "Jade.dll",
                "Kismet.dll",
                "Lotus.dll",
                "Maelstrom.dll",
                "Neo.dll",
                "Regency.dll",
                "Sapphire.dll",
                "Simplicity.dll",
                "Subdued.dll",
                "Supremacy.dll",
                "Vanilla.dll",

            };

            var current = new Version(_config != null ? _config.MBVersion : "2.6.2.0");
            if (current < new Version(3, 0, 0))
            {
                //Get our old directory structure
                oldPathMap = new Dictionary<string, string>();
                oldPathMap["app_data"] = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);

                BuildTree();

                //Move over config file
                CopyFile(Path.Combine(oldPathMap["AppConfigPath"], "MediaBrowserXml.config"), Path.Combine(ApplicationPaths.AppConfigPath,"MediaBrowserXml.config"));

                //And Plugins that work
                foreach (var dll in knownCompatibleDlls)
                {
                    CopyFile(Path.Combine(oldPathMap["AppPluginPath"], dll), Path.Combine(ApplicationPaths.AppPluginPath, dll));
                }

                //And the Localization folder
                foreach (var file in Directory.GetFiles(oldPathMap["AppLocalizationPath"]))
                {
                    try
                    {
                        CopyFile(file, Path.Combine(ApplicationPaths.AppLocalizationPath, Path.GetFileName(file)));
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error copying file {0} to {1}", e, file, Path.Combine(ApplicationPaths.AppLocalizationPath, Path.GetFileName(file)));
                    }
                }
            }
        }

        protected void CopyFile(string source, string dest)
        {
            try
            {
                File.Copy(source, dest, true);
            }
            catch (Exception e)
            {
                Logger.ReportException("Error copying file {0} to {1}.",e,source, dest);
            }

            
        }

        public void Migrate261()
        {
            //version 2.6 migration
            Version current = new Version(_config.MBVersion);
            if (current > new Version(2, 0) && current < new Version(2, 6, 1))
            {
                //need to continue to do this...
                MigratePlugins();

                //create new IBN directories
                string ibnLocation = ApplicationPaths.AppIBNPath;
                try
                {
                    if (!Directory.Exists(Path.Combine(ibnLocation, "default")))
                    {
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Video"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Movie"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Episode"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Series"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Season"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Folder"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\BoxSet"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Actor"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Genre"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Year"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Studio"));
                    }
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error creating new IBN folders", e);
                }

                //translate PC to new values
                Dictionary<int, int> translations = new Dictionary<int, int>()
                {
                    {1,1},
                    {2,5},
                    {3,7},
                    {4,9},
                    {5,10}
                };
                if (translations.ContainsKey(_config.MaxParentalLevel))
                {
                    _config.MaxParentalLevel = translations[_config.MaxParentalLevel];
                    _config.Save();
                }
            }
        }

        public void Migrate26()
        {
            //version 2.6 migration
            Version current = new Version(_config.MBVersion);
            if (current > new Version(2, 0) && current < new Version(2, 6))
            {
                BackupConfig(current);
                //external config
                UpgradeExternalPlayerXml();
                //set install directory and clean any bad ext players and reset image sizes
                try
                {
                    _config = ConfigData.FromFile(ApplicationPaths.ConfigFile);  //re-load because ext player migration changed it
                    _config.MBInstallDir = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
                    _config.ExternalPlayers.RemoveAll(p => String.IsNullOrEmpty(p.ExternalPlayerName));
                    _config.FetchedPosterSize = "w500";
                    _config.FetchedBackdropSize = "w1280";
                    _config.Save();
                }
                catch { }
            }
        }

        /// <summary>
        /// Upgrades extenral player xml from version 2.5.3 and below
        /// </summary>
        private void UpgradeExternalPlayerXml()
        {
            UpdateProgress("External Players", .5);
            XmlDocument doc = new XmlDocument();
            string xmlPath = ApplicationPaths.ConfigFile;
            doc.Load(xmlPath);
            XmlNode externalPlayersNode = doc.DocumentElement.SelectSingleNode("ExternalPlayers");

            if (externalPlayersNode == null || externalPlayersNode.InnerXml.Contains("<MediaTypes>"))
            {
                // Either no external players or we've already been converted (MediaTypes exist)
                return;
            }

            // Wrap <MediaType> with <MediaTypes>
            externalPlayersNode.InnerXml = externalPlayersNode.InnerXml.Replace("<MediaType>", "<MediaTypes><MediaType>").Replace("</MediaType>", "</MediaType></MediaTypes>");

            // Remove quotes from Args
            externalPlayersNode.InnerXml = externalPlayersNode.InnerXml.Replace("\"{0}\"", "{0}");

            // Loop through each one and try to detect the ExternalPlayerName based on the command
            foreach (XmlNode node in externalPlayersNode.SelectNodes("ExternalPlayer"))
            {
                string command = node.SelectSingleNode("Command").InnerText.ToLower();

                XmlElement typeElement = doc.CreateElement("ExternalPlayerName");

                if (command.Contains("mpc-hc"))
                {
                    typeElement.InnerText = "MPC-HC";
                }
                else if (command.Contains("vlc"))
                {
                    typeElement.InnerText = "VLC 2";
                }
                else if (command.Contains("utotalmediatheatre5"))
                {
                    typeElement.InnerText = "TotalMedia Theatre 5";
                }
                else
                {
                    typeElement.InnerText = "Generic";
                }

                node.AppendChild(typeElement);
            }

            MergeExternalPlayers(doc, externalPlayersNode);
            doc.Save(xmlPath);
        }

        /// <summary>
        /// In version 2.5.3 and below, external players were defined individually by MediaType.
        /// Now you define a player once and select multipe MediaTypes
        /// This will attempt to merge the multiple definitions into a single one
        /// </summary>
        private void MergeExternalPlayers(XmlDocument doc, XmlNode externalPlayersNode)
        {
            XmlNodeList nodes = externalPlayersNode.SelectNodes("ExternalPlayer");

            bool merged = false;

            // Loop through each node starting with the last and counting down
            for (int i = 1; i < nodes.Count; i++ )
            {
                XmlNode node = nodes[i];

                // Get the command and MediaType
                string command = node.SelectSingleNode("Command").InnerText.ToLower();
                string mediaType = node.SelectSingleNode("MediaTypes/MediaType").InnerText;

                XmlNode nodeToMergeWith = null;

                // Now go through each one from the beginning and see if there's another player using the same command
                for (int j = 0; j < i; j++)
                {
                    var testNode = nodes[j];

                    // Found a match
                    if (testNode.SelectSingleNode("Command").InnerText.ToLower() == command)
                    {
                        nodeToMergeWith = testNode;
                        break;
                    }
                }

                // If we found a match, add the MediaType to the one we found and delete the current node
                if (nodeToMergeWith != null)
                {
                    XmlElement mediaTypeElem = doc.CreateElement("MediaType");
                    mediaTypeElem.InnerText = mediaType;
                    nodeToMergeWith.SelectSingleNode("MediaTypes").AppendChild(mediaTypeElem);
                    externalPlayersNode.RemoveChild(node);
                    merged = true;
                    break;
                }
            }

            // Keep going until there are no more merges
            if (merged)
            {
                MergeExternalPlayers(doc, externalPlayersNode);
            }
        }


        public void Migrate253()
        {
            //version 2.5.3 migration
            MigratePlugins();
        }

        public void MigratePlugins()
        {
            List<string> KnownGlobalDLLs = new List<string>() {
                "Ascendancy.dll",
                "BDScreenSaver.dll",
                "Centrality.dll",
                "Chocolate.dll",
                "CoverSS.dll",
                "Destiny.dll",
                "Diamond.dll",
                "Harmony.dll",
                "Imperium.dll",
                "Jade.dll",
                "Kismet.dll",
                "Lotus.dll",
                "Maelstrom.dll",
                "Neo.dll",
                "Regency.dll",
                "Sapphire.dll",
                "Simplicity.dll",
                "StorageViewer.dll",
                "Subdued.dll",
                "Supremacy.dll",
                "TraktMB.dll",
                "Vanilla.dll",
                "gamebrowser.dll",

            };

            Version current = new Version(_config.MBVersion);
            if (current > new Version(2, 0) && current < new Version(2, 5, 3))
            {
                int total = Directory.GetFiles(ApplicationPaths.AppPluginPath).Count();
                int i = 0;
                foreach (var file in Directory.GetFiles(ApplicationPaths.AppPluginPath))
                {
                    i++;
                    UpdateProgress("Migrating plugins...", (double)(i / total));
                    if (file.ToLower().EndsWith(".pgn"))
                    {
                        //find dll in ehome and move to plugins
                        string dll = Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("windir"), "ehome"), Path.ChangeExtension(Path.GetFileName(file), ".dll"));
                        if (File.Exists(dll))
                        {
                            try
                            {
                                File.Move(dll, Path.Combine(ApplicationPaths.AppPluginPath, Path.GetFileName(dll)));
                            }
                            catch (FileNotFoundException)
                            {
                                //let it go - someone deleted the dll but not the pgn file
                            }
                            catch (Exception e)
                            {
                                //nothing we can really do here but warn - have to clean it up manually
                                Dispatcher.Invoke(DispatcherPriority.Background, (System.Windows.Forms.MethodInvoker)(() => MessageBox.Show("Error moving plugin " + dll + ". \n\nYou will have to re-install it from the configurator.\n\n" + e.Message)));
                                continue;
                            }
                        }
                        //and delete the pgn file
                        try
                        {
                            File.Delete(file);
                        }
                        catch { }
                    }
                }

                //now go through and clean up any old ones that may have been left behind
                foreach (var dll in KnownGlobalDLLs)
                {
                    try { File.Delete(Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("windir"), "ehome"), dll)); }
                    catch { }
                }
            }
                        

        }

        //public void Migrate25()
        //{
        //    //version 2.5 migration
        //    Version current = new Version(_config.MBVersion);
        //    if (current > new Version(2, 0) && current < new Version(2, 5))
        //    {
        //        string sqliteDb = Path.Combine(ApplicationPaths.AppCachePath, "cache.db");
        //        string sqliteDll = Path.Combine(ApplicationPaths.AppConfigPath, "system.data.sqlite.dll");
        //        if (!_config.EnableExperimentalSqliteSupport)
        //            //clean up any old sql db...
        //            try
        //            {
        //                File.Delete(sqliteDb);
        //            }
        //            catch { }

        //        Kernel.Init(_config);
        //        Logger.ReportInfo("==== Migration Process Started...");
        //        var newRepo = Kernel.Instance.ItemRepository;
        //        try
        //        {
        //            //var oldRepo = new MediaBrowser.Library.ItemRepository();
        //            //UpdateProgress("Preparing...", .03);
        //            //Thread.Sleep(15000); //allow old repo to load
        //            if (_config.EnableExperimentalSqliteSupport)
        //            {
        //                UpdateProgress("Backing up DB", .05);
        //                Logger.ReportInfo("Attempting to backup cache db...");
        //                if (newRepo.BackupDatabase()) Logger.ReportInfo("Database backed up successfully");
        //            }
        //            //UpdateProgress("PlayStates", .10);
        //            ////newRepo.MigratePlayState(oldRepo);
                    
        //            //UpdateProgress("DisplayPrefs", .20);
        //            //newRepo.MigrateDisplayPrefs(oldRepo);

        //            UpdateProgress("Images", .01);
        //            //MediaBrowser.Library.ImageManagement.ImageCache.Instance.DeleteResizedImages();

        //            if (_config.EnableExperimentalSqliteSupport)
        //            {
        //                //were already using SQL - our repo can migrate itself
        //                UpdateProgress("Items", .80);
        //                newRepo.MigrateItems();
        //            }
        //            else
        //            {
        //                //need to go through the file-based repo and re-save
        //                MediaBrowser.Library.Entities.BaseItem item;
        //                int cnt = 0;
        //                string[] cacheFiles = Directory.GetFiles(Path.Combine(ApplicationPaths.AppCachePath, "Items"));
        //                double total = cacheFiles.Count();
        //                foreach (var file in cacheFiles)
        //                {
        //                    UpdateProgress("Items", (double)(cnt / total));
        //                    try
        //                    {
        //                        using (Stream fs = MediaBrowser.Library.Filesystem.ProtectedFileStream.OpenSharedReader(file))
        //                        {
        //                            BinaryReader reader = new BinaryReader(fs);
        //                            item = Serializer.Deserialize<MediaBrowser.Library.Entities.BaseItem>(fs);
        //                        }

        //                        if (item != null)
        //                        {
        //                            Logger.ReportInfo("Migrating Item: " + item.Name);
        //                            newRepo.SaveItem(item);
        //                            if (item is Folder)
        //                            {
        //                                //need to save our children refs
        //                                var children = RetrieveChildrenOld(item.Id);
        //                                if (children != null) newRepo.SaveChildren(item.Id, children);
        //                            }
        //                            cnt++;
        //                            if (item is Video && (item as Video).RunningTime != null)
        //                            {
        //                                TimeSpan duration = TimeSpan.FromMinutes((item as Video).RunningTime.Value);
        //                                if (duration.Ticks > 0)
        //                                {
        //                                    PlaybackStatus ps = newRepo.RetrievePlayState(item.Id);
        //                                    decimal pctIn = Decimal.Divide(ps.PositionTicks, duration.Ticks) * 100;
        //                                    if (pctIn > Kernel.Instance.ConfigData.MaxResumePct)
        //                                    {
        //                                        Logger.ReportInfo("Setting " + item.Name + " to 'Watched' based on last played position.");
        //                                        ps.PositionTicks = 0;
        //                                        newRepo.SavePlayState(ps);
        //                                    }
        //                                }
        //                            }
        //                        }
                                    
        //                    }
        //                    catch (Exception e)
        //                    {
        //                        //this could fail if some items have already been refreshed before we migrated them
        //                        Logger.ReportException("Could not migrate item (probably just old data) " + file + e != null && e.InnerException != null ? " Inner Exception: " + e.InnerException.Message : "", e);
        //                    }
        //                }
        //                Logger.ReportInfo(cnt + " Items migrated successfully.");
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            Logger.ReportException("Error in migration - will need to re-build cache.", e);
        //            try
        //            {
        //                File.Delete(sqliteDb);
        //            }
        //            catch { }
        //        }
        //        UpdateProgress("Finishing up...",1);
        //        try
        //        {
        //            Async.RunWithTimeout(newRepo.ShutdownDatabase, 30000); //be sure all writes are flushed
        //        }
        //        catch
        //        {
        //            Logger.ReportWarning("Timed out attempting to close out DB.  Assuming all is ok and moving on...");
        //        }
        //    }
        //    else Logger.ReportInfo("Nothing to Migrate.  Version is: " + _config.MBVersion);
            
        //}

        public IEnumerable<Guid> RetrieveChildrenOld(Guid id)
        {

            List<Guid> children = new List<Guid>();
            string file = Path.Combine(Path.Combine(ApplicationPaths.AppCachePath, "Children"), id.ToString("N"));
            if (!File.Exists(file)) return null;

            try
            {

                using (Stream fs = MediaBrowser.Library.Filesystem.ProtectedFileStream.OpenSharedReader(file))
                {
                    BinaryReader br = new BinaryReader(fs);
                    lock (children)
                    {
                        var count = br.ReadInt32();
                        var itemsRead = 0;
                        while (itemsRead < count)
                        {
                            children.Add(br.ReadGuid());
                            itemsRead++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Failed to retrieve children:", e);
                return null;
            }
            return children.Count == 0 ? null : children;
        }

        private void UpdateProgress(string step, double pctDone)
        {
            Dispatcher.Invoke(DispatcherPriority.Background, (System.Windows.Forms.MethodInvoker)(() =>
            {
                lblCurrent.Content = step;
                progress.Value = pctDone;
            }));
        }
    }
}
