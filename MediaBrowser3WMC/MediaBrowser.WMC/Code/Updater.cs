using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using System.Diagnostics;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.Hosting;
using Microsoft.MediaCenter.UI;
using MediaBrowser;
using System.Reflection;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library;
using MediaBrowser.LibraryManagement;


// XML File structure
/*
 
 <Config> 
    <Beta url="" version=""/> 
    <Release url="" version="">
 </Config>
 
 
 */

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

        // private members.
        private string remoteFile;
        private string localFile;
        private System.Version newVersion;

        const int UPDATE_CHECK_INTERVAL_DAYS = 2;

        // This should be replaced with the real location of the version info XML.
        private const string infoURL = "http://www.mediabrowser.tv/version-info.xml?key={0}&os={1}&mem={2}&mac={3}&mbver={4}";

        // Blocking call to check the XML file up in the cloud to see if we need an update.
        // This is really meant to be called as its own thread.
        public void CheckForUpdate()
        {
            if (string.IsNullOrEmpty(Config.Instance.SupporterKey))
            {
                return;
            }

            if ((DateTime.Now - Kernel.Instance.ConfigData.LastAutoUpdateCheck).TotalDays > 2)
            {
                Kernel.Instance.ConfigData.LastAutoUpdateCheck = DateTime.Now;
                Kernel.Instance.ConfigData.Save();
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                string url = Kernel.Instance.ConfigData.SendStats ?
                    string.Format(infoURL, Config.Instance.SupporterKey,Kernel.isVista ? "Vista" : "Win7",Helper.GetPhysicalMemory(), Helper.GetMACAddress(),Kernel.Instance.VersionStr) :
                    string.Format(infoURL, Config.Instance.SupporterKey,null,null,null,null);

                doc.Load(new XmlTextReader(url));
                XmlNode node;

                if (appRef.Config.EnableBetas)
                {
                    node = doc.SelectSingleNode("/Config/Beta");
                }
                else
                {
                    node = doc.SelectSingleNode("/Config/Release");
                }

                newVersion = new System.Version(node.Attributes["version"].Value);
                remoteFile = node.Attributes["url"].Value;

                // Old -> start update
                if (CurrentVersion < newVersion)  //disable the automatic installer since this doesn't work very well with versions that require migration and configuration
                {
                    //if (Application.MediaCenterEnvironment.Capabilities.ContainsKey("Console"))
                    //{
                    //    // Prompt them if they want to update.
                    //    DialogResult reply = Application.DisplayDialog(Application.CurrentInstance.StringData("UpdateMBDial"), Application.CurrentInstance.StringData("UpdateMBCapDial"), (DialogButtons)12 /* Yes, No */, 10);
                    //    if (reply == DialogResult.Yes)
                    //    {
                    //        // If they want it, download in the background and prompt when done.
                    //        DownloadUpdate();
                    //    }
                    //}
                    //else
                    {
                        // Let the user know about the update, but do nothing as we can't install from 
                        // an extender.
                        DialogResult reply = Application.DisplayDialog(Application.CurrentInstance.StringData("UpdateMBDial"), Application.CurrentInstance.StringData("UpdateMBCapDial"), (DialogButtons)1 /* OK */, 10);
                    }
                }
            }
            catch (Exception e)
            {
                // No biggie, just return out.
                Logger.ReportException("Error attempting to check for an update to Media Browser", e);
            }

        }

        // Downloads the update and stores the location.
        private void DownloadUpdate()
        {

            int bytesdone = 0;

            // Get a temp file name for the installer.  (This had better be an MSI file.)
            // Later we might make this smart about the extension of the web URL.
            localFile = System.IO.Path.GetTempFileName();
            localFile += ".msi";

            // Streams to read/write.
            Stream RStream = null;
            Stream LStream = null;

            // The respose of the web request.
            WebResponse response = null;
            try
            {
                // request the URL and get the response.
                WebRequest request = WebRequest.Create(remoteFile);
                if (request != null)
                {
                    response = request.GetResponse();
                    if (response != null)
                    {
                        // If we got a response lets KiB by KiB stream the 
                        // data into the temp file.
                        RStream = response.GetResponseStream();
                        LStream = File.Create(localFile);
                        byte[] buffer = new byte[1024];
                        int bytesRead;
                        do
                        {
                            bytesRead = RStream.Read(buffer, 0, buffer.Length);
                            LStream.Write(buffer, 0, bytesRead);
                            bytesdone += bytesRead;
                        }
                        while (bytesRead > 0);
                    }
                }
            }
            catch (Exception)
            {
                // We don't want error reporting here.
                bytesdone = 0;
            }
            finally
            {
                // Close out all of the streams.
                if (response != null)
                    response.Close();
                if (RStream != null)
                    RStream.Close();
                if (LStream != null)
                    LStream.Close();
            }

            if (bytesdone > 0)
            {
                // If we got it all, lets process the completed download.
                DownloadComplete();
            }
            else
            {
                // Otherwise let them know the download didn't work and they should just keep using VB.
                DialogResult reply = Application.DisplayDialog(Application.CurrentInstance.StringData("DLUpdateFailDial"),
                    Application.CurrentInstance.StringData("DLUpdateFailCapDial"), DialogButtons.Ok, 10);
            }
        }

        // Process the completed update download.
        public void DownloadComplete()
        {
            // Let them know we will be closing VB then restarting it.
            DialogResult reply = Application.DisplayDialog(Application.CurrentInstance.StringData("UpdateSuccessDial"),
                Application.CurrentInstance.StringData("UpdateSuccessCapDial"), DialogButtons.Ok, 10);

            //shut down the service
            MBServiceController.SendCommandToService(IPCCommands.Shutdown);

            // put together a batch file to execute the installer in silent mode and restart VB.
            string updateBat = "msiexec.exe /qb /i \"" + localFile + "\"\n";
            string windir = Environment.GetEnvironmentVariable("windir");
            updateBat += Path.Combine(windir, "ehome\\ehshell /entrypoint:{CE32C570-4BEC-4aeb-AD1D-CF47B91DE0B2}\\{FC9ABCCC-36CB-47ac-8BAB-03E8EF5F6F22}");
            string filename = System.IO.Path.GetTempFileName();
            filename += ".bat";
            System.IO.File.WriteAllText(filename, updateBat);

            // Start the batch file minimized so they don't notice.
            Process toDo = new Process();
            toDo.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            toDo.StartInfo.FileName = filename;

            toDo.Start();

            // Once we start the process we can kill the VB application.
            AddInHost context = AddInHost.Current;
            context.ApplicationContext.CloseApplication();

        }

        /// <summary>
        /// Check our installed plugins against the available ones to see if there are updated versions available
        /// Don't try and update from here just display messages and return a bool so we can set an attribute.
        /// </summary>
        public bool PluginUpdatesAvailable()
        {
            List<IPlugin> availablePlugins = (List<IPlugin>)PluginSourceCollection.Instance.AvailablePlugins;
            bool updatesAvailable = false;
            Logger.ReportInfo("Checking for Plugin Updates...");

            foreach (IPlugin plugin in Kernel.Instance.Plugins)
            {
                IPlugin found = availablePlugins.Find(remote => remote.Name == plugin.Name);
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
