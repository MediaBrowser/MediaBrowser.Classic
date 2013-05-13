using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Threading;
using System.Reflection;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library;


namespace MediaBrowser.Util
{
    // Updater class deals with checking for updates and downloading/installing them.
    public class Updater
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetWindowPlacement(IntPtr hWnd,
                           ref WINDOWPLACEMENT lpwndpl);
        private struct POINTAPI
        {
            public int x;
            public int y;
        }

        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINTAPI ptMinPosition;
            public POINTAPI ptMaxPosition;
            public RECT rcNormalPosition;
        }
        
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
                                //Minimize WMC
                                var mceWnd = FindWindow(null, "Windows Media Center");
                                var wp = new WINDOWPLACEMENT();
                                GetWindowPlacement(mceWnd, ref wp);
                                wp.showCmd = 2; // 1 - Normal; 2 - Minimize; 3 - Maximize;
                                SetWindowPlacement(mceWnd, ref wp);

                                var info = new ProcessStartInfo
                                               {
                                                   FileName = ApplicationPaths.UpdaterExecutableFile,
                                                   Arguments = "product=mbc class=" + Kernel.Instance.CommonConfigData.SystemUpdateClass + " admin=true",
                                                   Verb = "runas"
                                               };

                                Process.Start(info);
                                Application.CurrentInstance.Close();
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
        /// Don't try and update from here just display messages and return a bool so we can set an attribute.
        /// </summary>
        public bool PluginUpdatesAvailable()
        {
            var availablePlugins = Kernel.Instance.GetAvailablePlugins().ToList();
            bool updatesAvailable = false;
            Logger.ReportInfo("Checking for Plugin Updates...");

            foreach (var plugin in Kernel.Instance.Plugins)
            {
                var found = availablePlugins.Find(remote => remote.Name == plugin.Name);
                if (found != null)
                {
                    if (found.Version > plugin.Version && found.RequiredMBVersion <= Kernel.Instance.Version)
                    {
                        //newer one available - alert and set our bool
                        Application.CurrentInstance.Information.AddInformationString(string.Format(Application.CurrentInstance.StringData("PluginUpdateProf"), plugin.Name));
                        Logger.ReportInfo("Plugin " + plugin.Name + " (version " + plugin.Version + ") has update to Version " + found.Version + " Available.");
                        updatesAvailable = true;
                    }
                }
            }
            if (!updatesAvailable) Application.CurrentInstance.Information.AddInformationString(Application.CurrentInstance.StringData("NoPluginUpdateProf"));
            return updatesAvailable;
        }

    }
}
