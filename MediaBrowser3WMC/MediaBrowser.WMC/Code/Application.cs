using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
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
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Playables;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.UI;
using MediaBrowser.Library.Util;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Updates;
using MediaBrowser.Util;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.AddIn;
using Microsoft.MediaCenter.UI;
using AddInHost = Microsoft.MediaCenter.Hosting.AddInHost;
using BrowseRequestEventArgs = MediaBrowser.ApiInteraction.WebSocket.BrowseRequestEventArgs;
using DialogResult = Microsoft.MediaCenter.DialogResult;
using LibraryChangedEventArgs = MediaBrowser.ApiInteraction.WebSocket.LibraryChangedEventArgs;
using MediaType = Microsoft.MediaCenter.MediaType;
using MenuItem = MediaBrowser.Library.MenuItem;
using PlayRequestEventArgs = MediaBrowser.ApiInteraction.WebSocket.PlayRequestEventArgs;
using PlaystateRequestEventArgs = MediaBrowser.ApiInteraction.WebSocket.PlaystateRequestEventArgs;
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
        private static Timer IdleTimer;
        //tracks whether to show recently added or watched items
        public string RecentItemOption { get { return Config.Instance.RecentItemOption; } set { Config.Instance.RecentItemOption = value; Kernel.Instance.ConfigData.RecentItemOption = value; } }
        public string WatchedOptionString { get { return Config.Instance.TreatWatchedAsInProgress ? LocalizedStrings.Instance.GetString("InProgressEHS") : LocalizedStrings.Instance.GetString("WatchedEHS"); }}
        private bool pluginUpdatesAvailable = false;
        private bool systemUpdateCheckInProgress = false;
        public System.Drawing.Bitmap ExtSplashBmp;
        public Image LogonSplashImage { get; set; }
        private Item lastPlayed;
        private Updater Updater;
        private PowerSettings _powerSetings;

        public PowerSettings PowerSettings { get { return _powerSetings ?? (_powerSetings = new PowerSettings()); } }

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

        private bool _isMuted;
        public bool WMCMute
        {
            get
            {
                return _isMuted; // MediaCenterEnvironment.AudioMixer.Mute; 
            }
            set
            {
                MediaCenterEnvironment.AudioMixer.Mute = _isMuted = value;
                if (currentPlaybackController != null)
                {
                    ReportPlaybackProgress(currentPlaybackController.CurrentPlayableItemId.ToString(), currentPlaybackController.CurrentFilePositionTicks, currentPlaybackController.IsPaused, currentPlaybackController.IsStreaming);
                }
            }
        }

        public void ReportPlaybackProgress(string id, long positionTicks, bool isPaused = false, bool isStreaming = false)
        {
            Kernel.ApiClient.ReportPlaybackProgress(id, Kernel.CurrentUser.Id, positionTicks, isPaused, WMCMute, isStreaming ? "Transcode" : "DirectPlay");
            //WebSocket.SendPlaystateMessage(id, positionTicks, isPaused);
        }

        public void ReportPlaybackStart(string id, bool isStreaming = false)
        {
            Kernel.ApiClient.ReportPlaybackStart(id, Kernel.CurrentUser.Id, isStreaming ? "Transcode" : "DirectPlay");
            //WebSocket.SendPlaybackStarted(id);
        }

        public PlaybackStatus ReportPlaybackStopped(string id, long positionTicks)
        {
            return Kernel.ApiClient.ReportPlaybackStopped(id, Kernel.CurrentUser.Id, positionTicks);
            //return WebSocket.SendPlaybackStopped(id, positionTicks);
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
                Async.Queue("OnNavigationInto", () => _NavigationInto(this, new GenericEventArgs<Item>() { Item = item }));
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

        #region LibraryChanged Notification

        public event EventHandler<LibraryChangedEventArgs> NotifyLibraryChanged;

        public void OnLibraryChanged(LibraryChangedEventArgs args)
        {
            if (NotifyLibraryChanged != null)
            {
                NotifyLibraryChanged(this, args);
            }
        }

        #endregion

        private bool showSplash = false;
        public bool ShowSplash
        {
            get { return showSplash; }
            set
            {
                if (showSplash != value)
                {
                    showSplash = value;
                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => FirePropertyChanged("ShowSplash"));
                }
            }
        }

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
            MessageUI = !string.IsNullOrEmpty(ui) ? ui : CurrentTheme.MsgBox;
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
            return MessageBox(msg, true, Config.DefaultMessageTimeout * 1000, CurrentTheme.YesNoBox);
        }

        public void ProgressBox(string msg)
        {
            MessageBox(msg, false, 0, CurrentTheme.ProgressBox);
        }

        #endregion

        #region Websocket
        public static ApiWebSocket WebSocket { get; private set; }

        private void BrowseRequest(object sender, BrowseRequestEventArgs args)
        {
            ScreenSaverActive = false;

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
                        Information.AddInformationString("Cannot Browse to "+args.Request.ItemName);
                    }
                    break;
                case "Studio":
                    break;
                default:
                    var item = Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(args.Request.ItemId));
                    if (item != null)
                    {
                        Logger.ReportInfo("Navigating to {0} by request from remote client", item.Name);
                        var model = ItemFactory.Instance.Create(item);
                        if (!TVHelper.CreateEpisodeParents(model))
                        {
                            //try to load real parent or attach a default
                            //var parent = !string.IsNullOrEmpty(item.ApiParentId) ? Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(item.ApiParentId)) as Folder : null;
                            item.Parent = new IndexFolder(new List<BaseItem> {item});
                            model.PhysicalParent = ItemFactory.Instance.Create(item.Parent) as FolderModel;
                        }
                        CurrentFolder = model.PhysicalParent;
                        Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => Navigate(model));
                    }
                    else
                    {
                        Logger.ReportWarning("Unable to browse to item {0}", args.Request.ItemId);
                        Information.AddInformationString("Cannot Browse to "+args.Request.ItemName);
                    }
                    break;
            }
        }

        private void GeneralCommand(object sender, GeneralCommandEventArgs args)
        {
            switch (args.Command.Name)
            {
                case "DisplayContent":
                    var newArgs = new BrowseRequestEventArgs {Request = new BrowseRequest {ItemType = args.Command.Arguments["ItemType"], ItemId = args.Command.Arguments["ItemId"], ItemName = args.Command.Arguments["ItemName"]}};
                    BrowseRequest(this, newArgs);
                    break;

                case "Back":
                    Back();
                    break;

                case "GoHome":
                    BackToRoot();
                    break;

                case "GoToSettings":
                    OpenConfiguration(true);
                    break;

                case "Mute":
                    WMCMute = true;
                    break;

                case "Unmute":
                    WMCMute = false;
                    break;

                case "ToggleMute":
                    WMCMute = !WMCMute;
                    break;

            }
        }

        private void SystemCommand(object sender, SystemRequestEventArgs args)
        {
            ScreenSaverActive = false;
            switch (args.Command)
            {
                case "GoHome":
                    BackToRoot();
                    break;

                case "GoToSettings":
                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => OpenConfiguration(true));
                    break;

                case "Mute":
                    WMCMute = true;
                    break;

                case "Unmute":
                    WMCMute = false;
                    break;

                case "ToggleMute":
                    WMCMute = !WMCMute;
                    break;

            }
                
        }

        private void PlayRequest(object sender, PlayRequestEventArgs args)
        {
            ScreenSaverActive = false;

            var item = Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(args.Request.ItemIds.FirstOrDefault() ?? ""));
                    if (item != null)
                    {
                        Logger.ReportInfo("Playing {0} by request from remote client", item.Name);
                        Play(ItemFactory.Instance.Create(item),false, args.Request.PlayCommand != PlayCommand.PlayNow, false, false, args.Request.StartPositionTicks ?? 0);
                    }
                    else
                    {
                        Logger.ReportWarning("Unable to play item {0}", args.Request.ItemIds.FirstOrDefault());
                        Information.AddInformationString("Unable to play requested item");
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
                    currentPlaybackController.Pause();
                    break;

                case PlaystateCommand.Unpause:
                    currentPlaybackController.UnPause();
                    break;

                case PlaystateCommand.Stop:
                    currentPlaybackController.Stop();
                    break;

                case PlaystateCommand.Seek:
                    Logger.ReportVerbose("Got seek message: {0}", args.Request.SeekPositionTicks);
                    currentPlaybackController.Seek(args.Request.SeekPositionTicks ?? currentPlaybackController.CurrentFilePositionTicks);
                    break;
            }
        }

        private void LibraryChanged(object sender, LibraryChangedEventArgs args)
        {
            Logger.ReportVerbose("Library Changed...");
            Logger.ReportVerbose("Folders Added to: {0} Items Added: {1} Items Removed: {2} Items Updated: {3}", args.UpdateInfo.FoldersAddedTo.Count, args.UpdateInfo.ItemsAdded.Count, args.UpdateInfo.ItemsRemoved.Count, args.UpdateInfo.ItemsUpdated.Count);
            var changedFolders = args.UpdateInfo.FoldersAddedTo.Concat(args.UpdateInfo.FoldersRemovedFrom).SelectMany(id => Kernel.Instance.FindItems(new Guid(id))).OfType<Folder>().Where(folder => folder != null).ToList();

            // Get folders that were reported to us
            foreach (var changedItem in changedFolders)
            {
                Logger.ReportVerbose("Folder with changes is: {0}", changedItem.Name);
                changedItem.ReloadChildren();
            }


            //First get all the top folders of removed items
            foreach (var id in args.UpdateInfo.ItemsRemoved)
            {
                var removed = Kernel.Instance.FindItem(new Guid(id));
                if (removed != null)
                {
                    // If our parent wasn't included in the updated list - refresh it
                    var parent = removed.Parent;
                    if (parent != null)
                    {
                        if (!args.UpdateInfo.ItemsUpdated.Contains(parent.ApiId)) parent.RefreshMetadata();
                    }
                }
            }

            // Get changed items and update them
            foreach (var id in args.UpdateInfo.ItemsUpdated)
            {
                var changedItem = Kernel.Instance.FindItem(new Guid(id));
                if (changedItem != null)
                {
                    if (!args.UpdateInfo.ItemsUpdated.Contains(changedItem.Parent.ApiId) && !args.UpdateInfo.FoldersAddedTo.Contains(id) && !args.UpdateInfo.FoldersRemovedFrom.Contains(id))
                    {
                        Logger.ReportVerbose("Item changed is: {0}.  Refreshing.", changedItem.Name);
                        changedItem.RefreshMetadata();
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

            //And reset the recent list for each top folder - just do them all
            foreach (var top in RootFolder.Children.OfType<Folder>())
            {
                top.ResetQuickList();
                top.OnQuickListChanged(null);
            } 

            //Call any subscribers
            OnLibraryChanged(args);

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

        public bool ScreenSaverTempDisabled { get; set; }

        public string CurrentScreenSaver
        {
            get { return Kernel.Instance.ScreenSaverUI; }
        }

        public int CondensedFolderLimit = 25;

        public Item CurrentUser { get; set; }

        private List<Item> _availableUsers; 
        public List<Item> AvailableUsers { get { return _availableUsers ?? (_availableUsers = Kernel.AvailableUsers.Select(u =>ItemFactory.Instance.Create(new User {Name=u.Name, Id = new Guid(u.Id ?? ""),  Dto = u, ParentalAllowed = !u.HasPassword, TagLine = "last seen" + Helper.FriendlyDateStr(u.LastActivityDate ?? DateTime.MinValue)})).ToList()); } } 
        public List<Item> OtherAvailableUsers { get { return AvailableUsers.Where(u => u.Name != CurrentUser.Name).ToList(); } }
        private bool _multipleUsersHere;
        public bool MultipleUsersHere
        {
            get { return _multipleUsersHere; }
            set
            {
                if (_multipleUsersHere != value)
                {
                    _multipleUsersHere = value;
                    FirePropertyChanged("MultipleUsersHere");
                }
            }
        }

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
                    // first try chocolate - fall back to classic
                    var def = AvailableThemes.ContainsKey("Chocolate") ? "Chocolate" : "Classic";
                    Config.Instance.ViewTheme = def;
                    return AvailableThemes[def];
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

        public PluginItemCollection InstalledPluginsCollection { get; set; }
        public PluginItemCollection AvailablePluginsCollection { get; set; }
        protected List<PackageInfo> Packages { get; set; }
        protected bool PackagesRetrieved = false;

        public void RefreshPluginCollections()
        {
            if (Microsoft.MediaCenter.UI.Application.ApplicationThread != Thread.CurrentThread)
            {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => RefreshPluginCollections());
            }
            else
            {
                AvailablePluginsCollection = new PluginItemCollection(Packages);

                var installedPluginItems = new List<PluginItem>();

                foreach (var plugin in Kernel.Instance.Plugins)
                {
                    var current = plugin;
                    var catalogPlugin = AvailablePluginsCollection.Items.FirstOrDefault(i => i.TargetFilename.Equals(current.Filename, StringComparison.OrdinalIgnoreCase));
                    var installedPlugin = new PluginItem(catalogPlugin != null ? catalogPlugin.Info : new PackageInfo
                                                                                                          {
                                                                                                              name = current.Name,
                                                                                                              overview = current.Description,
                                                                                                              targetFilename = current.Filename
                                                                                                          });
                    installedPlugin.InstalledVersion = current.Version.ToString();
                    var catalogVersion = catalogPlugin != null ? catalogPlugin.Versions.FirstOrDefault(v => v.version == current.Version) : null;
                    installedPlugin.InstalledVersionClass = catalogVersion != null ? " (" + catalogVersion.classification.ToString() + ")" : "";
                    if (catalogPlugin != null)
                    {
                        catalogPlugin.InstalledVersion = installedPlugin.InstalledVersion;
                        catalogPlugin.InstalledVersionClass = installedPlugin.InstalledVersionClass;
                        installedPlugin.UpdateAvailable = catalogPlugin.UpdateAvailable = catalogPlugin.ValidVersions.Any(v => v.version > current.Version);
                    }
                    else
                    {
                        installedPlugin.NotInCatalog = true;
                    }

                    installedPluginItems.Add(installedPlugin);

                }

                InstalledPluginsCollection = new PluginItemCollection(installedPluginItems);
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

        public FolderModel BlankFolder {get {return new FolderModel();}}

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
                    if (CurrentFolder != null && CurrentFolder.SelectedChild != null)
                    {
                        return CurrentFolder.SelectedChild;
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

        public Guid CurrentlyPlayingItemId
        {
            get { return _currentlyPlayingItemId; }
            set { 
                _currentlyPlayingItemId = value;
                CurrentlyPlayingItem = null;
            }
        }

        public Item CurrentlyPlayingItem
        {
            get { return _currentlyPlayingItem ?? (_currentlyPlayingItem = GetCurrentlyPlayingItem()); }
            set { _currentlyPlayingItem = value; FirePropertyChanged("CurrentlyPlayingItem"); }
        }

        private Item GetCurrentlyPlayingItem()
        {
            var baseItem = Kernel.Instance.FindItem(_currentlyPlayingItemId) ?? Kernel.Instance.MB3ApiRepository.RetrieveItem(_currentlyPlayingItemId) ?? new BaseItem {Id = _currentlyPlayingItemId, Name = "<Unknown>"};
            var item = ItemFactory.Instance.Create(baseItem);
            TVHelper.CreateEpisodeParents(item);
            return item;
        }

        public string CurrentMenuOption { get; set; }

        private List<MenuItem> currentContextMenu;

        public List<MenuItem> ContextMenu
        {
            get { return currentContextMenu ?? (currentContextMenu = Kernel.Instance.ContextMenuItems); }
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

        public List<MenuItem> UserMenu
        {
            get
            {
                return Kernel.Instance.UserMenuItems;
            }
        }

        private MenuManager menuManager;

        public List<MultiPartPlayOption> MultiPartOptions { get; private set; } 

        public bool NavigatingForward
        {
            get { return navigatingForward; }
            set { navigatingForward = value; }
        }

        public int ConfigPanelIndex { get; set; }

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
            
            //initialize our menu manager
            menuManager = new MenuManager();

            //initialize screen saver
            ScreenSaverTimer = new Timer() { AutoRepeat = true, Enabled = true, Interval = 60000 };
            ScreenSaverTimer.Tick += ScreenSaverTimer_Tick;

            //initialize auto logoff
            IdleTimer = new Timer() { AutoRepeat = true, Enabled = true, Interval = 60000 };
            IdleTimer.Tick += IdleTimer_Tick;
        }

        private void IdleTimer_Tick(object sender, EventArgs e)
        {
            if (LoggedIn && Config.EnableAutoLogoff && !IsPlaying)
            {
                if (Helper.SystemIdleTime > Config.AutoLogoffTimeOut*60000)
                {
                    Logger.ReportInfo("System logging off automatically due to timeout of {0} minutes of inactivity...", Config.AutoLogoffTimeOut);
                    Config.StartupParms = "ShowLogin";
                    Restart();
                }
            }
        }

        void ScreenSaverTimer_Tick(object sender, EventArgs e)
        {
            if (LoggedIn && Config.EnableScreenSaver) 
            {
                if (!IsPlayingVideo && !IsExternalWmcApplicationPlaying && !ScreenSaverTempDisabled)
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

        public IntrosPlaybackController IntroController { get; set; }

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

        /// <summary>
        /// Restart media browser classic
        /// </summary>
        public void Restart()
        {
            var updateBat = "ping 127.0.2.0 -n 1 -w 150 > NUL\r\n"; // give us time to exit
            var windir = Environment.GetEnvironmentVariable("windir") ?? "c:\\windows\\";
            updateBat += Path.Combine(windir, "ehome\\ehshell /entrypoint:{CE32C570-4BEC-4aeb-AD1D-CF47B91DE0B2}\\{FC9ABCCC-36CB-47ac-8BAB-03E8EF5F6F22}");
            var filename = Path.GetTempFileName();
            filename += ".bat";
            File.WriteAllText(filename, updateBat);

            // Start the batch file minimized so they don't notice.
            var toDo = new Process {StartInfo = {WindowStyle = ProcessWindowStyle.Minimized, FileName = filename}};

            toDo.Start();
            Close();
        }

        public void BackOut()
        {
            //back up and close the app if that fails
            if (!session.BackPage())
                Close();
        }

        public void Back()
        {
            if (Config.UseExitMenu && session.AtRoot)
            {
                // show menu
                DisplayExitMenu = true;
            }
            else
            {
                session.BackPage();
            }
        }

        public void SleepMachine()
        {
            DisplayExitMenu = false; //we probably were called from this - don't want it still there when we wake up
            System.Windows.Forms.Application.SetSuspendState(PowerState.Suspend, false, false);
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

                        // Now ask the server to delete it
                        Kernel.ApiClient.DeleteItem(Item.BaseItem.ApiId);

                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error trying to delete {0}", e, Item.Path);
                        mce.Dialog(CurrentInstance.StringData("NotDelUnknownDial"), CurrentInstance.StringData("DelFailedDial"), DialogButtons.Ok, 0, true);
                        return;
                    }
                    DeleteNavigationHelper(Item);
                    // Show a message - the time it takes them to respond to this will hopefully be enough for it to be gone from the server
                    Thread.Sleep(1000);
                    Information.AddInformationString(LocalizedStrings.Instance.GetString("DelServerDial"));
                    //MessageBox(LocalizedStrings.Instance.GetString("DelServerDial"));
                    Thread.Sleep(7000); // in case they dismiss very quickly
                    foreach (var item in Kernel.Instance.FindItems(Item.Id)) item.Parent.RefreshMetadata();
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

        protected bool UsingDirectEntry;

        // Entry point for the app
        public void Init()
        {
            Config.IsFirstRun = false;
            Logger.ReportInfo("Media Browser (version " + AppVersion + ") Starting up.");
            Logger.ReportInfo("Startup parameters: "+ Config.StartupParms);
            Logger.ReportInfo("Server version: "+ Kernel.ServerInfo.Version);
            //let's put some useful info in here for diagnostics
            if (!Config.AutoValidate)
                Logger.ReportWarning("*** AutoValidate is OFF.");
            //Need to track who is still using us
            Helper.Ping("http://www.mb3admin.com/admin/service/registration/ping?feature=MBClassic&ver=" + Kernel.Instance.VersionStr + "&mac=" + Helper.GetMACAddress());
            try
            {
                Updater = new Updater(this);

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
                string splashFilename = Path.Combine(ApplicationPaths.CommonConfigPath, "extsplash.jpg");
                ExtSplashBmp = File.Exists(splashFilename) ? new System.Drawing.Bitmap(splashFilename) : new System.Drawing.Bitmap(Resources.mblogo1000);

                // setup image to use in login splash screen
                splashFilename = Path.Combine(ApplicationPaths.CommonConfigPath, "loginsplash.png");
                LogonSplashImage = File.Exists(splashFilename) ? new Image("file://"+splashFilename) : new Image("resx://MediaBrowser/MediaBrowser.Resources/mblogo1000");

                IntroController = new IntrosPlaybackController();

                Login();
            }
            catch (Exception e)
            {
                AddInHost.Current.MediaCenterEnvironment.Dialog(CurrentInstance.StringData("CriticalErrorDial") + e + " " + e.StackTrace, CurrentInstance.StringData("CriticalErrorCapDial"), DialogButtons.Ok, 60, true);
                AddInHost.Current.ApplicationContext.CloseApplication();
            }
        }

        public bool AutoLogin
        {
            get { return Kernel.Instance.CommonConfigData.LogonAutomatically; }
            set
            {
                if (Kernel.Instance.CommonConfigData.LogonAutomatically != value)
                {
                    Kernel.Instance.CommonConfigData.LogonAutomatically = value;
                    Kernel.Instance.CommonConfigData.AutoLogonUserName = Kernel.CurrentUser.Name;
                    Kernel.Instance.CommonConfigData.AutoLogonPw = Kernel.CurrentUser.PwHash;
                    Kernel.Instance.CommonConfigData.Save();
                    FirePropertyChanged("AutoLogin");
                }
            }
        }

        public string AutoLoginUserName { get { return Kernel.Instance.CommonConfigData.AutoLogonUserName; } }

        public string CurrentServerAddress { get { return Kernel.ApiClient.ServerHostName; } }

        public string ServerAddressOptionString { get { return "Current Server (" + CurrentServerAddress + ")"; } }

        public void Logout(bool force)
        {
            if (force)
            {
                if (AvailableUsers.Count > 1) Config.StartupParms = "ShowLogin";
                Restart();
            }
            else
            {
                Logout();
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
                                              if (AvailableUsers.Count > 1) Config.StartupParms = "ShowLogin";
                                              Restart();
                                          }
                                      });
            

        }

        /// <summary>
        /// Log in to default or show a login screen with choices
        /// </summary>
        public void Login()
        {
            var user = !string.IsNullOrEmpty(Config.StartupParms) ? Config.StartupParms.Equals("ShowLogin", StringComparison.OrdinalIgnoreCase) ? null : AvailableUsers.FirstOrDefault(u => u.Name.Equals(Config.StartupParms, StringComparison.OrdinalIgnoreCase)) : 
                        Kernel.Instance.CommonConfigData.LogonAutomatically ? AvailableUsers.FirstOrDefault(u => u.Name.Equals(Kernel.Instance.CommonConfigData.AutoLogonUserName, StringComparison.OrdinalIgnoreCase)) :
                           Kernel.AvailableUsers.Count == 1 ? AvailableUsers.FirstOrDefault() : null;

            Config.StartupParms = null;

            if (user != null)
            {
                // only one user or specified - log in automatically
                UsingDirectEntry = true;
                OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/SplashPage", new Dictionary<string, object> {{"Application",this}});
                LoginUser(user);
            }
            else
            {
                // validate that we actually have some users
                if (!Kernel.AvailableUsers.Any())
                {
                    AddInHost.Current.MediaCenterEnvironment.Dialog("No user profiles are available.  Please ensure all users are not hidden on the server.", "No Users Found", DialogButtons.Ok, 100, true);
                    Close();
                }
                // show login screen
                session.GoToPage("resx://MediaBrowser/MediaBrowser.Resources/MetroLoginPage", new Dictionary<string, object> {{"Application",this}});
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
                Async.Queue("Load user", () => LoadUser(user.BaseItem as User, ""));
            }
        }

        public void SwitchUser(string userName)
        {
            Config.StartupParms = userName;
            Restart();
        }

        public void SwitchUser(Item ignore)
        {
            //User to switch to will be in CurrentMenuOption
            //Not a huge fan of that but changing the signature of the menu item command would break all themes
            SwitchUser(CurrentMenuOption);
        }

        protected bool LoadUser(User user, string pw)
        {
            ShowSplash = true;
            Kernel.ApiClient.CurrentUserId = user.Id;

            try
            {
                if (!string.IsNullOrEmpty(pw))
                {
                    //Logger.ReportVerbose("Authenticating with pw: {0} ({1})",CustomPINEntry, pw);
                    Kernel.ApiClient.AuthenticateUserWithHash(user.Id, pw);
                    //If we get here the pw was correct - save it so we can use it if automatically logging in
                    user.PwHash = pw;
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
                    if (!UsingDirectEntry)
                    {
                        AddInHost.Current.MediaCenterEnvironment.Dialog("Incorrect Password.", "Access Denied", DialogButtons.Ok, 100, true);
                    }
                    UsingDirectEntry = false;
                    ShowSplash = false;
                    return false;
                }
                throw;
            }

            LoggedIn = true;


            // load user config
            Kernel.Instance.LoadUserConfig();

            //wire up our mouseActiveHooker if enabled so we can know if the mouse is active over us
            Kernel.Instance.MouseActiveHooker.MouseActive += mouseActiveHooker_MouseActive;

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

            // load root
            Kernel.Instance.ReLoadRoot();

            LoadPluginsAndModels();

            // build switch user menu
            BuildUserMenu();

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
                if (Microsoft.MediaCenter.UI.Application.ApplicationThread != Thread.CurrentThread)
                {
                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => LaunchEntryPoint(EntryPointResolver.EntryPointPath));
                }
                else
                {
                    LaunchEntryPoint(EntryPointResolver.EntryPointPath);
                }
            }

            //load plug-in catalog info
            if (user.Dto.Configuration.IsAdministrator)
            {
                Async.Queue("PackageLoad",() =>
                {
                    LoadPackages();
                    RefreshPluginCollections();
                });
                
            }
            return true;
        }

        private void LoadPackages()
        {
            Packages = Kernel.ApiClient.GetPackages() ?? new List<PackageInfo>();
            PackagesRetrieved = true;
        }

        public void UpdateAllPlugins(bool silent = false)
        {
            Async.Queue("Plugin Update", () => Updater.UpdateAllPlugins(InstalledPluginsCollection, silent));
        }

        public void UpdatePlugin(PluginItem plugin)
        {
            Async.Queue("Plugin Update", () => Updater.UpdatePlugin(plugin));
        }

        public void InstallPlugin(PluginItem plugin)
        {
            Async.Queue("Plugin Update", () => Updater.UpdatePlugin(plugin, "Installing"));
        }

        public void RemovePlugin(PluginItem plugin)
        {
            Async.Queue("Plugin Remove", () =>
            {
                if (YesNoBox("Remove Plug-in "+plugin.Name+" - Are you sure?") == "Y")
                {

                    try
                    {
                        Logger.ReportInfo("Removing plug-in {0}/{1}",plugin.Name, plugin.TargetFilename);
                        File.Delete(Path.Combine(ApplicationPaths.AppPluginPath, plugin.TargetFilename));
                        InstalledPluginsCollection.Remove(plugin);
                        // Close the catalog page
                        ShowPluginDetailPage = false;
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error attempting to remove plugin {0}", e, plugin.TargetFilename);
                        Async.Queue("msg", () => MessageBox("Could not delete plugin " + plugin.Name));
                    }
                }
            });
        }

        public void RatePlugin(PluginItem plugin, int rating, bool recommend)
        {
            Async.Queue("PackageRating", () => Kernel.ApiClient.RatePackage(plugin.Id, rating, recommend));
            Information.AddInformationString("Thank you for submitting your rating");
        }

        /// <summary>
        /// Open the dash in the default browser to the proper plugin page for registration
        /// </summary>
        /// <param name="plugin"></param>
        public void RegisterPlugin(PluginItem plugin)
        {
            Async.Queue("Registration", () =>
            {
                if (YesNoBox("The Registration Page will open in your web browser.  You will need a keyboard to complete the transaction.  Continue?") == "Y")
                {
                    Process.Start(Kernel.Instance.DashboardUrl + string.Format("/addplugin.html?name={0}&guid={1}&autosubmit=true",plugin.Name, plugin.GuidString));
                }
                else
                {
                    MessageBox("You can register with any device via the dashboard at " + Kernel.Instance.DashboardUrl + "/plugincatalog.html");
                }
            });
        }

        /// <summary>
        /// Not used.
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns></returns>
        protected string BuildRegisterPage(PluginItem plugin)
        {
            var page = "<html>";
            page += string.Format("<h1 style='text-align: center'>Testing Register {0}</h1><br/>",plugin.Name);

            var form = "<form name=\"_xclick\" action=\"https://www.paypal.com/cgi-bin/webscr\" method=\"post\">";
            form += "<input type=\"hidden\" name=\"cmd\" value=\"_xclick\">";
            form += string.Format("<input type=\"hidden\" id=\"payPalEmail\" name=\"business\" value=\"{0}\">", "donate@ebrsoft.com");
            form += "<input type=\"hidden\" name=\"currency_code\" value=\"USD\">";
            form += string.Format("<input type=\"hidden\" id=\"featureName\" name=\"item_name\" value=\"{0}\">", plugin.FeatureId);
            form += string.Format("<input type=\"hidden\" id=\"amount\" name=\"amount\" value=\"{0}\">", plugin.Price);
            form += string.Format("<input type=\"hidden\" id=\"featureId\" name=\"item_number\" value=\"{0}\">", plugin.FeatureId);
            form += "<input type=\"hidden\" name=\"notify_url\" value=\"http://mb3admin.com/admin/service/services/ppipn.php\">";
            form += "<input type=\"hidden\" name=\"return\" value=\"#\">";
            form += "<a href=\"#\" onclick=\"$(this).parents('form')[0].submit();\"><img src=\"https://www.paypalobjects.com/en_US/i/btn/btn_buynowCC_LG.gif\" /></a>";
            form += "</form>";
            page += form;
            page += "</html>";

            return page;

        }

        protected void BuildUserMenu()
        {
            if (Microsoft.MediaCenter.UI.Application.ApplicationThread != Thread.CurrentThread)
            {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => BuildUserMenu());
            }
            else
            {
                foreach (var otherUser in AvailableUsers.Where(u => u.Name != CurrentUser.Name))
                {
                    Kernel.Instance.AddMenuItem(new MenuItem(otherUser.Name, otherUser.BaseItem.PrimaryImagePath ?? "resx://MediaBrowser/MediaBrowser.Resources/UserLoginDefault", SwitchUser, new List<MenuType> { MenuType.User }));
                }
            }
        }

        protected void LoadPluginsAndModels()
        {
            if (Microsoft.MediaCenter.UI.Application.ApplicationThread != Thread.CurrentThread)
            {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => LoadPluginsAndModels());
            }
            else
            {
                // add plug-ins config panel if admin
                if (Kernel.CurrentUser.Dto.Configuration.IsAdministrator)
                    Kernel.Instance.AddConfigPanel(LocalizedStrings.Instance.GetString("PluginsConfig"),"resx://MediaBrowser/MediaBrowser.Resources/AdvancedConfigPanel#PluginsPanel");
                
                // load plugins
                Kernel.Instance.LoadPlugins();

                // add advanced config panel to end
                if (Kernel.CurrentUser.Dto.Configuration.IsAdministrator)
                    Kernel.Instance.AddConfigPanel(LocalizedStrings.Instance.GetString("AdvancedConfig"),"resx://MediaBrowser/MediaBrowser.Resources/AdvancedConfigPanel#AdvancedPanel");
                
                //populate the config model choice
                ConfigModel = new Choice { Options = ConfigPanelNames };

            }
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
                    try
                    {
                        Kernel.ApiClient.ReportRemoteCapabilities();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error reporting remote capabilities to server.",e);
                    }
                    WebSocket.LibraryChanged += LibraryChanged;
                    WebSocket.BrowseCommand += BrowseRequest;
                    WebSocket.PlayCommand += PlayRequest;
                    WebSocket.PlaystateCommand += PlayStateRequest;
                    WebSocket.SystemCommand += SystemCommand;
                    WebSocket.GeneralCommand += GeneralCommand;
                  
                    if (Config.EnableUpdates && Config.EnableSilentUpdates && !RunningOnExtender)
                    {
                        Async.Queue(Async.STARTUP_QUEUE, () =>
                                                             {
                                                                 while (!PackagesRetrieved) {Thread.Sleep(500);}
                                                                 RefreshPluginCollections();
                                                                 while (InstalledPluginsCollection == null) {Thread.Sleep(500);}
                                                                 UpdateAllPlugins(true);
                                                             });
                    }

                    if (Kernel.CurrentUser.Dto.Configuration.IsAdministrator) // don't show these prompts to non-admins
                    {
                        // We check config here instead of in the Updater class because the Config class 
                        // CANNOT be instantiated outside of the application thread.
                        if (Config.EnableUpdates && !RunningOnExtender)
                        {
                            Async.Queue(Async.STARTUP_QUEUE, CheckForSystemUpdate, 10000);
                        }

                        // Let the user know if the server needs to be restarted
                        // Put it on the same thread as the update checks so it will be behind them
                        Async.Queue(Async.STARTUP_QUEUE, () =>
                                                             {
                                                                 if (Kernel.ServerInfo.HasPendingRestart)
                                                                 {
                                                                     if (Kernel.ServerInfo.CanSelfRestart)
                                                                     {
                                                                         if (YesNoBox("The MB Server needs to re-start to apply an update.  Restart now?") == "Y")
                                                                         {
                                                                             Kernel.ApiClient.PerformPendingRestart();
                                                                             MessageBox("Your server is being re-started.  MB Classic will now exit so you can re load it.");
                                                                             Close();
                                                                         }
                                                                     }
                                                                     else
                                                                     {
                                                                        MessageBox("Your server needs to be re-started to apply an update.  You must re-start it from the server machine.");
                                                                     }
                                                                 }
                                                             },35000);
                        
                    }

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
                                                    if (!Config.EnableSilentUpdates) PluginUpdatesAvailable = Updater.PluginUpdatesAvailable();
                                                    SystemUpdateCheckInProgress = false;
                                                });
            }
        }

        bool FirstRunForVersion(string thisVersion)
        {
            Updater.WriteToUpdateLog(String.Format("New MBC Version {0} successfully run for first time.", thisVersion));
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

                case "3.0.52.0":
                    //Clear the image cache out
                    Logger.ReportInfo("Clearing image cache...");
                    try
                    {
                        Directory.Delete(ApplicationPaths.AppImagePath, true);
                        Thread.Sleep(1000); //wait for the delete to fiinish
                    }
                    catch (Exception e) { Logger.ReportException("Error trying to clear image cache.", e); } //just log it
                    try
                    {
                        Directory.CreateDirectory(ApplicationPaths.AppImagePath);
                        Thread.Sleep(500); //wait for the directory to create
                        Directory.CreateDirectory(ApplicationPaths.CustomImagePath);
                    }
                    catch (Exception e) { Logger.ReportException("Error trying to create image cache.", e); } //just log it
                    break;

                case "3.0.83.0":
                case "3.0.86.0":
                case "3.0.87.0":
                case "3.0.88.0":
                    // Re-set login background
                    Kernel.Instance.CommonConfigData.LoginBgColor = "Black";
                    Kernel.Instance.CommonConfigData.Save();
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

        private bool displayUserMenu = false;
        public bool DisplayUserMenu
        {
            get
            {
                return this.displayUserMenu;
            }
            set
            {
                this.displayUserMenu = value;
                FirePropertyChanged("DisplayUserMenu");
            }
        }

        private bool _displayMultiMenu;
        public bool DisplayMultiMenu
        {
            get
            {
                return this._displayMultiMenu;
            }
            set
            {
                this._displayMultiMenu = value;
                FirePropertyChanged("DisplayMultiMenu");
            }
        }

        private bool _displayExitMenu;
        public bool DisplayExitMenu
        {
            get
            {
                return this._displayExitMenu;
            }
            set
            {
                this._displayExitMenu = value;
                FirePropertyChanged("DisplayExitMenu");
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

        private bool _showPluginDetailPage;
        public bool ShowPluginDetailPage
        {
            get { return this._showPluginDetailPage; }
            set
            {
                if (_showPluginDetailPage != value)
                {
                    _showPluginDetailPage = value;
                    FirePropertyChanged("ShowPluginDetailPage");
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
            IsMouseActive = e.MouseActive;
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

        public void OpenCatalogPage()
        {
            var properties = new Dictionary<string, object>();
            properties["Application"] = this;

            if (session != null)
            {
                session.GoToPage("resx://MediaBrowser/MediaBrowser.Resources/PluginCatalog#PluginCatalog", properties);
            }
            else
            {
                Logger.ReportError("Session is null in OpenPage");
            }
            
        }

        public void OpenConfiguration(bool showFullOptions)
        {
            if (Microsoft.MediaCenter.UI.Application.ApplicationThread != Thread.CurrentThread)
            {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => OpenConfiguration(showFullOptions));
            }
            else
            {
                var properties = new Dictionary<string, object>();
                properties["Application"] = this;
                properties["ShowFull"] = showFullOptions;

                if (session != null)
                {
                    session.GoToPage("resx://MediaBrowser/MediaBrowser.Resources/ConfigPage", properties);
                    if (AvailablePluginsCollection == null || !AvailablePluginsCollection.Items.Any())
                    if (PackagesRetrieved)
                    {
                        RefreshPluginCollections();
                    }
                    else
                    {
                        InstalledPluginsCollection = new PluginItemCollection(new List<PluginItem>());
                        AvailablePluginsCollection = new PluginItemCollection(new List<PluginItem>());
                    }
                }
                else
                {
                    Logger.ReportError("Session is null in OpenPage");
                }
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

        private PluginItem _currentPluginItem;
        public PluginItem CurrentPluginItem
        {
            get { return _currentPluginItem; }
            set
            {
                if (_currentPluginItem != value)
                {
                    _currentPluginItem = value;
                    FirePropertyChanged("CurrentPluginItem");
                }
            }
        }

        public void OpenFolderPage(FolderModel folder)
        {
            var properties = new Dictionary<string, object>();
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
            var currentIndex = item as Index;
            return currentIndex ?? (Folder)RootFolder;
        }

        void NavigateToActor(Item item)
        {
            var person = item.BaseItem as Person;
            if (person != null)
            {
                NavigateToPerson(person.Name, new string[] {"Actor"});
            }
        }

        public void NavigateToDirector(string director, Item currentMovie)
        {
            NavigateToPerson(director, new [] {"Director"});
        }

        void NavigateToPerson(string name, string[] personTypes)
        {
            Async.Queue("Person navigation", () =>
                                                 {
                                                    ProgressBox(string.Format("Finding items with {0} in them...", name));
                                                    

                                                    var query = new ItemQuery
                                                                    {
                                                                        UserId = Kernel.CurrentUser.Id.ToString(),
                                                                        Fields = MB3ApiRepository.StandardFields,
                                                                        Person = name,
                                                                        PersonTypes = personTypes,
                                                                        Recursive = true
                                                                    };
                                                     var person = Kernel.Instance.MB3ApiRepository.RetrievePerson(name) ?? new Person();
                                                    var index = new SearchResultFolder(Kernel.Instance.MB3ApiRepository.RetrieveItems(query).ToList()) {Name = name, Overview = person.Overview};
                                                    ShowMessage = false;

                                                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>Navigate(ItemFactory.Instance.Create(index)));
                                                     
                                                 });
            
        }


        public void NavigateToGenre(string genre, Item currentMovie)
        {
            var itemType = "Movie";

            switch (currentMovie.BaseItem.GetType().Name)
            {
                case "Series":
                case "Season":
                case "Episode":
                    itemType = "Series";
                    break;

                case "MusicAlbum":
                case "MusicArtist":
                case "Song":
                    itemType = "MusicAlbum";
                    break;

                case "Game":
                    itemType = "Game";
                    break;
            }

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
                                                                        IncludeItemTypes = new [] {itemType},
                                                                        Recursive = true
                                                                    };
                                                    var index = new SearchResultFolder(Kernel.Instance.MB3ApiRepository.RetrieveItems(query).ToList()) {Name = genre};
                                                    ShowMessage = false;

                                                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => Navigate(ItemFactory.Instance.Create(index)));
                                                });
        }

        public void NavigateToSimilar(Item item)
        {
            var itemType = "Movies";

            switch (item.BaseItem.GetType().Name)
            {
                case "Series":
                case "Season":
                case "Episode":
                    itemType = "Shows";
                    break;

                case "MusicAlbum":
                case "MusicArtist":
                case "Song":
                    itemType = "Albums";
                    break;

                case "Game":
                    itemType = "Games";
                    break;
            }

            Async.Queue("Similar navigation", () =>
                                                {
                                                    ProgressBox(string.Format("Finding {0} similar to {1}...", itemType, item.Name));

                                                    var query = new SimilarItemsQuery
                                                                    {
                                                                        UserId = Kernel.CurrentUser.Id.ToString(),
                                                                        Fields = MB3ApiRepository.StandardFields,
                                                                        Id = item.BaseItem.ApiId,
                                                                        Limit = 25
                                                                        
                                                                    };
                                                    var items = Kernel.Instance.MB3ApiRepository.RetrieveSimilarItems(query, itemType).ToList();
                                                    // Preserve the order of scoring
                                                    var i = 0;
                                                    foreach (var thing in items)
                                                    {
                                                        thing.SortName = i.ToString("000");
                                                        i++;
                                                    }
                                                    var index = new SearchResultFolder(items) {Name = LocalizedStrings.Instance.GetString("SimilarTo")+item.Name};
                                                    ShowMessage = false;

                                                    if (index.Children.Any())
                                                    {
                                                        Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => Navigate(ItemFactory.Instance.Create(index)));
                                                    }
                                                    else
                                                    {
                                                        MessageBox("No Items Found.");
                                                    }
                                                });
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
                if ((item.HasDataForDetailPage) ||
                    this.Config.AlwaysShowDetailsPage)
                {
                    item.NavigatingInto();
                    // go to details screen 
                    var properties = new Dictionary<string, object>();
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

        public void OpenCustomPlayerUi()
        {
            var properties = new Dictionary<string, object>();
            properties["Application"] = this;
            session.GoToPage("resx://MediaBrowser/MediaBrowser.Resources/CustomPlayer", properties);
        }

        public void OpenMCMLPage(string page, Dictionary<string, object> properties)
        {
            if (Microsoft.MediaCenter.UI.Application.ApplicationThread != Thread.CurrentThread)
            {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => OpenMCMLPage(page, properties));
            }
            else
            {
                currentContextMenu = null; //good chance this has happened as a result of a menu item selection so be sure this is reset
                session.GoToPage(page, properties);
            }
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

        public void ClearAllQuickLists()
        {
            foreach (var folder in RootFolder.Children.OfType<Folder>())
            {
                folder.ResetQuickList();
                folder.OnQuickListChanged(null);
            }
        }

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

        public void PlayMultiItem(MultiPartPlayOption chosen)
        {
            Play(chosen.ItemToPlay, false, false, false, false, chosen.Resume ? chosen.ItemToPlay.PlayState.PositionTicks : 0, true);
        }

        /// <summary>
        /// Play an item starting at a specific position
        /// </summary>
        /// <param name="item"></param>
        /// <param name="pos"></param>
        public void Play(Item item, long pos)
        {
            Play(item, false, false, false, false, pos);
        }

        public void Play(Item item, bool resume, bool queue, bool? playIntros, bool shuffle)
        {
                Play(item, resume, queue, playIntros, shuffle, 0);
        }

        public void Play(Item item, bool resume, bool queue, bool? playIntros, bool shuffle, long startPos, bool forceFirstPart = false)
        {
            //if external display a message
            if (item.IsExternalDisc)
            {
                DisplayDialog("Item is an external disc.  Please insert the proper disc.", "Cannot Play");
                return;
            }

            //if playback is disabled display a message
            if (!PlaybackEnabled)
            {
                DisplayDialog("Playback is disabled.  You may need to register your current theme.", "Cannot Play");
                return;
            }

            //or if the item is offline
            if (item.BaseItem.LocationType == LocationType.Offline)
            {
                if (!Directory.Exists(Path.GetDirectoryName(item.Path) ?? ""))
                {
                    DisplayDialog("Item is off-line.", "Cannot Play");
                    return;
                }
                else
                {
                    item.BaseItem.LocationType = LocationType.FileSystem;
                    item.BaseItem.IsOffline = false;
                }
            }

            //or virtual
            if (item.BaseItem.LocationType == LocationType.Virtual)
            {
                DisplayDialog("Item is not actually in your collection.", "Cannot Play");
                return;
            }

            //or otherwise disabled
            if (!item.IsPlayable)
            {
                DisplayDialog("Item is not playable at this time.", "Cannot Play");
                return;
            }

            //special handling for photos
            if (item.BaseItem is Photo || item.BaseItem is PhotoFolder)
            {
                MBPhotoController.Instance.SlideShow(item);
                return;
            }

            //multi-part handling
            if (item.HasAdditionalParts && !forceFirstPart)
            {
                //if we are being asked to resume - automatically resume the proper part
                if (resume)
                {
                    if (!item.CanResumeMain) item = item.AdditionalParts.FirstOrDefault(p => p.CanResume) ?? item;
                }
                else
                {

                    //now, if we are not in ripped media form, just automatically play the concatenated parts
                    var video = item.BaseItem as Video;
                    if (video != null && !video.ContainsRippedMedia && !item.ItemTypeString.StartsWith("Game", StringComparison.OrdinalIgnoreCase))
                    {
                        //assemble all parts
                        var allparts = ItemFactory.Instance.Create(new IndexFolder((new List<BaseItem> {item.BaseItem}).Concat(item.BaseItem.AdditionalParts).ToList()));
                        PlayMultiItem(new MultiPartPlayOption { Name = LocalizedStrings.Instance.GetString("PlayDetail") + " " + LocalizedStrings.Instance.GetString("AllParts"), ItemToPlay = allparts });
                        return;
                    }
                    else
                    {
                        //build playback options
                        MultiPartOptions = new List<MultiPartPlayOption>();
                        
                        //then - the main item + resume if available
                        if (item.CanResumeMain) MultiPartOptions.Add(new MultiPartPlayOption {Name = LocalizedStrings.Instance.GetString("ResumeDetail") + " " + LocalizedStrings.Instance.GetString("Part") + " 1", ItemToPlay = item, Resume = true});
                        MultiPartOptions.Add(new MultiPartPlayOption {Name = LocalizedStrings.Instance.GetString("PlayDetail") + " " + LocalizedStrings.Instance.GetString("Part") + " 1", ItemToPlay = item, Resume = false});

                        //now all additional parts
                        var n = 2;
                        foreach (var part in item.AdditionalParts)
                        {
                            if (part.CanResume) MultiPartOptions.Add(new MultiPartPlayOption {Name = LocalizedStrings.Instance.GetString("ResumeDetail") + " " + LocalizedStrings.Instance.GetString("Part") + " " + n, ItemToPlay = part, Resume = true});
                            MultiPartOptions.Add(new MultiPartPlayOption {Name = LocalizedStrings.Instance.GetString("PlayDetail") + " " + LocalizedStrings.Instance.GetString("Part") + " " + n++, ItemToPlay = part, Resume = false});
                        }

                        //just display menu - it will take it from there
                        FirePropertyChanged("MultiPartOptions");
                        DisplayMultiMenu = true;
                        return;
                    }
                }
            }

            if (Config.WarnOnStream && !item.IsFolder && !item.IsRemoteContent && !Directory.Exists(Path.GetDirectoryName(item.Path) ?? ""))
            {
                Logger.ReportWarning("Unable to directly access {0}.  Attempting to stream.", item.Path);

                Async.Queue("Access Error", () => MessageBox("Could not access media. Will attempt to stream.  Use UNC paths or path substitution on server and ensure this machine can access them.", true, 10000));

            }

            var playable = PlayableItemFactory.Instance.Create(item);

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
            CurrentlyPlayingItemId = playable.CurrentMedia.Id;

            PlaySecure(playable);
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
            if (playableItem.EnablePlayStateSaving && playableItem.HasMediaItems)
            {
                // cause the RAL to re-load if set to watched or un-watched
                if (RecentItemOption == "watched" || RecentItemOption == "unwatched")
                {
                    Async.Queue("quicklist update", () => { foreach (var item in playableItem.PlayedMediaItems.Select(i => i.TopParent).Where(i => i != null).Distinct()) UpdateQuicklist(item); });
                }
            }

            Logger.ReportVerbose("Firing Application.PlaybackFinished for: " + playableItem.DisplayName);

            OnPlaybackFinished(playableItem);
        }

        protected void UpdateQuicklist(Folder item)
        {
            item.ResetQuickList();
            item.OnQuickListChanged(null);
        }

        public bool LoggedIn { get; set; } //used to tell if we have logged in successfully

        public bool RequestingPIN { get; set; } //used to signal the app that we are asking for PIN entry

        public string CustomPINEntry { get; set; } //holds the entry for a custom pin (entered by user to compare to pin)

        public void ParentalPINEntered()
        {
            RequestingPIN = false;
            Async.Queue("Load user", () => LoadUser(CurrentUser.BaseItem as User, BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(CustomPINEntry)))));
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
            while (!session.AtRoot)
            {
                session.BackPage();
            }
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
        private Item _currentlyPlayingItem;
        private Guid _currentlyPlayingItemId;

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


        public string MainBackdrop
        {
            get { return RootFolder.BackdropImagePath; }
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
        public void UpdatePlayState(Media media, PlaybackStatus playstate, bool isPaused, bool saveToDataStore)
        {
            if (saveToDataStore)
            {
                ReportPlaybackProgress(media.ApiId, playstate.PositionTicks, isPaused, currentPlaybackController.IsStreaming);
            }
        }
    }
}
