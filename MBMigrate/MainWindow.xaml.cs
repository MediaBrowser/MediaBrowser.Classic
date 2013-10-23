using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using System.Xml;
using MediaBrowser.Library;
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
        private CommonConfigData _config;

        public MainWindow()
        {
            // set up assembly resolution hooks, so earlier versions of the plugins resolve properly 
            AppDomain.CurrentDomain.AssemblyResolve += Kernel.OnAssemblyResolve;

            InitializeComponent();
            //_serviceConfig = ServiceConfigData.FromFile(ApplicationPaths.ServiceConfigFile);
            Async.Queue("Migration", () =>
            {
                var mbphoto = Path.Combine(ApplicationPaths.AppPluginPath, "mbphoto.classic.dll");
                if (File.Exists(mbphoto))
                    try
                    {

                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error deleting old MBPhoto plug-in", e);
                    }

                if (File.Exists(ApplicationPaths.CommonConfigFile)) _config = CommonConfigData.FromFile(ApplicationPaths.CommonConfigFile);

                if (_config == null) // only do this if a fresh install
                {
                    try
                    {
                        _config = CommonConfigData.FromFile(ApplicationPaths.CommonConfigFile); // create a new one
                        Migrate300();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error during migration",e);
                    }
                    
                }
                if (_config != null)
                {
                    // Set install directory
                    _config.MBInstallDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
                    _config.Save();
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
            string backupName = Path.Combine(ApplicationPaths.CommonConfigPath,
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
                "BDScreenSaver.dll",
                "Chocolate.dll",
                "CoverSS.dll",

            };

            var current = new Version(_config != null ? _config.MBVersion : "2.6.2.0");
            if (current < new Version(3, 0, 0))
            {
                //Get our old directory structure
                oldPathMap = new Dictionary<string, string>();
                oldPathMap["app_data"] = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);

                BuildTree();

                //Move over external players from old config file
                var oldConfig = CommonConfigData.FromFile(Path.Combine(oldPathMap["AppConfigPath"], "MediaBrowserXml.config"));
                if (oldConfig != null && _config != null)
                {
                    _config.ExternalPlayers = oldConfig.ExternalPlayers;
                }

                //And Plugins that work
                //foreach (var dll in knownCompatibleDlls)
                //{
                //    CopyFile(Path.Combine(oldPathMap["AppPluginPath"], dll), Path.Combine(ApplicationPaths.AppPluginPath, dll));
                //}

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
