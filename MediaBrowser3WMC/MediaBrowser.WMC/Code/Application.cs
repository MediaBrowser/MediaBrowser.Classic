using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MediaBrowser.ApiInteraction;
using MediaBrowser.ApiInteraction.WebSocket;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Events;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Input;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Localization;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Metadata;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Playables;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.UI;
using MediaBrowser.Library.Util;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Session;
using MediaBrowser.Util;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.AddIn;
using Microsoft.MediaCenter.UI;
using AddInHost = Microsoft.MediaCenter.Hosting.AddInHost;
using MediaType = Microsoft.MediaCenter.MediaType;
using Timer = Microsoft.MediaCenter.UI.Timer;

namespace MediaBrowser
{

    public class Application : ModelItem
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // reset config stuff - be sure and grab the current config file
                var config = ConfigData.FromFile(ApplicationPaths.ConfigFile);
                config.InvalidateRecentLists = false;
                config.Save();
            }
            base.Dispose(disposing);
        }

        public Config Config
        {
            get
            {
                return Config.Instance;
            }
        }

        public static Application CurrentInstance
        {
            get { return singleApplicationInstance; }
        }

        public string StringData(string name)
        {
            return Kernel.Instance.GetString(name);
        }

        public MBPropertySet LocalStrings //used to access our localized strings from mcml
        {
            get
            {
                return Kernel.Instance.LocalStrings;
            }
        }

        private static Application singleApplicationInstance;
        private MyHistoryOrientedPageSession session;
        private static object syncObj = new object();
        private bool navigatingForward;
        private BasePlaybackController currentPlaybackController = null;
        private static Timer ScreenSaverTimer;
        //tracks whether to show recently added or watched items
        public string RecentItemOption { get { return Config.Instance.RecentItemOption; } set { Config.Instance.RecentItemOption = value; Kernel.Instance.ConfigData.RecentItemOption = value; } }
        private bool pluginUpdatesAvailable = false;
        private bool systemUpdateCheckInProgress = false;
        public System.Drawing.Bitmap ExtSplashBmp;
        private Item lastPlayed;
        private Updater Updater;

        #region CurrentItemChanged EventHandler
        volatile EventHandler<GenericEventArgs<Item>> _CurrentItemChanged;
        /// <summary>
        /// Fires whenever CurrentItem changes
        /// </summary>
        public event EventHandler<GenericEventArgs<Item>> CurrentItemChanged
        {
            add
            {
                _CurrentItemChanged += value;
            }
            remove
            {
                _CurrentItemChanged -= value;
            }
        }

        internal void OnCurrentItemChanged()
        {
            FirePropertyChanged("CurrentItem");

            // send context message
            Async.Queue("Context", () =>
            {
                try
                {
                    WebSocket.SendContextMessage(CurrentItem.BaseItem.GetType().Name, CurrentItem.BaseItem.ApiId, CurrentItem.Name);
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error sending context message", e);
                }
            });

            if (_CurrentItemChanged != null)
            {
                Async.Queue("OnCurrentItemChanged", () => _CurrentItemChanged(this, new GenericEventArgs<Item>() { Item = CurrentItem })); 
            }
        }
        #endregion

        #region NavigatedInto EventHandler
        volatile EventHandler<GenericEventArgs<Item>> _NavigationInto;
        /// <summary>
        /// Fires whenever an Item is navigated into
        /// </summary>
        public event EventHandler<GenericEventArgs<Item>> NavigationInto
        {
            add
            {
                _NavigationInto += value;
            }
            remove
            {
                _NavigationInto -= value;
            }
        }

        internal void OnNavigationInto(Item item)
        {
            if (_NavigationInto != null)
            {
                Async.Queue("OnNavigationInto", () =>
                {
                    _NavigationInto(this, new GenericEventArgs<Item>() { Item = item });
                });
            }
        }
        #endregion

        #region PrePlayback EventHandler
        volatile EventHandler<GenericEventArgs<PlayableItem>> _PrePlayback;
        /// <summary>
        /// Fires whenever a PlayableItem is about to be played
        /// </summary>
        public event EventHandler<GenericEventArgs<PlayableItem>> PrePlayback
        {
            add
            {
                _PrePlayback += value;
            }
            remove
            {
                _PrePlayback -= value;
            }
        }

        private void OnPrePlayback(PlayableItem playableItem)
        {
            if (_PrePlayback != null)
            {
                try
                {
                    _PrePlayback(this, new GenericEventArgs<PlayableItem>() { Item = playableItem });
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Application.PrePlayback event listener had an error: ", ex);
                }
            }
            Async.Queue("IsPlayingVideo delay", () => { FirePropertyChanged("IsPlayingVideo"); FirePropertyChanged("IsPlaying"); }, 1500);
        }
        #endregion

        #region PlaybackFinished EventHandler
        volatile EventHandler<GenericEventArgs<PlayableItem>> _PlaybackFinished;
        /// <summary>
        /// Fires whenever a PlayableItem finishes playback
        /// </summary>
        public event EventHandler<GenericEventArgs<PlayableItem>> PlaybackFinished
        {
            add
            {
                _PlaybackFinished += value;
            }
            remove
            {
                _PlaybackFinished -= value;
            }
        }

        private void OnPlaybackFinished(PlayableItem playableItem)
        {
            if (_PlaybackFinished != null)
            {
                Async.Queue("OnPlaybackFinished", () => _PlaybackFinished(this, new GenericEventArgs<PlayableItem>() { Item = playableItem })); 
            }
            FirePropertyChanged("IsPlayingVideo");
            FirePropertyChanged("IsPlaying");
        }
        #endregion

        #region New Item Notification

        private bool _showNewItemPopout;
        public bool ShowNewItemPopout
        {
            get { return _showNewItemPopout; }
            set {if (_showNewItemPopout != value)
            {
                _showNewItemPopout = value;
                FirePropertyChanged("ShowNewItemPopout");
            }}
        }

        private Item _newItem;
        public Item NewItem
        {
            get { return _newItem; }
            set
            {
                if (_newItem != value)
                {
                    _newItem = value;
                    FirePropertyChanged("NewItem");
                }
            }
        }

        #endregion

        #region CustomDialogs
        private bool showMessage = false;
        public bool ShowMessage
        {
            get { return showMessage; }
            set
            {
                if (showMessage != value)
                {
                    showMessage = value;
                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => FirePropertyChanged("ShowMessage"));
                }
            }
        }

        public string MessageResponse { get; set; }
        private string messageText = "";
        public string MessageText
        {
            get
            {
                return messageText;
            }
            set
            {
                if (messageText != value)
                {
                    messageText = value;
                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => FirePropertyChanged("MessageText"));
                }
            }
        }

        private string messageUI = "resx://MediaBrowser/MediaBrowser.Resources/Message#MessageBox";
        public string MessageUI
        {
            get { return messageUI; }
            set
            {
                if (messageUI != value)
                {
                    messageUI = value;
                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => FirePropertyChanged("MessageUI"));
                }
            }
        }

        protected string MessageBox(string msg, bool modal, int timeout, string ui)
        {
            MessageUI = !string.IsNullOrEmpty(ui) ? ui : "resx://MediaBrowser/MediaBrowser.Resources/Message#MessageBox";
            MessageResponse = "";
            MessageText = msg;
            ShowMessage = true;
            if (timeout > 0)  //0 will leave it up
            {
                if (modal)
                {
                    //wait for response or timeout
                    WaitForMessage(timeout);
                }
                else
                {
                    //async the timeout
                    Async.Queue("Custom Msg", () => WaitForMessage(timeout));
                }
            }

            return MessageResponse;
        }

        protected void WaitForMessage(int timeout)
        {
            DateTime start = DateTime.Now;
            while (showMessage && (DateTime.Now - start).TotalMilliseconds < timeout)
            {
                System.Threading.Thread.Sleep(250);
            }

            ShowMessage = false;
        }

        public string MessageBox(string msg)
        {
            return MessageBox(msg, true, Config.DefaultMessageTimeout * 1000, null);
        }

        public string MessageBox(string msg, bool modal)
        {
            return MessageBox(msg, modal, Config.DefaultMessageTimeout * 1000, null);
        }

        public string MessageBox(string msg, bool modal, int timeout)
        {
            return MessageBox(msg, modal, timeout, null);
        }

        public string YesNoBox(string msg)
        {
            return MessageBox(msg, true, Config.DefaultMessageTimeout * 1000, "resx://MediaBrowser/MediaBrowser.Resources/Message#YesNoBox");
        }

        public void ProgressBox(string msg)
        {
            MessageBox(msg, false, 0, "resx://MediaBrowser/MediaBrowser.Resources/Message#ProgressBox");
        }

        #endregion

        #region Websocket
        public static ApiWebSocket WebSocket { get; private set; }

        private void BrowseRequest(object sender, BrowseRequestEventArgs args)
        {
            switch (args.Request.ItemType)
            {
                case "Genre":
                    Logger.ReportInfo("Navigating to genre {0} by request from remote client", args.Request.ItemName);
                    NavigateToGenre(args.Request.ItemName, CurrentItem);
                    break;
                case "Artist":
                case "Person":
                    var actor = Kernel.Instance.MB3ApiRepository.RetrievePerson(args.Request.ItemName);
                    if (actor != null)
                    {
                        Logger.ReportInfo("Navigating to person {0} by request from remote client", args.Request.ItemName);
                        NavigateToActor(ItemFactory.Instance.Create(actor));
                    }
                    else
                    {
                        Logger.ReportWarning("Unable to browse to person {0}", args.Request.ItemName);
                    }
                    break;
                case "Studio":
                    break;
                default:
                    var item = Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(args.Request.ItemId));
                    if (item != null)
                    {
                        Logger.ReportInfo("Navigating to {0} by request from remote client", item.Name);
                        Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>Navigate(ItemFactory.Instance.Create(item)));
                    }
                    else
                    {
                        Logger.ReportWarning("Unable to browse to item {0}", args.Request.ItemId);
                    }
                    break;
            }
        }

        private void PlayRequest(object sender, PlayRequestEventArgs args)
        {
            var item = Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(args.Request.ItemIds.FirstOrDefault() ?? ""));
                    if (item != null)
                    {
                        Logger.ReportInfo("Playing {0} by request from remote client", item.Name);
                        Play(ItemFactory.Instance.Create(item),false, args.Request.PlayCommand != PlayCommand.PlayNow, false, false, args.Request.StartPositionTicks ?? 0);
                    }
                    else
                    {
                        Logger.ReportWarning("Unable to play item {0}", args.Request.ItemIds.FirstOrDefault());
                    }
        }

        private void PlayStateRequest(object sender, PlaystateRequestEventArgs args)
        {
            if (currentPlaybackController == null)
            {
                Logger.ReportWarning("No playback in progress.  Cannot respond to playstate command {0}", args.Request.Command);
                return;
            }

            switch (args.Request.Command)
            {
                case PlaystateCommand.Pause:
                case PlaystateCommand.Unpause:
                    currentPlaybackController.Pause();
                    break;

                case PlaystateCommand.Stop:
                    currentPlaybackController.Stop();
                    break;

                case PlaystateCommand.Seek:
                    currentPlaybackController.Seek(args.Request.SeekPosition);
                    break;
            }
        }

        private void LibraryChanged(object sender, LibraryChangedEventArgs args)
        {
            Logger.ReportVerbose("Library Changed...");
            Logger.ReportVerbose("Folders Added to: {0} Items Added: {1} Items Removed: {2} Items Updated: {3}", args.UpdateInfo.FoldersAddedTo.Count, args.UpdateInfo.ItemsAdded.Count, args.UpdateInfo.ItemsRemoved.Count, args.UpdateInfo.ItemsUpdated.Count);
            var changedFolders = args.UpdateInfo.FoldersAddedTo.Concat(args.UpdateInfo.FoldersRemovedFrom).Select(id => Kernel.Instance.FindItem(id)).Where(folder => folder != null).Cast<Folder>().ToList();
            // Get folders that were reported to us
            foreach (var changedItem in changedFolders) Logger.ReportVerbose("Folder with changes is: {0}", changedItem.Name);

            var topFolders = new Dictionary<Guid, Folder>();

            //First get all the top folders of removed items
            foreach (var id in args.UpdateInfo.ItemsRemoved)
            {
                var removed = Kernel.Instance.FindItem(id);
                if (removed != null)
                {
                    var top = removed.TopParent;
                    if (top != null) topFolders[top.Id] = top;

                    // If our parent wasn't included in the updated list - refresh it
                    var parent = removed.Parent;
                    if (parent != null)
                    {
                        if (!args.UpdateInfo.ItemsUpdated.Contains(parent.Id)) parent.RefreshMetadata();
                    }
                }
            }

            // Get changed items and update them
            foreach (var id in args.UpdateInfo.ItemsUpdated)
            {
                var changedItem = Kernel.Instance.FindItem(id);
                if (changedItem != null)
                {
                    if (!args.UpdateInfo.ItemsUpdated.Contains(changedItem.Parent.Id))
                    {
                        Logger.ReportVerbose("Item changed is: {0}.  Refreshing.", changedItem.Name);
                        changedItem.RefreshMetadata();
                        // Add it's top parent so we can clear out recent lists if this was an added item
                        if (args.UpdateInfo.ItemsAdded.Contains(changedItem.Id))
                        {
                            var top = changedItem.TopParent;
                            if (top != null) topFolders[top.Id] = top;
                        }
                    }
                    else
                    {
                        Logger.ReportVerbose("Not refreshing {0} because parent was/will be.", changedItem.Name);
                    }

                }
                else
                {
                    Logger.ReportVerbose("Changed Item {0} is not loaded", id);
                }
            }

            //Now we can get all the top folders of added items and parents if they weren't refreshed already
            foreach (var id in args.UpdateInfo.ItemsAdded)
            {
                var added = Kernel.Instance.FindItem(id);
                if (added != null)
                {
                    var top = added.TopParent;
                    if (top != null) topFolders[top.Id] = top;

                    // If our parent wasn't included in the updated list - refresh it
                    var parent = added.Parent;
                    if (parent != null)
                    {
                        if (!args.UpdateInfo.ItemsUpdated.Contains(parent.Id)) parent.RefreshMetadata();
                    }
                }
            }

            //And reset the recent list for each top folder affected
            foreach (var top in topFolders.Values)
            {
                top.ResetQuickList();
                top.OnQuickListChanged(null);
            } 

            //Finally notify if enabled
            if (Config.ShowNewItemNotification)
            {
                var firstNew = Kernel.Instance.MB3ApiRepository.RetrieveItem(args.UpdateInfo.ItemsAdded.FirstOrDefault());
                if (firstNew != null)
                {
                    NewItem = ItemFactory.Instance.Create(firstNew);
                    ShowNewItemPopout = true;
                    Thread.Sleep(Config.NewItemNotificationDisplayTime * 1000);
                    ShowNewItemPopout = false;
                }
                
            }
        }



        #endregion

        public bool PluginUpdatesAvailable
        {
            get
            {
                return pluginUpdatesAvailable;
            }
            set
            {
                pluginUpdatesAvailable = value;
                FirePropertyChanged("PluginUpdatesAvailable");
            }
        }

        public bool SystemUpdateCheckInProgress
        {
            get
            {
                return systemUpdateCheckInProgress;
            }
            set
            {
                if (systemUpdateCheckInProgress != value)
                {
                    systemUpdateCheckInProgress = value;
                    FirePropertyChanged("SystemUpdateCheckInProgress");
                }
            }
        }

        private bool _ScreenSaverActive = false;

        public bool ScreenSaverActive
        {
            get { return _ScreenSaverActive; }
            set { if (_ScreenSaverActive != value) { _ScreenSaverActive = value; FirePropertyChanged("ScreenSaverActive"); } }
        }

        public string CurrentScreenSaver
        {
            get { return Kernel.Instance.ScreenSaverUI; }
        }

        public Item CurrentUser { get; set; }

        public List<Item> AvailableUsers { get { return Kernel.AvailableUsers.Select(u =>ItemFactory.Instance.Create(new User {Name=u.Name, Id = new Guid(u.Id ?? ""), Dto = u, ParentalAllowed = !u.HasPassword})).ToList(); } } 

        public List<string> ConfigPanelNames
        {
            get
            {
                return Kernel.Instance.ConfigPanels.Keys.ToList();
            }
        }

        public string ConfigPanel(string name)
        {
            if (Kernel.Instance.ConfigPanels.ContainsKey(name))
            {
                return Kernel.Instance.ConfigPanels[name].Resource;
            }
            else
            {
                return "me:AddinPanel"; //return the embedded empty UI if not found
            }
        }

        public Choice ConfigModel { get; set; }

        public string CurrentConfigPanel
        {
            get
            {
                return Kernel.Instance.ConfigPanels[ConfigModel.Chosen.ToString()].Resource;
            }
        }

        public ModelItem CurrentConfigObject
        {
            get
            {
                if (Kernel.Instance.ConfigPanels[ConfigModel.Chosen.ToString()] != null)
                {
                    return Kernel.Instance.ConfigPanels[ConfigModel.Chosen.ToString()].ConfigObject;
                }
                else return null;
            }
        }

        public Dictionary<string, ViewTheme> AvailableThemes { get { return Kernel.Instance.AvailableThemes; } }

        public List<string> AvailableThemeNames
        {
            get
            {
                return AvailableThemes.Keys.ToList();
            }
        }

        public ViewTheme CurrentTheme
        {
            get
            {
                if (AvailableThemes.ContainsKey(Config.Instance.ViewTheme))
                {
                    return AvailableThemes[Config.Instance.ViewTheme];
                }
                else
                { //old or bogus theme - return default so we don't crash
                    //and set the config so config page doesn't crash
                    Config.Instance.ViewTheme = "Default";
                    return AvailableThemes["Default"];
                }
            }
        }

        public bool SetThemeStatus(string theme, string status)
        {
            if (AvailableThemes.ContainsKey(theme))
            {
                AvailableThemes[theme].Status = status;
                FirePropertyChanged("CurrentThemeStatus");
                return true;
            }
            else
            {
                return false;
            }
        }

        public string CurrentThemeStatus
        {
            get
            {
                return CurrentTheme.Status;
            }
        }

        public FavoritesCollectionFolder FavoritesFolder
        {
            get { return Kernel.Instance.FavoritesFolder; }
        }

        public void ClearFavorites()
        {
            if (FavoritesFolder != null)
            {
                FavoritesFolder.Clear();
            }
            
        }

        private Item currentItem;

        public Item CurrentItem
        {
            get
            {
                if (currentItem != null)
                {
                    return currentItem;
                }
                else
                {
                    if (Application.CurrentInstance.CurrentFolder.SelectedChild != null)
                    {
                        return Application.CurrentInstance.CurrentFolder.SelectedChild;
                    }
                    else return Item.BlankItem;
                }
            }
            set
            {
                if (currentItem != value)
                {
                    currentItem = value;
                    OnCurrentItemChanged();
                }
            }
        }

        private List<MenuItem> currentContextMenu;

        public List<MenuItem> ContextMenu
        {
            get
            {
                if (currentContextMenu == null) currentContextMenu = Kernel.Instance.ContextMenuItems;
                return currentContextMenu;
            }
            set
            {
                currentContextMenu = value;
                //Logger.ReportVerbose("Context Menu Changed.  Items: " + currentContextMenu.Count);
                FirePropertyChanged("ContextMenu");
            }
        }

        public void ResetContextMenu()
        {
            ContextMenu = Kernel.Instance.ContextMenuItems;
        }

        public List<MenuItem> PlayMenu
        {
            get
            {
                return Kernel.Instance.PlayMenuItems;
            }
        }

        public List<MenuItem> DetailMenu
        {
            get
            {
                return Kernel.Instance.DetailMenuItems;
            }
        }

        private MenuManager menuManager;

        public bool NavigatingForward
        {
            get { return navigatingForward; }
            set { navigatingForward = value; }
        }


        private string entryPointPath = string.Empty;

        public string EntryPointPath
        {
            get
            {
                return this.entryPointPath.ToLower();
            }
        }

        public const string CONFIG_ENTRY_POINT = "configmb";

        public string ConfigEntryPointVal
        {
            get
            {
                return CONFIG_ENTRY_POINT.ToLower();
            }
        }

        public Item LastPlayedItem
        {
            get
            {
                return lastPlayed ?? Item.BlankItem;
            }
        }

        static Application()
        {

        }

        public Application()
            : this(null, null)
        {

        }

        public Application(MyHistoryOrientedPageSession session, Microsoft.MediaCenter.Hosting.AddInHost host)
        {

            this.session = session;
            if (session != null)
            {
                this.session.Application = this;
            }
            singleApplicationInstance = this;
            //wire up our mouseActiveHooker if enabled so we can know if the mouse is active over us
            if (Config.Instance.EnableMouseHook)
            {
                Kernel.Instance.MouseActiveHooker.MouseActive += new IsMouseActiveHooker.MouseActiveHandler(mouseActiveHooker_MouseActive);
            }
            //initialize our menu manager
            menuManager = new MenuManager();

            //initialize screen saver
            ScreenSaverTimer = new Timer() { AutoRepeat = true, Enabled = true, Interval = 60000 };
            ScreenSaverTimer.Tick += new EventHandler(ScreenSaverTimer_Tick);
        }

        void ScreenSaverTimer_Tick(object sender, EventArgs e)
        {
            if (LoggedIn && Config.EnableScreenSaver) 
            {
                if (!IsPlayingVideo && !IsExternalWmcApplicationPlaying)
                {
                    if (Helper.SystemIdleTime > Config.ScreenSaverTimeOut * 60000)
                    {
                        this.ScreenSaverActive = true;
                        //increase the frequency of this tick so we will turn off quickly
                        ScreenSaverTimer.Interval = 500;
                    }
                    else
                    {
                        this.ScreenSaverActive = false;
                        ScreenSaverTimer.Interval = 60000; //move back to every minute
                    }
                }
                else
                {
                    if (!this.ScreenSaverActive)
                    {
                        //something playing - be sure we don't kick off right after it ends
                        ScreenSaverTimer.Interval = Config.ScreenSaverTimeOut * 60000;
                    }
                }
            }
        }


        /// <summary>
        /// This is an oddity under TVPack, sometimes the MediaCenterEnvironemt and MediaExperience objects go bad and become
        /// disconnected from their host in the main application. Typically this is after 5 minutes of leaving the application idle (but noot always).
        /// What is odd is that using reflection under these circumstances seems to work - even though it is only doing the same as Reflector shoulds the real 
        /// methods do. As I said it's odd but this at least lets us get a warning on the screen before the application crashes out!
        /// </summary>
        /// <param name="message"></param>
        public static void DialogBoxViaReflection(string message)
        {
            MediaCenterEnvironment ev = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
            FieldInfo fi = ev.GetType().GetField("_legacyAddInHost", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            if (fi != null)
            {
                AddInHost2 ah2 = (AddInHost2)fi.GetValue(ev);
                if (ah2 != null)
                {
                    Type t = ah2.GetType();
                    PropertyInfo pi = t.GetProperty("HostControl", BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Public);
                    if (pi != null)
                    {
                        HostControl hc = (HostControl)pi.GetValue(ah2, null);
                        hc.Dialog(message, "Media Browser", 1, 120, true);
                    }
                }
            }
        }

        private static bool? _RunningOnExtender;
        public static bool RunningOnExtender
        {
            get
            {
                if (!_RunningOnExtender.HasValue)
                {
                    try
                    {
                        Dictionary<string, object> capabilities = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment.Capabilities;

                        bool isLocal = capabilities.ContainsKey("Console") && (bool)capabilities["Console"];

                        _RunningOnExtender = !isLocal;
                    }
                    catch (Exception ex)
                    {
                        Logger.ReportException("Error in RunningOnExtender. If you're on a PC this is not a problem.", ex);
                        //don't crash - just assume we are on a regular install and something went wrong momentarily - it'll have a problem later if it is real
                        return false;
                    }
                }

                return _RunningOnExtender.Value;

            }
        }


        /// <summary>
        /// Unfortunately TVPack has some issues at the moment where the MedaCenterEnvironment stops working, we catch these errors and rport them then close.
        /// In the future this method and all references should be able to be removed, once MS fix the bugs
        /// </summary>
        internal static void ReportBrokenEnvironment()
        {
            Logger.ReportInfo("Application has broken MediaCenterEnvironment, possibly due to 5 minutes of idle while running under system with TVPack installed.\n Application will now close.");
            Logger.ReportInfo("Attempting to use reflection that sometimes works to show a dialog box");
            // for some reason using reflection still works
            Application.DialogBoxViaReflection(CurrentInstance.StringData("BrokenEnvironmentDial"));
            Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.CloseApplication();
        }

        public void FixRepeatRate(object scroller, int val)
        {
            try
            {
                PropertyInfo pi = scroller.GetType().GetProperty("View", BindingFlags.Public | BindingFlags.Instance);
                object view = pi.GetValue(scroller, null);
                pi = view.GetType().GetProperty("Control", BindingFlags.Public | BindingFlags.Instance);
                object control = pi.GetValue(view, null);

                pi = control.GetType().GetProperty("KeyRepeatThreshold", BindingFlags.NonPublic | BindingFlags.Instance);
                pi.SetValue(control, (UInt32)val, null);
            }
            catch
            {
                // thats it, I give up, Microsoft went and changed interfaces internally 
            }

        }

        public static MediaCenterEnvironment MediaCenterEnvironment
        {
            get
            {
                return Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
            }
        }

        public BasePlaybackController PlaybackController
        {
            get
            {
                if (currentPlaybackController != null)
                    return currentPlaybackController;
                return Kernel.Instance.PlaybackControllers[0];
            }
        }

        /// <summary>
        /// Determines whether or not a PlaybackController is currently playing
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                return Kernel.Instance.PlaybackControllers.Any(p => p.IsPlaying);
            }
        }

        /// <summary>
        /// Determines whether or not a PlaybackController is currently playing video
        /// </summary>
        public bool IsPlayingVideo
        {
            get
            {
                return Kernel.Instance.PlaybackControllers.Any(p => p.IsPlayingVideo);
            }
        }

        public bool IsExternalWmcApplicationPlaying
        {
            get
            {
                if (IsPlaying)
                {
                    return false;
                }

                var playstate = PlaybackControllerHelper.GetCurrentPlayState();

                return playstate == Microsoft.MediaCenter.PlayState.Playing || playstate == Microsoft.MediaCenter.PlayState.Paused || playstate == Microsoft.MediaCenter.PlayState.Buffering;
            }
        }

        public AggregateFolder RootFolder
        {
            get
            {
                return Kernel.Instance.RootFolder;
            }
        }

        public void Close()
        {
            Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.CloseApplication();
        }

        public void BackOut()
        {
            //back up and close the app if that fails
            if (!session.BackPage())
                Close();
        }

        public void Back()
        {
            session.BackPage();
        }

        public void FinishInitialConfig()
        {
            MediaCenterEnvironment ev = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
            ev.Dialog(CurrentInstance.StringData("InitialConfigDial"), CurrentInstance.StringData("Restartstr"), DialogButtons.Ok, 60, true);
            Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.CloseApplication();

        }

        public void DeleteMediaItem(Item Item)
        {
            // Need to put delete on a thread because the play process is asynchronous and
            // we don't want to tie up the ui when we call sleep
            Async.Queue("DeleteMediaItem", () =>
            {
                // Setup variables
                MediaCenterEnvironment mce = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
                var msg = CurrentInstance.StringData("DeleteMediaDial");
                var caption = CurrentInstance.StringData("DeleteMediaCapDial");

                // Present dialog
                var dr = mce.Dialog(msg, caption, DialogButtons.No | DialogButtons.Yes, 0, true);

                if (dr == DialogResult.No)
                {
                    mce.Dialog(CurrentInstance.StringData("NotDeletedDial"), CurrentInstance.StringData("NotDeletedCapDial"), DialogButtons.Ok, 0, true);
                    return;
                }

                if (dr == DialogResult.Yes && this.Config.Advanced_EnableDelete && this.Config.EnableAdvancedCmds)
                {
                    Item parent = Item.PhysicalParent;
                    string path = Item.Path;
                    string name = Item.Name;
                    var topParents = Kernel.Instance.FindItems(Item.Id).Select(i => i.TopParent).Distinct(i => i.Id);

                    try
                    {
                        //play something innocuous to be sure the file we are trying to delete is not in the now playing window
                        string DingFile = System.Environment.ExpandEnvironmentVariables("%WinDir%") + "\\Media\\Windows Recycle.wav";

                        // try and run the file regardless whether it exists or not.  Ideally we want it to play but if we can't find it, it will still put MC in a state that allows
                        // us to delete the file we are trying to delete
                        mce.PlayMedia(MediaType.Audio, DingFile, false);

                        // The play method runs asynchronously, so give it a second to ensure it's at least started.
                        System.Threading.Thread.Sleep(1000);

                        if (Directory.Exists(path))
                        {
                            Directory.Delete(path, true);
                        }
                        else if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch (IOException)
                    {
                        mce.Dialog(CurrentInstance.StringData("NotDelInvalidPathDial"), CurrentInstance.StringData("DelFailedDial"), DialogButtons.Ok, 0, true);
                    }
                    catch (Exception)
                    {
                        mce.Dialog(CurrentInstance.StringData("NotDelUnknownDial"), CurrentInstance.StringData("DelFailedDial"), DialogButtons.Ok, 0, true);
                    }
                    DeleteNavigationHelper(Item);
                    this.Information.AddInformation(new InfomationItem("Deleted media item: " + name, 2));
                    // And refresh the RAL of all needed parents
                    foreach (var folder in topParents) folder.OnQuickListChanged(null);
                    
                }
                else
                    mce.Dialog(CurrentInstance.StringData("NotDelTypeDial"), CurrentInstance.StringData("DelFailedDial"), DialogButtons.Ok, 0, true);
            
            });
        }


        private void DeleteNavigationHelper(Item item)
        {
            Back(); // Back to the Parent Item; This parent still contains old data.
            Async.Queue("Post delete validate", () =>
                                                    {
                                                        //update parent info on all occurences in lib
                                                        foreach (var occurence in Kernel.Instance.FindItems(item.Id))
                                                        {
                                                            var parent = occurence.Parent;
                                                            if (parent != null) parent.RefreshMetadata();
                                                        }

                                                        //and also actual parent of this item (in case in recent list)
                                                        item.PhysicalParent.RefreshChildren();
                                                    });
        }

        // Entry point for the app
        public void Init()
        {
            Config.IsFirstRun = false;

            Logger.ReportInfo("Media Browser (version " + AppVersion + ") Starting up.");
            //let's put some useful info in here for diagnostics
            if (!Config.AutoValidate)
                Logger.ReportWarning("*** AutoValidate is OFF.");
            //if (Config.AllowInternetMetadataProviders) Logger.ReportInfo("*** Save Locally is "+(Config.SaveLocalMeta ? "ON." : "OFF."));
            // Now let's put a diagnostic ping in here for the beta cycle so we can see how much testing we're getting
            //string info = "IP=" + Config.AllowInternetMetadataProviders + " EXTP=" + Config.ExternalPlayers.Count + " EXT=" + RunningOnExtender;
            //Helper.Ping("http://www.ebrsoft.com/software/mb/plugins/ping.php?product=MBBeta&ver=" + Kernel.Instance.VersionStr + "&mac=" + Helper.GetMACAddress() + "&key=" + info);
            try
            {
                //Check to see if this is the first time this version is run
                string currentVersion = Kernel.Instance.Version.ToString();
                if (Config.MBVersion != currentVersion)
                {
                    //first time with this version - run routine
                    Logger.ReportInfo("First run for version " + currentVersion);
                    bool okToRun = FirstRunForVersion(currentVersion);
                    //and update
                    Config.MBVersion = currentVersion;
                    if (!okToRun)
                    {
                        Logger.ReportInfo("Closing MB to allow new version migration...");
                        this.Close();
                    }
                }
                ShowNowPlaying = IsPlaying || IsExternalWmcApplicationPlaying;

                // setup image to use in external splash screen
                string splashFilename = Path.Combine(Path.Combine(ApplicationPaths.AppIBNPath, "General"), "splash.png");
                ExtSplashBmp = File.Exists(splashFilename) ? new System.Drawing.Bitmap(splashFilename) : new System.Drawing.Bitmap(Resources.mblogo1000);

                Login();
            }
            catch (Exception e)
            {
                AddInHost.Current.MediaCenterEnvironment.Dialog(CurrentInstance.StringData("CriticalErrorDial") + e + " " + e.StackTrace, CurrentInstance.StringData("CriticalErrorCapDial"), DialogButtons.Ok, 60, true);
                AddInHost.Current.ApplicationContext.CloseApplication();
            }
        }

        
        /// <summary>
        /// Logout current user and re-display login screen
        /// </summary>
        public void Logout()
        {
            // Present dialog - must be asynced to get off the UI thread
            Async.Queue("Logout", () =>
                                      {
                                          if (YesNoBox(string.Format("Logout of user profile {0}?", Kernel.CurrentUser.Name)) == "Y")
                                          {
                                              Close();
                                          }
                                      });
            

        }

        /// <summary>
        /// Log in to default or show a login screen with choices
        /// </summary>
        public void Login()
        {
            var user = Kernel.Instance.CommonConfigData.LogonAutomatically ? AvailableUsers.FirstOrDefault(u => u.Name.Equals(Kernel.Instance.CommonConfigData.AutoLogonUserName, StringComparison.OrdinalIgnoreCase)) :
                           Kernel.AvailableUsers.Count == 1 ? AvailableUsers.FirstOrDefault() : null;
            if (user != null)
            {
                // only one user or specified - log in automatically
                LoginUser(user);
            }
            else
            {
                // show login screen
                session.GoToPage("resx://MediaBrowser/MediaBrowser.Resources/LoginPage", new Dictionary<string, object> {{"Application",this}});
            }
        }

        public void LoginUser(Item user)
        {
            Kernel.CurrentUser = user.BaseItem as User;
            CurrentUser = user;
            var ignore = CurrentUser.PrimaryImage; // force this to load
            FirePropertyChanged("CurrentUser");
            if (Kernel.CurrentUser != null && Kernel.CurrentUser.HasPassword)
            {
                // Try with saved pw
                if (!Kernel.Instance.CommonConfigData.LogonAutomatically || !LoadUser(Kernel.CurrentUser, Kernel.Instance.CommonConfigData.AutoLogonPw))
                {
                    // show pw screen
                    OpenSecurityPage("Please Enter Password for " + CurrentUser.Name + " (select or enter when done)");
                }
            }
            else
            {
                // just log in as we don't have a pw
                LoadUser(user.BaseItem as User, "");
            }
        }

        protected bool LoadUser(User user, string pw)
        {
            try
            {
                if (!string.IsNullOrEmpty(pw))
                {
                    Kernel.ApiClient.AuthenticateUserWithHash(user.Id, pw);
                }
                else
                {
                    Kernel.ApiClient.AuthenticateUser(user.ApiId, pw);
                }
            }
            catch (Model.Net.HttpException e)
            {
                if (((System.Net.WebException)e.InnerException).Status == System.Net.WebExceptionStatus.ProtocolError)
                {
                    AddInHost.Current.MediaCenterEnvironment.Dialog("Incorrect Password.", "Access Denied", DialogButtons.Ok, 100, true);
                    return false;
                }
                throw;
            }

            LoggedIn = true;

            Kernel.ApiClient.CurrentUserId = user.Id;

            // load user config
            Kernel.Instance.LoadUserConfig();

            // setup styles and fonts with user options
            try
            {
                CustomResourceManager.SetupStylesMcml(AddInHost.Current, Config.Instance);
                CustomResourceManager.SetupFontsMcml(AddInHost.Current, Config.Instance);
            }
            catch (Exception ex)
            {
                AddInHost.Current.MediaCenterEnvironment.Dialog(ex.Message, "Error", DialogButtons.Ok, 100, true);
                AddInHost.Current.ApplicationContext.CloseApplication();
                return false;
            }

            // load plugins
            Kernel.Instance.LoadPlugins();

            //populate the config model choice
            ConfigModel = new Choice { Options = ConfigPanelNames };

            // load root
            Kernel.Instance.ReLoadRoot();

            if (Kernel.Instance.RootFolder == null)
            {
                Async.Queue("Launch Error", () =>
                                                {
                                                    MessageBox("Unable to retrieve root folder.  Application will exit.");
                                                    Close();
                                                });
            }
            else
            {
                Logger.ReportInfo("*** Theme in use is: " + Config.ViewTheme);
                //Launch into our entrypoint
                LaunchEntryPoint(EntryPointResolver.EntryPointPath);
            }

            return true;
        }

        protected void ValidateRoot()
        {
            //validate that everything at the root level is actually a folder - the UI will blow chow with items
            foreach (var item in RootFolder.Children)
            {
                if (!(item is Folder))
                {
                    string msg = "W A R N I N G: Item " + item.Name + " is resolving to a " + item.GetType().Name + ". All root level items must be folders.\n  Check that this item isn't being mistaken because it is a folder with only a couple items and you have the playlist functionality enabled.";
                    Logger.ReportError(msg);
                    DisplayDialog(msg, "Invalid Root Item");
                }
            }
            
        }

        public void LaunchEntryPoint(string entryPointPath)
        {
            this.entryPointPath = entryPointPath;

            if (IsInEntryPoint)
            {
                //add in a fake breadcrumb so they will show properly
                session.AddBreadcrumb("DIRECTENTRY");
            }

            if (this.EntryPointPath.ToLower() == ConfigEntryPointVal) //specialized case for config page
            {
                OpenConfiguration(true);
            }
            else
            {
                try
                {
                    this.RootFolderModel = (FolderModel)ItemFactory.Instance.Create(EntryPointResolver.EntryPoint(this.EntryPointPath));

                    WebSocket = new ApiWebSocket(new WebSocket4NetClientWebSocket());
                    WebSocket.Connect(Kernel.ApiClient.ServerHostName, Kernel.ServerInfo.WebSocketPortNumber, Kernel.ApiClient.ClientType, Kernel.ApiClient.DeviceId);
                    WebSocket.LibraryChanged += LibraryChanged;
                    WebSocket.BrowseCommand += BrowseRequest;
                    WebSocket.PlayCommand += PlayRequest;
                    WebSocket.PlaystateCommand += PlayStateRequest;
                  

                    // We check config here instead of in the Updater class because the Config class 
                    // CANNOT be instantiated outside of the application thread.
                    Updater = new Updater(this);
                    if (Config.EnableUpdates && !RunningOnExtender)
                    {
                        Async.Queue(Async.STARTUP_QUEUE, CheckForSystemUpdate, 10000);
                        Async.Queue(Async.STARTUP_QUEUE, () =>
                        {
                            PluginUpdatesAvailable = Updater.PluginUpdatesAvailable();
                        }, 30000);
                    }

                    // Let the user know if the server needs to be restarted
                    // Put it on the same thread as the update checks so it will be behind them
                    Async.Queue(Async.STARTUP_QUEUE, () =>
                                                         {
                                                             if (Kernel.ServerInfo.HasPendingRestart)
                                                             {
                                                                 if (YesNoBox("The MB Server needs to re-start to apply an update.  Restart now?") == "Y")
                                                                 {
                                                                     Kernel.ApiClient.PerformPendingRestart();
                                                                     MessageBox("Your server is being re-started.  MB Classic will now exit so you can re load it.");
                                                                     Close();
                                                                 }
                                                             }
                                                         },35000);

                    Navigate(this.RootFolderModel);
                }
                catch (Exception ex)
                {
                    Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment.Dialog(CurrentInstance.StringData("EntryPointErrorDial") + this.EntryPointPath + ". " + ex.ToString() + " " + ex.StackTrace.ToString(), CurrentInstance.StringData("EntryPointErrorCapDial"), DialogButtons.Ok, 30, true);
                    Close();
                }
            }
        }

        public void CheckForSystemUpdate()
        {
            if (!systemUpdateCheckInProgress)
            {
                Async.Queue("UPDATE CHECK", () =>
                                                {
                                                    if (RunningOnExtender)
                                                    {
                                                        Information.AddInformationString("Cannot update from an extender.");
                                                        return;
                                                    }
                                                    SystemUpdateCheckInProgress = true;
                                                    Updater.CheckForUpdate();
                                                    SystemUpdateCheckInProgress = false;
                                                });
            }
        }

        bool FirstRunForVersion(string thisVersion)
        {
            var oldVersion = new System.Version(Config.MBVersion);
            if (oldVersion < new System.Version(2, 0, 0, 0))
            {
                //FullRefresh();
                return true;  //new install, don't need to migrate
            }
            switch (thisVersion)
            {
                case "2.2.4.0":
                    //set cacheAllImages to "false" - user can change it back if they wish or are directed to
                    Config.CacheAllImagesInMemory = false;
                    //anything else...?
                    break;
                case "2.2.6.0":
                case "2.2.7.0":
                case "2.2.8.0":
                case "2.2.9.0":
                    //set validationDelay to "0" - user can change it back if they wish or are directed to
                    //Config.ValidationDelay = 0; removed in future version
                    break;
                case "2.3.0.0":
                    //re-set plugin source if not already done by configurator...
                    MigratePluginSource();
                    break;
                case "2.3.1.0":
                case "2.3.2.0":
                case "2.5.0.0":
                case "2.5.1.0":
                case "2.5.2.0":
                case "2.5.3.0":
                case "2.6.0.0":
                case "2.6.1.0":
                case "2.6.2.0":
                    Config.EnableNestedMovieFolders = false;  //turn this off - it is what causes all the "small library" issues
                    Config.EnableTranscode360 = false; //no longer need transcoding and it just causes problems
                    Kernel.Instance.ConfigData.Save();
                    if (oldVersion <= new System.Version(2, 3, 0, 0))
                    {
                        MigratePluginSource(); //still may need to do this (if we came from earlier version than 2.3
                    }
                    if (oldVersion <= new System.Version(2, 3, 1, 0))
                    {
                        Config.EnableTraceLogging = true; //turn this on by default since we now have levels and retention/clearing
                    }
                    else
                    if (oldVersion < new System.Version(2,5,0,0))
                    {
                        //upgrading from 2.3.2 - item migration should have already occurred...
                        Config.EnableTraceLogging = true; //turn this on by default since we now have levels and retention/clearing
                        var oldRepo = new ItemRepository();
                        Kernel.Instance.MB3ApiRepository.MigrateDisplayPrefs(oldRepo);
                        //Async.Queue("Playstate Migration",() => Kernel.Instance.ItemRepository.MigratePlayState(oldRepo),15000); //delay to allow repo to load
                    }
                    break;
            }
            return true;
        }

        private void MigratePluginSource()
        {
                    try
                    {
                        Config.PluginSources.RemoveAt(Config.PluginSources.FindIndex(s => s.ToLower() == "http://www.mediabrowser.tv/plugins/plugin_info.xml"));
                    }
                    catch
                    {
                        //wasn't there - no biggie
                    }
                    if (Config.PluginSources.Find(s => s == "http://www.mediabrowser.tv/plugins/multi/plugin_info.xml") == null)
                    {
                        Config.PluginSources.Add("http://www.mediabrowser.tv/plugins/multi/plugin_info.xml");
                        Logger.ReportInfo("Plug-in Source migrated to multi-version source");
                    }
        }

        public bool IsInEntryPoint
        {
            get
            {
                return !String.IsNullOrEmpty(this.EntryPointPath);
            }
        }

        public void ReLoad()
        {
            //force a re-load of all our data
            this.RootFolder.RefreshMetadata();
        }
           

        public void FullRefresh()
        {
            Async.Queue(CurrentInstance.StringData("Manual Full Refresh"), () =>
                                                                               {
                                                                                   Kernel.ApiClient.StartLibraryScan();
                                                                                   MessageBox(CurrentInstance.StringData("ManualRefreshDial"));
                                                                               });
        }

        void RunActionRecursively(Folder folder, Action<BaseItem> action)
        {
            action(folder);
            foreach (var item in folder.RecursiveChildren.OrderByDescending(i => i.DateModified))
            {
                action(item);
            }
        }

        Boolean PlayStartupAnimation = true;

        public Boolean CanPlayStartup()
        {
            if (PlayStartupAnimation)
            {
                PlayStartupAnimation = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool displayPopupPlay = false;
        public bool DisplayPopupPlay
        {
            get
            {
                return this.displayPopupPlay;
            }
            set
            {
                this.displayPopupPlay = value;
                FirePropertyChanged("DisplayPopupPlay");
            }
        }

        private bool showSearchPanel = false;
        public bool ShowSearchPanel
        {
            get { return this.showSearchPanel; }
            set
            {
                if (showSearchPanel != value)
                {
                    showSearchPanel = value;
                    FirePropertyChanged("ShowSearchPanel");
                }
            }
        }

        private bool showNowPlaying = false;
        public bool ShowNowPlaying
        {
            get { return this.showNowPlaying; }
            set
            {
                if (showNowPlaying != value)
                {
                    Logger.ReportVerbose("Setting now playing status to " + value.ToString());
                    
                    showNowPlaying = value; 
                    
                    FirePropertyChanged("ShowNowPlaying");
                }
            }
        }

        public string NowPlayingText
        {
            get
            {
                try
                {
                    foreach (var controller in Kernel.Instance.PlaybackControllers)
                    {
                        if (controller.IsPlaying)
                        {
                            return controller.NowPlayingTitle;
                        }
                    }

                    if (IsExternalWmcApplicationPlaying)
                    {
                        return PlaybackControllerHelper.GetNowPlayingTextForExternalWmcApplication();
                    }

                }
                catch (Exception e)
                {
                    // never crash here
                    Logger.ReportException("Something strange happend while getting media name, please report to community.mediabrowser.tv", e);                    

                }
                return "Unknown";
            }
        }

        private Boolean isMouseActive = false;
        public Boolean IsMouseActive
        {
            get { return isMouseActive; }
            set
            {
                if (isMouseActive != value)
                {
                    isMouseActive = value;
                    FirePropertyChanged("IsMouseActive");
                }
            }
        }

        void mouseActiveHooker_MouseActive(IsMouseActiveHooker m, MouseActiveEventArgs e)
        {
            this.IsMouseActive = e.MouseActive;
        }

        public string BreadCrumbs
        {
            get
            {
                return session.Breadcrumbs;
            }
        }

        public void ClearCache()
        {
            MediaCenterEnvironment ev = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
            DialogResult r = ev.Dialog(CurrentInstance.StringData("ClearCacheDial"), CurrentInstance.StringData("ClearCacheCapDial"), DialogButtons.Yes | DialogButtons.No, 60, true);
            if (r == DialogResult.Yes)
            {
                bool ok = Kernel.Instance.MB3ApiRepository.ClearEntireCache();
                if (!ok)
                {
                    ev.Dialog(string.Format(CurrentInstance.StringData("ClearCacheErrorDial"), ApplicationPaths.AppCachePath), CurrentInstance.StringData("Errorstr"), DialogButtons.Ok, 60, true);
                }
                else
                {
                    ev.Dialog(CurrentInstance.StringData("RestartMBDial"), CurrentInstance.StringData("CacheClearedDial"), DialogButtons.Ok, 60, true);
                }
                Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.CloseApplication();
            }
        }


        public void OpenConfiguration(bool showFullOptions)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties["Application"] = this;
            properties["ShowFull"] = showFullOptions;

            if (session != null)
            {
                session.GoToPage("resx://MediaBrowser/MediaBrowser.Resources/ConfigPage", properties);
            }
            else
            {
                Logger.ReportError("Session is null in OpenPage");
            }
        }


        // accessed from Item
        internal void OpenExternalPlaybackPage(Item item)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties["Application"] = this;
            properties["Item"] = item;

            if (session != null)
            {
                session.GoToPage("resx://MediaBrowser/MediaBrowser.Resources/ExternalPlayback", properties);
            }
            else
            {
                Logger.ReportError("Session is null in OpenExternalPlaybackPage");
            }
        }

        public FolderModel CurrentFolder; //used to keep track of the current folder so we can update the UI if needed
        public FolderModel RootFolderModel; //used to keep track of root folder as foldermodel for same reason

        public FolderModel CurrentFolderModel
        {
            get { return CurrentFolder; }
            set
            {
                if (CurrentFolder != value)
                {
                    CurrentFolder = value;
                    FirePropertyChanged("CurrentFolderModel");
                }
            }
        }

        private void OpenFolderPage(FolderModel folder)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties["Application"] = this;
            properties["Folder"] = folder;
            properties["ThemeConfig"] = CurrentTheme.Config;
            CurrentFolder = folder; //store our current folder
            CurrentItem = null; //blank this out in case it was messed with in the last screen

            if (folder.IsRoot)
                RootFolderModel = folder; //store the root as well

            if (session != null)
            {
                folder.NavigatingInto();

                session.GoToPage(folder.Folder.CustomUI ?? CurrentTheme.FolderPage, properties);
            }
            else
            {
                Logger.ReportError("Session is null in OpenPage");
            }
        }

        private Folder GetStartingFolder(BaseItem item)
        {
            Index currentIndex = item as Index;
            return currentIndex ?? (Folder)RootFolder;
        }

        void NavigateToActor(Item item)
        {
            Async.Queue("Person navigation", () =>
                                                 {
                                                    ProgressBox(string.Format("Finding items with {0} in them...", item.Name));
                                                    var person = (Person)item.BaseItem;
                                                    Folder searchStart = GetStartingFolder(item.BaseItem.Parent);

                                                    var query = new ItemQuery
                                                                    {
                                                                        UserId = Kernel.CurrentUser.Id.ToString(),
                                                                        Fields = MB3ApiRepository.StandardFields,
                                                                        ParentId = searchStart.ApiId,
                                                                        Person = person.Name,
                                                                        PersonTypes = new[] {"Actor"},
                                                                        Recursive = true
                                                                    };
                                                    var index = new SearchResultFolder(Kernel.Instance.MB3ApiRepository.RetrieveItems(query).ToList()) {Name = item.Name};
                                                    ShowMessage = false;

                                                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>Navigate(ItemFactory.Instance.Create(index)));
                                                     
                                                 });
        }


        public void NavigateToGenre(string genre, Item currentMovie)
        {
            Async.Queue("Genre navigation", () =>
                                                {
                                                    ProgressBox(string.Format("Finding items in the {0} genre...", genre));
                                                    var searchStart = GetStartingFolder(currentMovie.BaseItem.Parent);

                                                    var query = new ItemQuery
                                                                    {
                                                                        UserId = Kernel.CurrentUser.Id.ToString(),
                                                                        Fields = MB3ApiRepository.StandardFields,
                                                                        ParentId = searchStart.ApiId,
                                                                        Genres = new[] {genre},
                                                                        ExcludeItemTypes = Config.ExcludeRemoteContentInSearch ? new[] {"Episode", "Season", "Trailer"} : new[] {"Episode", "Season"},
                                                                        Recursive = true
                                                                    };
                                                    var index = new SearchResultFolder(Kernel.Instance.MB3ApiRepository.RetrieveItems(query).ToList()) {Name = genre};
                                                    ShowMessage = false;

                                                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => Navigate(ItemFactory.Instance.Create(index)));
                                                });
        }


        public void NavigateToDirector(string director, Item currentMovie)
        {

            var searchStart = GetStartingFolder(currentMovie.BaseItem.Parent);

            var index = searchStart.Search(
                ShowFinder(show => show.Directors == null ? false : show.Directors.Contains(director)),
                director);

            index.Name = director;

            Navigate(ItemFactory.Instance.Create(index));
        }



        Func<BaseItem, bool> ShowFinder(Func<IShow, bool> func)
        {
            return i => i is IShow ? func((i as IShow)) : false;
        }

        public void Navigate(Item item)
        {
            currentContextMenu = null; //any sort of navigation should reset our context menu so it will properly re-evaluate on next ref
            
            if (item.BaseItem is Person)
            {
                NavigateToActor(item);
                return;
            }

            if (item.BaseItem is Show)
            {
                if ((item.HasDataForDetailPage && item.BaseItem is Movie) ||
                    this.Config.AlwaysShowDetailsPage)
                {
                    // go to details screen 
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties["Application"] = this;
                    properties["Item"] = item;
                    properties["ThemeConfig"] = CurrentTheme.Config;
                    
                    session.GoToPage(item.BaseItem.CustomUI ?? CurrentTheme.DetailPage, properties);

                    return;
                }
            }


            var folder = item as MediaBrowser.Library.FolderModel;
            if (folder != null)
            {
                if (!Config.Instance.RememberIndexing)
                {
                    folder.DisplayPrefs.IndexBy = MediaBrowser.Library.Localization.LocalizedStrings.Instance.GetString("NoneDispPref");
                }
                if (Config.Instance.AutoEnterSingleDirs && (folder.Folder.Children.Count == 1))
                {
                    if (folder.IsRoot) //special breadcrumb if we are going from a single item root
                        session.AddBreadcrumb("DIRECTENTRY");
                    else
                        session.AddBreadcrumb(folder.Name);
                    folder.NavigatingInto(); //make sure we validate
                    Navigate(folder.Children[0]);
                }
                else
                {
                    OpenFolderPage(folder);
                }
            }
            else
            {
                Resume(item);
            }
        }

        public void OpenSecurityPage(object prompt)
        {
            var properties = new Dictionary<string, object>();
            properties["Application"] = this;
            properties["PromptString"] = prompt;
            this.RequestingPIN = true; //tell page we are calling it (not a back action)
            session.GoToPage("resx://MediaBrowser/MediaBrowser.Resources/ParentalPINEntry", properties);
        }

        public void OpenMCMLPage(string page, Dictionary<string, object> properties)
        {
            currentContextMenu = null; //good chance this has happened as a result of a menu item selection so be sure this is reset
            Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => session.GoToPage(page, properties));
        }

        /// <summary>
        /// Backward compatiblity only
        /// </summary>
        public void UnlockPC()
        {}

        /// <summary>
        /// Backward compatiblity only
        /// </summary>
        public void RelockPC()
        {}

        /// <summary>
        /// Themes can use this to disable playback in expired mode.
        /// </summary>
        /// <param name="themeName"></param>
        /// <param name="value"></param>
        /// <returns>True if the theme was found false if not</returns>
        public bool SetPlaybackEnabled(string themeName, bool value)
        {
            if (AvailableThemes.ContainsKey(themeName))
            {
                AvailableThemes[themeName].PlaybackEnabled = value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// This will return the playback capability of the current theme.  Themes should use SetPlaybackEnabled(themename) to set.
        /// </summary>
        public bool PlaybackEnabled
        {
            get
            {
                return CurrentTheme.PlaybackEnabled;
            }
        }

        /// <summary>
        /// Takes an item and plays all items within the same folder, starting with the supplied Item
        /// </summary>
        public void PlayFolderBeginningWithItem(Item item)
        {
            Folder folder = item.PhysicalParent.BaseItem as Folder;

            IEnumerable<Media> items = folder.RecursiveMedia.SkipWhile(v => v.Id != item.Id);

            PlayableItem playable = PlayableItemFactory.Instance.Create(items);
            Play(playable);

        }

        /// <summary>
        /// Plays an item while shuffling it's contents
        /// </summary>
        public void Shuffle(Item item)
        {
            Play(item, false, false, null, true);
        }

        /// <summary>
        /// Plays the first unwatched item within a Folder
        /// </summary>
        public void Unwatched(Item item)
        {
            Folder folder = item.BaseItem as Folder;

            Media firstUnwatched = folder.RecursiveMedia.Where(v => v != null && !v.PlaybackStatus.WasPlayed).OrderBy(v => v.Path).FirstOrDefault();

            if (firstUnwatched != null)
            {
                PlayableItem playable = PlayableItemFactory.Instance.Create(firstUnwatched);
                Play(playable);
            }
        }

        /// <summary>
        /// Queues an item for playback
        /// </summary>
        public void AddToQueue(Item item)
        {
            Play(item, false, true, null, false);
        }

        /// <summary>
        /// Plays an Item
        /// </summary>
        public void Play(Item item)
        {
            Play(item, false, false, null, false);
        }

        /// <summary>
        /// Plays all trailers for an Item
        /// </summary>
        public void PlayLocalTrailer(Item item)
        {
            var movie = item.BaseItem as ISupportsTrailers;
            if (movie.ContainsTrailers)
            {
                PlayableItem playable = PlayableItemFactory.Instance.Create(movie.TrailerFiles);
                Play(playable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="playIntros">Whether not not intros should be played. Unless you have a specific reason to set this, leave it null so the core can decide.</param>
        public void Play(Item item, bool resume, bool queue, bool? playIntros, bool shuffle, long startPos = 0)
        {
            //if playback is disabled display a message
            if (!PlaybackEnabled)
            {
                DisplayDialog("Playback is disabled.  You may need to register your current theme.", "Cannot Play");
                return;
            }

            PlayableItem playable = PlayableItemFactory.Instance.Create(item);

            // This could happen if both item.IsFolder and item.IsPlayable are false
            if (playable == null)
            {
                return;
            }

            if (playIntros.HasValue)
            {
                playable.PlayIntros = playIntros.Value;
            }

            playable.Resume = resume;
            if (startPos > 0) playable.StartPositionTicks = startPos;
            playable.QueueItem = queue;
            playable.Shuffle = shuffle;

            Play(playable);
        }

        /// <summary>
        /// Resumes an Item
        /// </summary>
        public void Resume(Item item)
        {
            Play(item, true, false, null, false);
        }

        public void Play(PlayableItem playable)
        {
            //if (Config.Instance.ParentalControlEnabled && !playable.ParentalAllowed)
            //{
            //    //PIN screen mucks with turning this off
            //    Application.CurrentInstance.DisplayPopupPlay = false; 
                
            //    Kernel.Instance.ParentalControls.PlayProtected(playable);
            //}
            //else
            {
                PlaySecure(playable);
            }
        }

        internal void PlaySecure(PlayableItem playable)
        {
            Async.Queue("Play Action", () =>
            {
                currentPlaybackController = playable.PlaybackController;

                playable.Play();

            });
        }

        /// <summary>
        /// Runs all preplay processes
        /// </summary>
        internal void RunPrePlayProcesses(PlayableItem playableItem)
        {
            OnPrePlayback(playableItem);
        }

        /// <summary>
        /// Used this to notify the core that playback has ceased.
        /// Ideally, only PlayableItem should need to call this.
        /// </summary>
        public void RunPostPlayProcesses(PlayableItem playableItem)
        {
            if (playableItem.EnablePlayStateSaving)
            {
                Async.Queue("AddNewlyWatched", () => AddNewlyWatched(playableItem));
                Async.Queue("Playbackstopped", () => Kernel.ApiClient.ReportPlaybackStopped(playableItem.CurrentMedia.Id.ToString(), Kernel.CurrentUser.Id, playableItem.CurrentMedia.PlaybackStatus.PositionTicks));
            }

            Logger.ReportVerbose("Firing Application.PlaybackFinished for: " + playableItem.DisplayName);

            OnPlaybackFinished(playableItem);
        }

        /// <summary>
        /// Resets last played item for the top level parents of the played media 
        /// </summary>
        private void AddNewlyWatched(PlayableItem playableItem)
        {
            var playedMediaItems = playableItem.PlayedMediaItems;

            if (playedMediaItems.Any())
            {
                // get the top parents of all items that were played
                var topParents = playedMediaItems.Select(i => i.TopParentID).Distinct();
                // and reset the watched list for each of them
                foreach (FolderModel folderModel in RootFolderModel.Children.Where(f => topParents.Contains(f.Id)))
                {
                    folderModel.AddNewlyWatched(ItemFactory.Instance.Create(playedMediaItems.Where(i => i.TopParentID == folderModel.Id).LastOrDefault()));
                }
                //I don't think anyone actually uses this but just in case...
                this.lastPlayed = ItemFactory.Instance.Create(playedMediaItems.LastOrDefault());
            }
        }

        public bool LoggedIn { get; set; } //used to tell if we have logged in successfully

        public bool RequestingPIN { get; set; } //used to signal the app that we are asking for PIN entry

        public string CustomPINEntry { get; set; } //holds the entry for a custom pin (entered by user to compare to pin)

        public void ParentalPINEntered()
        {
            RequestingPIN = false;
            LoadUser(CurrentUser.BaseItem as User, BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(CustomPINEntry))));
        }
        public void BackToRoot()
        {
            //be sure we are on the app thread for session access
            if (Microsoft.MediaCenter.UI.Application.ApplicationThread != System.Threading.Thread.CurrentThread)
            {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => BackToRoot());
                return;
            }
            //back up the app to the root page - used when library re-locks itself
            while (session.BackPage()) { };
        }

        public string DescString(string name)
        {
            //get the description string for "name" out of our string data object
            //we need to translate the content of our item to the field name so that the
            //description field name can be the same across languages
            string key = Kernel.Instance.StringData.GetKey(name.Trim());
            if (string.IsNullOrEmpty(key))
            {
                //probably a string file that has not been updated
                key = name;
                key = key.Replace(" ", "");
                key = key.Replace("*", "");
                key = key.Replace(")", "");
                key = key.Replace(")", "");
                key = key.Replace("-", "");
            }
            return Kernel.Instance.StringData.GetString(key.Trim() + "Desc");
        }

        public static void DisplayDialog(string message, string caption)
        {
            DisplayDialog(message, caption, DialogButtons.Ok, 10);
        }


        public static DialogResult DisplayDialog(string message, string caption, DialogButtons buttons, int timeout)
        {
            // We won't be able to take this during a page transition.  This is good!
            // Conversly, no new pages can be navigated while this is present.
            lock (syncObj)
            {
                DialogResult result = MediaCenterEnvironment.Dialog(message, caption, buttons, timeout, true);
                return result;
            }
        }

        public string AppVersion
        {
            get { return Kernel.Instance.VersionStr; }
        }

        private Information _information = new Information();

        public Information Information
        {
            get
            {
                return _information;
            }
            set
            {
                _information = value;
            }
        }


        private static string _background = null;

        public string MainBackdrop
        {
            get
            {
                if (_background == null)
                {
                    string pngImage = this.Config.InitialFolder + "\\backdrop.png";
                    string jpgImage = this.Config.InitialFolder + "\\backdrop.jpg";

                    if (File.Exists(pngImage))
                    {
                        _background = "file://" + pngImage;
                    }
                    else if (File.Exists(jpgImage))
                    {
                        _background = "file://" + jpgImage;
                    }
                    else
                    {
                        _background = string.Empty;
                    }
                }

                if (string.IsNullOrEmpty(_background))
                {
                    return null;
                }

                return _background;
            }
        }

        /// <summary>
        /// Mounts an iso and returns a string containing the mounted path
        /// </summary>
        public string MountISO(string path)
        {
            try
            {
                Logger.ReportVerbose("Mounting ISO: " + path);
                string command = Config.Instance.DaemonToolsLocation;

                // Create the process start information.
                using (Process process = new Process())
                {
                    //virtualclonedrive
                    if (command.ToLower().EndsWith("vcdmount.exe"))
                        process.StartInfo.Arguments = "-mount \"" + path + "\"";
                    //alcohol120 or alcohol52
                    else if (command.ToLower().EndsWith("axcmd.exe"))
                        process.StartInfo.Arguments = Config.Instance.DaemonToolsDrive + ":\\ /M:\"" + path + "\"";
                    //deamontools
                    else
                        process.StartInfo.Arguments = "-mount 0,\"" + path + "\"";
                    process.StartInfo.FileName = command;
                    process.StartInfo.ErrorDialog = false;
                    process.StartInfo.CreateNoWindow = true;

                    // We wait for exit to ensure the iso is completely loaded.
                    process.Start();
                    process.WaitForExit();
                }

                // Play the DVD video that was mounted.
                string mountedPath = Config.Instance.DaemonToolsDrive + ":\\";

                while (!Directory.Exists(mountedPath))
                {
                    System.Threading.Thread.Sleep(1000);
                }

                return mountedPath;
            }
            catch (Exception)
            {
                // Display the error in this case, they might wonder why it didn't work.
                Application.DisplayDialog("ISO Mounter is not correctly configured.", "Could not mount ISO");
                throw (new Exception("ISO Mounter is not configured correctly"));
            }
        }

        public void UnmountIso()
        {
            try
            {
                Logger.ReportVerbose("Unmounting ISO");
                string command = Config.Instance.DaemonToolsLocation;

                // Create the process start information.
                using (Process process = new Process())
                {
                    //virtualclonedrive
                    if (command.ToLower().EndsWith("vcdmount.exe"))
                        process.StartInfo.Arguments = "/u";
                    //alcohol120 or alcohol52
                    else if (command.ToLower().EndsWith("axcmd.exe"))
                        process.StartInfo.Arguments = Config.Instance.DaemonToolsDrive + ":\\ /U";
                    //deamontools
                    else
                        process.StartInfo.Arguments = "-unmount 0";
                    process.StartInfo.FileName = command;
                    process.StartInfo.ErrorDialog = false;
                    process.StartInfo.CreateNoWindow = true;

                    // We wait for exit to ensure the iso is completely loaded.
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch (Exception)
            {
                // Display the error in this case, they might wonder why it didn't work.
                Application.DisplayDialog("ISO Mounter is not correctly configured.", "Could not unmount ISO");
                throw (new Exception("ISO Mounter is not configured correctly"));
            }
        }

        /// <summary>
        /// Tells all registered PlayBackControllers to stop playback
        /// </summary>
        public void StopAllPlayback(bool waitForStop)
        {
            foreach (var controller in Kernel.Instance.PlaybackControllers)
            {
                if (controller.IsPlaying)
                {
                    controller.StopAndWait();
                }
            }

            if (IsExternalWmcApplicationPlaying)
            {
                Logger.ReportVerbose("Stopping playback from another wmc application, such as live tv");
                StopExternalWmcApplication(true);
            }
        }

        /// <summary>
        /// Stops video playing from other applications, such as live tv
        /// </summary>
        public void StopExternalWmcApplication(bool waitForStop)
        {
            if (IsExternalWmcApplicationPlaying)
            {
                PlaybackControllerHelper.Stop();
            }

            if (waitForStop)
            {
                int i = 0;

                // Try to wait for playback to completely stop, but don't get hung up too long
                while (IsExternalWmcApplicationPlaying && i < 10)
                {
                    System.Threading.Thread.Sleep(250);
                    i++;
                }
            }
        }

        /// <summary>
        /// All we do now is check in with the server and let it figure everything else out
        /// </summary>
        public void UpdatePlayState(Media media, PlaybackStatus playstate, int playlistPosition, long positionTicks, long? duration, DateTime datePlayed, bool saveToDataStore)
        {
            if (saveToDataStore)
            {
                Kernel.Instance.SavePlayState(media, playstate);
            }
        }
    }
}
