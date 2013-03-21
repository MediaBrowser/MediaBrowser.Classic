using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library;
using System.ComponentModel;
using System.Windows;
using System.IO;
using MediaBrowser.Library.Logging;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Threading;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Configuration;

namespace Configurator.Code {
    public class PluginManager {

        public bool PluginsLoaded = false;

        static PluginManager instance; 
        public static PluginManager Instance {
            get {
                return instance;
            }
        }

        internal void Init() {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject())) {
                sources = PluginSourceCollection.Instance;
                RefreshInstalledPlugins();
                RefreshAvailablePlugins();
                RefreshBackedUpPlugins();
                PluginsLoaded = true; //safe to go see if we have updates
            }
        }

        PluginCollection installedPlugins = new PluginCollection();
        PluginCollection availablePlugins = new PluginCollection();
        PluginCollection backedUpPlugins = new PluginCollection();
        PluginSourceCollection sources;
        string backupDir = Path.Combine(ApplicationPaths.AppPluginPath, "Backup");

        Dictionary<string, System.Version> latestVersions = new Dictionary<string, System.Version>();

        //any of these plugins with older versions than defined here are incompatable with this version
        public static Dictionary<string, System.Version> RequiredVersions = new Dictionary<string, System.Version>() {
                {"coverart",new System.Version(3,0,0,0)},
                {"mediainfo provider", new System.Version(1,3,0)},
                {"gametime", new System.Version(6,0,0)},
                {"high quality thumbnails", new System.Version(1,2,0)},
                {"media browser trailers", new System.Version(1,3,3)},
                {"media browser intros", new System.Version(1,1,2)},
                {"diamond theme", new System.Version(0,3,6,0)},
                {"dvr-ms and wtv metadata", new System.Version(1,0,5,0)},
                {"ascendancy theme", new System.Version(2,0,0,0)},
                {"centrality theme", new System.Version(2,0,0,0)},
                {"harmony theme", new System.Version(2,0,0,0)},
                {"imperium theme", new System.Version(2,0,0,0)},
                {"kismet theme", new System.Version(2,0,0,0)},
                {"maelstrom theme", new System.Version(2,0,0,0)},
                {"regency theme", new System.Version(1,0,0,0)},
                {"supremacy theme", new System.Version(1,0,0,0)},
                {"vanilla theme", new System.Version(3,0,0,0)},
                {"pearl theme", new System.Version(1,0,7,1)},
                {"sapphire theme", new System.Version(1,0,6,2)},
                {"lotus theme", new System.Version(1,0,6,2)},
                {"jade theme", new System.Version(1,0,6,2)},
                {"neo theme", new System.Version(1,0,7,0)},
                {"follw.it", new System.Version(1,0,2,0)},
                {"traktmb", new System.Version(0,9,6,17)},
                {"storageviewer", new System.Version(0,0,4,10)},
                {"gamebrowser", new System.Version(1,9,0,0)},
                {"subdued theme", new System.Version(2,9,0,1)},
                {"music support", new System.Version(2,1)},
                {"mbtv", new System.Version(1,4,9,10)},
            };

        public PluginManager()
        {
            instance = this;
        }

        public void RefreshAvailablePlugins() {

            if (Application.Current.Dispatcher.Thread != System.Threading.Thread.CurrentThread) {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, (System.Windows.Forms.MethodInvoker)RefreshAvailablePlugins);
                return;
            }

            availablePlugins.Clear();
            latestVersions.Clear();

            foreach (var plugin in sources.AvailablePlugins.OrderBy(p => p.Name))
            {
                IPlugin ip = this.InstalledPlugins.Find(plugin);
                if (ip != null)
                {
                    if (ip.Version == plugin.Version)
                        plugin.Installed = true;

                    //we need to set this in the installed plugin here because we didn't have this info the first time we refreshed
                    ip.UpdateAvail |= (plugin.Version > ip.Version && Kernel.Instance.Version >= plugin.RequiredMBVersion);
                }
                if (availablePlugins.Find(plugin, plugin.Version) == null) //ignore dups
                {
                    availablePlugins.Add(plugin);
                    try
                    {
                        string key = plugin.Name + System.IO.Path.GetFileName(plugin.Filename);
                        if (latestVersions.ContainsKey(key))
                        {
                            if (plugin.Version > latestVersions[key]) latestVersions[key] = plugin.Version;
                        }
                        else latestVersions.Add(key, plugin.Version);
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Cannot add plugin latest version. Probably two references to same plugin.", e);
                    }
                }
                else
                {
                    Logger.ReportWarning("Duplicate plugin version in main repo: " + plugin.Name + " v" + plugin.Version);
                }
            }
            //now go back through and indicate which one is the latest version
            foreach (var plugin in availablePlugins)
            {
                string key = plugin.Name + System.IO.Path.GetFileName(plugin.Filename);
                System.Version v;
                if (latestVersions.TryGetValue(key, out v))
                {
                    plugin.IsLatestVersion = (plugin.Version == v) ? true : false;
                }
            }
        }

        private void RefreshBackedUpPlugins()
        {
            backedUpPlugins.Clear();
            if (Directory.Exists(backupDir))
            {
                foreach (var file in Directory.GetFiles(backupDir))
                {
                    if (file.ToLower().EndsWith(".dll"))
                    {
                        try
                        {
                            backedUpPlugins.Add(Plugin.FromFile(file, true));
                        }
                        catch (Exception e)
                        {
                            Logger.ReportException("Error attempting to load " + file + " as plug-in.", e);
                        }
                    }
                }
            }
        }

        public void InstallPlugin(IPlugin plugin,
          MediaBrowser.Library.Network.WebDownload.PluginInstallUpdateCB updateCB,
          MediaBrowser.Library.Network.WebDownload.PluginInstallFinishCB doneCB,
          MediaBrowser.Library.Network.WebDownload.PluginInstallErrorCB errorCB) {
            //taking this check out for now - it's just too cumbersome to have to re-compile all plug-ins to change this -ebr
            //if (plugin.TestedMBVersion < Kernel.Instance.Version) {
            //    var dlgResult = MessageBox.Show("Warning - " + plugin.Name + " has not been tested with your version of MediaBrowser. \n\nInstall anyway?", "Version not Tested", MessageBoxButton.YesNo);
            //    if (dlgResult == MessageBoxResult.No) {
            //        doneCB();
            //        return;
            //    }
            //}

            if (BackupPlugin(plugin)) Logger.ReportInfo("Plugin "+plugin.Name+" v"+plugin.Version+" backed up.");

            if (plugin is RemotePlugin) {
                try {
                    Kernel.Instance.InstallPlugin((plugin as RemotePlugin).BaseUrl + "\\" + (plugin as RemotePlugin).SourceFilename, plugin.Filename, updateCB, doneCB, errorCB);
                }
                catch (Exception ex) {
                    MessageBox.Show("Cannot Install Plugin.  If MediaBrowser is running, please close it and try again.\n" + ex.Message, "Install Error");
                    doneCB();
                }
            }
            else {
                var local = plugin as Plugin;
                Debug.Assert(plugin != null);
                try {
                    Kernel.Instance.InstallPlugin(local.Filename, null, null, null);
                }
                catch (Exception ex) {
                    MessageBox.Show("Cannot Install Plugin.  If MediaBrowser is running, please close it and try again.\n" + ex.Message, "Install Error");
                    doneCB();
                }
            }

        }

        private bool BackupPlugin(IPlugin plugin)
        {
            //Backup current version if installed and different from the one we are installing
            try
            {
                IPlugin ip = InstalledPlugins.Find(plugin);
                if (ip != null && ip.Version != plugin.Version)
                {
                    if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                    string oldPluginPath = Path.Combine(ApplicationPaths.AppPluginPath, plugin.Filename);
                    string bpPath = Path.Combine(backupDir, plugin.Filename);
                    File.Copy(oldPluginPath,bpPath ,true);
                    IPlugin bp = backedUpPlugins.Find(plugin);
                    if (bp != null) backedUpPlugins.Remove(bp);
                    backedUpPlugins.Add(Plugin.FromFile(bpPath,false));
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error trying to backup current plugin", e);
            }
            return false;
        }

        public bool RollbackPlugin(IPlugin plugin)
        {
            try
            {
                string source = Path.Combine(backupDir, plugin.Filename);
                if (File.Exists(source))
                {
                    string target = Path.Combine(ApplicationPaths.AppPluginPath, plugin.Filename);
                    Kernel.Instance.InstallPlugin(source, null, null, null);
                    MainWindow.Instance.KernelModified = true;
                    UpdateAvailableAttributes(plugin, true);
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error attempting to rollback plugin " + plugin.Name, e);
            }

            return false;
        }

        public void UpdateAvailableAttributes(IPlugin plugin, bool installed)
        {
            //first find any version of this that was installed and un-mark it
            IPlugin ip = this.AvailablePlugins.Find(plugin, true);
            if (ip != null) ip.Installed = false; //reset
            //now go find the one we just installed and mark it
            ip = this.AvailablePlugins.Find(plugin, plugin.Version);
            if (ip != null) ip.Installed = installed;
        }

        public void RefreshInstalledPlugins() {

            if (Application.Current.Dispatcher.Thread != System.Threading.Thread.CurrentThread) {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,(System.Windows.Forms.MethodInvoker)RefreshInstalledPlugins);
                return;
            }

            installedPlugins.Clear();
            foreach (var plugin in Kernel.Instance.Plugins.OrderBy(p => p.Name)) {
                System.Version v = GetLatestVersion(plugin);
                System.Version rv = plugin.RequiredMBVersion;
                if (v != null)
                {
                    plugin.UpdateAvail = (v > plugin.Version && rv <= Kernel.Instance.Version);
                    UpdateAvailableAttributes(plugin, true);
                }
                plugin.Installed = true;
                installedPlugins.Add(plugin);
            }
        }

        public void RemovePlugin(IPlugin plugin) {
            try {
                Kernel.Instance.DeletePlugin(plugin);
                installedPlugins.Remove(plugin);
                MainWindow.Instance.KernelModified = true;
            } catch (Exception e) {
                MessageBox.Show("Failed to delete the plugin.  If MediaBrowser is running, Please close it and try again.");
                Logger.ReportException("Failed to delete plugin", e);
            }
        }

        public bool UpgradesAvailable()
        {
            foreach (IPlugin plugin in installedPlugins)
            {
                if (plugin.UpdateAvail) return true;
            }
            return false;
        }

        public System.Version GetLatestVersion(IPlugin plugin) {
            System.Version version;
            latestVersions.TryGetValue(plugin.Name+plugin.Filename, out version);
            return version;
        }

        public System.Version GetBackedUpVersion(IPlugin plugin)
        {
            System.Version version = null;
            IPlugin p = backedUpPlugins.Find(plugin);
            if (p != null) version = p.Version;
            return version;
        }

        public PluginCollection InstalledPlugins {
            get {
                return installedPlugins;
            } 
        }

        public PluginCollection AvailablePlugins
        {
            get
            {
                return availablePlugins;
            }
        }
        public PluginCollection BackedUpPlugins
        {
            get
            {
                return backedUpPlugins;
            }
        }
        public PluginSourceCollection Sources
        {
            get
            {
                return sources;
            }
        }
       
    }
}
