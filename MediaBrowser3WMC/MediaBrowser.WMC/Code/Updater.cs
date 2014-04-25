using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly Application _appRef;

        // Constructor.
        public Updater(Application appRef)
        {
            this._appRef = appRef;
        }

        public static System.Version CurrentVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
        }

        protected string UpdateLogFileName = Path.Combine(ApplicationPaths.AppProgramPath, "MBCUpdate.log");
        public void WriteToUpdateLog(string line)
        {
            try
            {
                File.AppendAllText(UpdateLogFileName,  DateTime.Now + " " + line + "\r\n");
            }
            catch (Exception e)
            {
                Logger.ReportException("Error writing to update log {0}", e, line);
            }
        }

        public void ClearUpdateLog()
        {
            try
            {
                File.Delete(UpdateLogFileName);
            }
            catch (Exception e)
            {
                Logger.ReportException("Error clearing update log", e);
            }
        }

        public string UpdateLogText
        {
            get
            {
                try
                {
                    return String.Join("\n", File.ReadAllLines(UpdateLogFileName));
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error retrieving update log", e);
                    return "";
                }
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
                        WriteToUpdateLog("Updating MBC to new version "+newVersion.versionStr);
                        if (_appRef.YesNoBox(string.Format("Version {0} ({1}) of MB Classic available.  Update now?", newVersion.versionStr, newVersion.classification)) == "Y")
                        {
                            _appRef.MessageBox("MB Classic will now exit to update.  It will restart when the update is complete.");
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
                                Async.Queue("error", () => _appRef.MessageBox("Error attempting to update.  Please update manually."));
                            }
                        }
                        else
                        {
                            Logger.ReportVerbose("Not updating.  User refused or timed out.");
                        }

                    }
                    else
                    {
                        _appRef.Information.AddInformationString("MB Classic is up to date");
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
            if (_appRef.InstalledPluginsCollection.Items.Any(i => i.UpdateAvailable))
            {
                if (_appRef.YesNoBox(MediaBrowser.Library.Localization.LocalizedStrings.Instance.GetString("PluginUpdatesAvailQ")) == "Y")
                {
                    _appRef.ConfigPanelIndex = 3;
                    _appRef.OpenConfiguration(true);
                    Async.Queue("Panel Reset", () => { _appRef.ConfigPanelIndex = 0; }, 1000);
                    
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

        public bool UpdateAllPlugins(PluginItemCollection installedPlugins, bool silent = false)
        {
            var success = false;
            foreach (var plugin in installedPlugins.Items.Where(p => p.UpdateAvailable))
            {
                if (!silent) _appRef.ProgressBox("Updating " + plugin.Name + "...");
                if (InstallPlugin(new RemotePlugin
                                      {
                                          SourceFilename = plugin.ValidVersions.OrderBy(v => v.version).Last().sourceUrl,
                                          Filename = plugin.TargetFilename
                                      }))
                {
                    plugin.UpdateAvailable = false;
                    plugin.UpdatePending = true;
                    success = true;
                    WriteToUpdateLog(String.Format("Plug-in {0} Updated to version {1} by user {2}", plugin.Name, plugin.ValidVersions.OrderBy(v => v.version).Last().versionStr, Kernel.CurrentUser.Name));
                }
                if (!silent) _appRef.ShowMessage = false;
            }

            installedPlugins.ResetUpdatesAvailable();
            return success;
        }

        public bool UpdatePlugin(PluginItem plugin, string operation = "Updating")
        {
            var success = false;

                _appRef.ProgressBox(operation + " " + plugin.Name + "...");
                if (InstallPlugin(new RemotePlugin
                                      {
                                          SourceFilename = plugin.ValidVersions.OrderBy(v => v.version).Last().sourceUrl,
                                          Filename = plugin.TargetFilename
                                      }))
                {
                    plugin.UpdateAvailable = false;
                    plugin.UpdatePending = true;
                    plugin.NotifyPropertiesChanged();
                    WriteToUpdateLog(String.Format("Plug-in {0} Updated to version {1} by user {2}", plugin.Name, plugin.ValidVersions.OrderBy(v => v.version).Last().versionStr, Kernel.CurrentUser.Name));
                    success = true;
                }
                _appRef.ShowMessage = false;
                

            _appRef.InstalledPluginsCollection.ResetUpdatesAvailable();
            return success;
        }
        
        public bool InstallPlugin(RemotePlugin plugin)
        {
            _installInProgress = true;
            _sucessfulUpdate = false;
            Kernel.Instance.InstallPlugin(plugin.SourceFilename, plugin.Filename, null, PluginInstallFinish, PluginInstallError );
            while (_installInProgress)
            {
                Thread.Sleep(250);
            }
            return _sucessfulUpdate;
        }

        private void PluginInstallFinish()
        {
            _installInProgress = false;
            _sucessfulUpdate = true;
        }

        private void PluginInstallError(WebException ex)
        {
            Logger.ReportException("Error installing plug-in update.",ex);
            _appRef.MessageBox("Error Installing.");
            _installInProgress = false;
        }

    }
}
