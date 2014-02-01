using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library.Threading;
using System.Reflection;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library;


namespace MediaBrowser.Util
{
    // Updater class deals with checking for updates and downloading/installing them.
    public class Updater
    {
        
        // Reference back to the application for displaying dialog (thread safe).
        private Application appRef;

        // Constructor.
        public Updater(Application appRef)
        {
            this.appRef = appRef;
        }

        public static System.Version CurrentVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
        }


        // Blocking call to check the mb admin server to see if we need an update.
        // This must be called as its own thread.
        public void CheckForUpdate()
        {

            if ((DateTime.Now - Kernel.Instance.ConfigData.LastAutoUpdateCheck).TotalDays > 2)
            {
                Kernel.Instance.ConfigData.LastAutoUpdateCheck = DateTime.Now;
                Kernel.Instance.ConfigData.Save();
            }

            try
            {
                var systemInfo = Kernel.ApiClient.GetSystemInfo();
                var serverVersion = new System.Version(systemInfo.Version ?? "3.0");
                

                var mbClassic = Kernel.ApiClient.GetPackageInfo("MBClassic");
                if (mbClassic != null)
                {
                    var newVersion = mbClassic.versions.FirstOrDefault(v => v.classification <= Kernel.Instance.CommonConfigData.SystemUpdateClass 
                        && new System.Version(!string.IsNullOrEmpty(v.requiredVersionStr) ? v.requiredVersionStr : "3.0") <= serverVersion && v.version > Kernel.Instance.Version);
                    if (newVersion != null)
                    {
                        Logger.ReportVerbose("New version {0} found.",newVersion.versionStr);
                        if (Application.CurrentInstance.YesNoBox(string.Format("Version {0} ({1}) of MB Classic available.  Update now?", newVersion.versionStr, newVersion.classification)) == "Y")
                        {
                            Application.CurrentInstance.MessageBox("MB Classic will now exit to update.  It will restart when the update is complete.");
                            //Kick off the installer and shut us down
                            try
                            {
                                Logger.ReportVerbose("Updating to version {0}.",newVersion.versionStr);

                                var info = new ProcessStartInfo
                                               {
                                                   FileName = ApplicationPaths.UpdaterExecutableFile,
                                                   Arguments = "product=mbc class=" + Kernel.Instance.CommonConfigData.SystemUpdateClass + " admin=true",
                                                   Verb = "runas"
                                               };

                                Process.Start(info);

                                //And close WMC
                                var killWmc = new Process();
                                killWmc.StartInfo.CreateNoWindow = true;
                                killWmc.StartInfo.FileName = "Taskkill";
                                killWmc.StartInfo.Arguments = "/F /im ehshell.exe";
                                killWmc.Start();
                            }
                            catch (Exception e)
                            {
                                Logger.ReportException("Error attempting to update.",e);
                                Async.Queue("error", () => Application.CurrentInstance.MessageBox("Error attempting to update.  Please update manually."));
                            }
                        }
                        else
                        {
                            Logger.ReportVerbose("Not updating.  User refused or timed out.");
                        }

                    }
                    else
                    {
                        appRef.Information.AddInformationString("MB Classic is up to date");
                        Logger.ReportInfo("==== MB Classic is up to date.");
                    }
                }
            }
            catch (Exception e)
            {
                // No biggie, just return out.
                Logger.ReportException("Error attempting to check for an update to Media Browser Classic", e);
            }

        }


        /// <summary>
        /// Check our installed plugins against the available ones to see if there are updated versions available
        /// Allow in-place update if there are.
        /// </summary>
        public bool PluginUpdatesAvailable()
        {
            Logger.ReportInfo("Checking for Plugin Updates...");
            if (Application.CurrentInstance.InstalledPluginsCollection.Items.Any(i => i.UpdateAvailable))
            {
                if (Application.CurrentInstance.YesNoBox(MediaBrowser.Library.Localization.LocalizedStrings.Instance.GetString("PluginUpdatesAvailQ")) == "Y")
                {
                    Application.CurrentInstance.ConfigPanelIndex = 3;
                    Application.CurrentInstance.OpenConfiguration(true);
                    Async.Queue("Panel Reset", () => { Application.CurrentInstance.ConfigPanelIndex = 0; }, 1000);
                    
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        private bool _installInProgress = false;
        private bool _sucessfulUpdate = false;

        public void InstallPlugin(RemotePlugin plugin)
        {
            _installInProgress = true;
            Kernel.Instance.InstallPlugin(plugin.SourceFilename, plugin.Filename, null, PluginInstallFinish, PluginInstallError );
            while (_installInProgress)
            {
                Thread.Sleep(250);
            }
        }

        private void PluginInstallFinish()
        {
            _installInProgress = false;
            _sucessfulUpdate = true;
        }

        private void PluginInstallError(WebException ex)
        {
            Logger.ReportException("Error installing plug-in update.",ex);
            Application.CurrentInstance.MessageBox("Error Installing.  Please try through Configurator.");
            _installInProgress = false;
        }

    }
}
