using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Threading;

namespace MediaBrowser.Library.Filesystem
{
    public class MBDirectoryWatcher : IDisposable
    {        
        private List<FileSystemWatcher> fileSystemWatchers = null;
        private Timer initialTimer;
        private Timer secondaryTimer;
        private System.DateTime lastRefresh = DateTime.MinValue;
        private string[] watchedFolders;

        private Folder folder;
        private string lastChangedDirectory = "Nothing";
        private string lastRefreshedDirectory = "Nothing";

        public MBDirectoryWatcher(Folder aFolder, bool watchChanges)
        {
            lastRefresh = System.DateTime.Now.AddMilliseconds(-60000); //initialize this
            this.folder = aFolder;
            IFolderMediaLocation location = folder.FolderMediaLocation;
            if (location is VirtualFolderMediaLocation)
            {
                //virtual folder
                this.watchedFolders = ((VirtualFolderMediaLocation)location).VirtualFolder.Folders.ToArray();
            }
            else
            {
                if (location != null)
                {
                    //regular folder
                    if (Directory.Exists(location.Path))
                    {
                        this.watchedFolders = new string[] { location.Path };
                    }
                    else
                    {
                        this.watchedFolders = new string[0];
                        Logger.ReportInfo("Cannot watch non-folder location " + aFolder.Name);
                    }

                }
                else
                {
                    Logger.ReportInfo("Cannot watch non-folder location " + aFolder.Name);
                    return;
                }
            }

            this.fileSystemWatchers = new List<FileSystemWatcher>();
            InitFileSystemWatcher(this.watchedFolders, watchChanges);
            Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => { InitTimers(); }); //timers only on app thread
        }

        ~MBDirectoryWatcher()
        {
            Dispose();
        }

        public void Dispose()
        {
            Logger.ReportInfo("Disposing MBDirectoryWatcher.");

            if (initialTimer != null)
                initialTimer.Enabled = false;

            initialTimer = null;

            if (fileSystemWatchers != null)
            {
                foreach (var watcher in fileSystemWatchers)
                {
                    watcher.EnableRaisingEvents = false;
                }
            }

            fileSystemWatchers = null;
            GC.SuppressFinalize(this);
        }

        private void InitTimers()
        {
            //when a file event first occurs we will wait five seconds for it to complete before doing our refresh
            this.initialTimer = new Timer();
            this.initialTimer.Enabled = false;
            this.initialTimer.Tick += new EventHandler(InitialTimer_Timeout);
            this.initialTimer.Interval =30000; // 30 seconds            

            //after that, if events are still occurring wait 90 seconds so we don't continually refresh during long file operations
            this.secondaryTimer = new Timer();
            this.secondaryTimer.Enabled = false;
            this.secondaryTimer.Tick += new EventHandler(SecondaryTimer_Timeout);
            this.secondaryTimer.Interval = 120000; // 120 seconds            
        }        

        private void InitFileSystemWatcher(string[] watchedFolders, bool watchChanges)
        {
            foreach (string folder in watchedFolders)
            {
                try
                {
                    FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(folder,"*.*");
                    fileSystemWatcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.DirectoryName | NotifyFilters.FileName;
                    fileSystemWatcher.IncludeSubdirectories = true;
                    if (watchChanges) // we will only watch changes in special situations (startup folder)
                        fileSystemWatcher.Changed += new FileSystemEventHandler(WatchedFolderChanged); 
                    fileSystemWatcher.Created += new FileSystemEventHandler(WatchedFolderCreation);
                    fileSystemWatcher.Deleted += new FileSystemEventHandler(WatchedFolderDeletion);
                    fileSystemWatcher.Renamed += new RenamedEventHandler(WatchedFolderRename);
                    fileSystemWatcher.EnableRaisingEvents = true;

                    this.fileSystemWatchers.Add(fileSystemWatcher);
                    Logger.ReportInfo("Watching folder " + folder + " for changes.");
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error adding " + folder + " to watched folders. ", ex);
                }
            }            
        }

        string[] ignores = new string[] { ".jpg", ".json", ".data", ".png", ".xml" };
        private void WatchedFolderUpdated(string FullPath, FileSystemEventArgs e)
        {
            if (!Kernel.IgnoreFileSystemMods)
            {
                try
                {
                    string ext = Path.GetExtension(e.Name).ToLower();
                    if (ignores.Contains(ext))
                    {
                        Logger.ReportVerbose("File Watcher ignoring change of type " + e.ChangeType + " to " + e.FullPath + ".");
                        return;
                    }
                    if (Directory.Exists(FullPath))
                    {
                        lastChangedDirectory = Path.GetDirectoryName(e.FullPath).ToLower();
                        lastChangedDirectory = lastChangedDirectory.Replace("\\metadata", ""); //if chg was to metadata directory for TV look at parent
                        if (System.DateTime.Now > lastRefresh.AddMilliseconds(120000))
                        {
                            //initial change event - wait 30 seconds and then update
                            this.initialTimer.Enabled = true;
                            lastRefresh = System.DateTime.Now;
                            Logger.ReportInfo("A change of type \"" + e.ChangeType.ToString() + "\" has occured in " + lastChangedDirectory);
                        }
                        else
                        {
                            //another change within 120 seconds kick off timer if not already
                            if (!secondaryTimer.Enabled)
                            {
                                this.secondaryTimer.Enabled = true;
                                Logger.ReportInfo("Another change within 120 seconds on " + FullPath);
                            }
                            else
                            {
                                //multiple changes - reset timer so that we wait 90 seconds after last change
                                this.secondaryTimer.Stop();
                                this.secondaryTimer.Enabled = false;
                                this.secondaryTimer.Enabled = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error adding VF to queue. ", ex);
                }
            }
            else
            {
                Logger.ReportVerbose("Ignoring change to " + FullPath);
            }
        }

        private void WatchedFolderChanged(object sender, FileSystemEventArgs e)
        {            
            WatchedFolderUpdated(((FileSystemWatcher)sender).Path, e);
        }

        private void WatchedFolderCreation(object sender, FileSystemEventArgs e)
        {
            WatchedFolderUpdated(((FileSystemWatcher)sender).Path, e);
        }

        private void WatchedFolderDeletion(object sender, FileSystemEventArgs e)
        {
            WatchedFolderUpdated(((FileSystemWatcher)sender).Path, e);
        }

        private void WatchedFolderRename(object sender, FileSystemEventArgs e)
        {
            WatchedFolderUpdated(((FileSystemWatcher)sender).Path, e);
        }

        private void InitialTimer_Timeout(object sender, EventArgs e)
        {
            this.initialTimer.Enabled = false;
            RefreshFolder();
        }

        private void SecondaryTimer_Timeout(object sender, EventArgs e)
        {
            this.secondaryTimer.Enabled = false;
            RefreshFolder();
        }

        private void RefreshFolder()
        {
            //if ((Application.CurrentInstance.CurrentFolder.Name == folder.Name || Application.CurrentInstance.CurrentItem.Name == folder.Name) &&
            //    lastChangedDirectory != lastRefreshedDirectory)
            {
                Async.Queue("File Watcher Refresher", () =>
                {
                    Logger.ReportInfo("Refreshing " + Application.CurrentInstance.CurrentFolder.Name + " due to change in " + folder.Name);
                    Logger.ReportInfo("  Directory changed was: " + lastChangedDirectory);
                    lastRefreshedDirectory = lastChangedDirectory;
                    folder.ValidateChildren();
                    foreach (var item in folder.RecursiveChildren)
                    {
                        if (item is Folder) (item as Folder).ValidateChildren();
                    }
                    //and go back through to find the item that actually changed and update its metadata - as long as it wasn't the root that changed
                    if (!isTopLevel(folder.FolderMediaLocation as VirtualFolderMediaLocation, lastChangedDirectory))
                    {
                        foreach (var item in folder.RecursiveChildren)
                        {
                            if (item.Path.ToLower().StartsWith(lastChangedDirectory))
                            {
                                Logger.ReportInfo("Refreshing metadata on " + item.Name + "(" + item.Path + ") because change was in " + lastChangedDirectory);
                                item.RefreshMetadata(Metadata.MetadataRefreshOptions.Force);
                                item.ReCacheAllImages();
                            }
                        }
                    }
                    else
                    {
                        Logger.ReportInfo("Not refreshing all items because change was at root level: " + lastChangedDirectory);
                    }
                    //Refresh whatever folder we are currently viewing plus all parents up the tree
                    FolderModel aFolder = Application.CurrentInstance.CurrentFolder;
                    while (aFolder != Application.CurrentInstance.RootFolderModel && aFolder != null)
                    {
                        aFolder.RefreshUI();
                        aFolder = aFolder.PhysicalParent;
                    }
                    Application.CurrentInstance.RootFolderModel.RefreshUI();
                });
            }
        }

        private bool isTopLevel(VirtualFolderMediaLocation location, string changed)
        {
            if (location != null)
            {
                foreach (var dir in location.VirtualFolder.Folders)
                {
                    if (dir.ToLower() == changed) return true;
                }
            }
            return false;
        }
        
    }
}
