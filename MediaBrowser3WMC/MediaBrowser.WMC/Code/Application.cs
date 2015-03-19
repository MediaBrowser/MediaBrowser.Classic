using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
using MediaBrowser.Library.Registration;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.UI;
using MediaBrowser.Library.Util;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Connect;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Plugins;
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
        public Config Config
        {
            get
            {
                return Config.Instance;
            }
        }

        public UserConfig ServerUserConfig { get; set; }

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
        private Microsoft.MediaCenter.Hosting.AddInHost addinHost;
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

        private new void UIFirePropertyChange(string property)
        {
            Application.UIDeferredInvokeIfRequired(() =>
            {
                base.FirePropertyChanged(property);
            }
            );
        }

        public bool UpdateAvailable
        {
            get { return _updateAvailable; }
            set { _updateAvailable = value; UIFirePropertyChange("UpdateAvailable"); }
        }

        public PowerSettings PowerSettings { get { return _powerSetings ?? (_powerSetings = new PowerSettings()); } }
        public DeviceId DeviceId = new DeviceId();

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
            UIFirePropertyChange("CurrentItem");

            // send context message
            //"Context"
            Async.Queue(Async.ThreadPoolName.Context, () =>
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
                //"OnCurrentItemChanged"
                Async.Queue(Async.ThreadPoolName.OnCurrentItemChanged, () => _CurrentItemChanged(this, new GenericEventArgs<Item>() { Item = CurrentItem })); 
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
                UpdateProgress();
            }
        }

        public void WmcVolumeUp()
        {
            MediaCenterEnvironment.AudioMixer.VolumeUp();
            UpdateProgress();
        }

        public void WmcVolumeDown()
        {
            MediaCenterEnvironment.AudioMixer.VolumeDown();
            UpdateProgress();
        }

        public int VolumePct { get; private set; }

        /// <summary>
        /// Set volume to specific level 0-50
        /// </summary>
        /// <param name="amt"></param>
        public void SetWmcVolume(int amt)
        {
            //There is no method to do this directly so we have to fake it
            var diff = (int)(MediaCenterEnvironment.AudioMixer.Volume / 1310.7) - amt;
            if (diff > 0)
            {
                for (var i = 0; i < diff; i++)
                {
                    MediaCenterEnvironment.AudioMixer.VolumeDown();
                    Thread.Sleep(5);
                }
            }
            else
            {
                diff = Math.Abs(diff);
                for (var i = 0; i < diff; i++)
                {
                    MediaCenterEnvironment.AudioMixer.VolumeUp();
                    Thread.Sleep(5);
                }
            }

            UpdateProgress();
        }

        protected void UpdateProgress()
        {
            if (currentPlaybackController != null && currentPlaybackController.IsPlaying)
            {
                ReportPlaybackProgress(currentPlaybackController.GetCurrentPlayableItem().CurrentMedia.ApiId, currentPlaybackController.CurrentFilePositionTicks, currentPlaybackController.IsPaused, currentPlaybackController.IsStreaming);
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
                Async.Queue(Async.ThreadPoolName.OnNavigationInto, () => _NavigationInto(this, new GenericEventArgs<Item>() { Item = item }));
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
            Async.Queue(Async.ThreadPoolName.IsPlayingVideoDelay, () => { UIFirePropertyChange("IsPlayingVideo"); UIFirePropertyChange("IsPlaying"); }, 1500);
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
                Async.Queue(Async.ThreadPoolName.OnPlaybackFinished, () => _PlaybackFinished(this, new GenericEventArgs<PlayableItem>() { Item = playableItem })); 
            }
            UIFirePropertyChange("IsPlayingVideo");
            UIFirePropertyChange("IsPlaying");
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
                UIFirePropertyChange("ShowNewItemPopout");
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
                    UIFirePropertyChange("NewItem");
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
                    UIFirePropertyChange("ShowSplash");
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
                    UIFirePropertyChange("ShowMessage");
                }
            }
        }

        private Timer _popoutMessageTimer = new Timer {AutoRepeat = false, Enabled = false, Interval = 8000};
        private void _popoutMessageTimerTick(object sender, EventArgs args)
        {
            ShowMessagePopout = false;
        }

        private bool showMessagePopout = false;
        public bool ShowMessagePopout
        {
            get { return showMessagePopout; }
            set
            {
                if (showMessagePopout != value)
                {
                    showMessagePopout = value;
                    if (value) _popoutMessageTimer.Start();
                    UIFirePropertyChange("ShowMessagePopout");
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
                    UIFirePropertyChange("MessageText");
                }
            }
        }

        private string messageTitle = "";
        public string MessageTitle
        {
            get
            {
                return messageTitle;
            }
            set
            {
                if (messageTitle != value)
                {
                    messageTitle = value;
                    UIFirePropertyChange("MessageTitle");
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
                    UIFirePropertyChange("MessageUI");
                }
            }
        }

        protected string MessageBox(string msg, bool modal, int timeout, string ui)
        {
            MessageUI = !string.IsNullOrEmpty(ui) ? ui : LoggedIn ? CurrentTheme.MsgBox : MessageUI;
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
                    Async.Queue(Async.ThreadPoolName.CustomMsg, () => WaitForMessage(timeout));
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

        public void PopoutMessage(string header, string message)
        {
            MessageTitle = header;
            MessageText = message;
            ShowMessagePopout = true;
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
                case "MusicGenre":
                    Logger.ReportInfo("Navigating to music genre {0} by request from remote client", args.Request.ItemName);
                    NavigateToGenre(args.Request.ItemName, CurrentItem, "MusicAlbum");
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
                        Application.UIDeferredInvokeIfRequired(()=>Navigate(model));
                    }
                    else
                    {
                        Logger.ReportWarning("Unable to browse to item {0}", args.Request.ItemId);
                        Information.AddInformationString("Cannot Browse to "+args.Request.ItemName);
                    }
                    break;
            }
        }

        private void GeneralCommand(object sender, Model.Events.GenericEventArgs<GeneralCommandEventArgs> generic)
        {
            var args = generic.Argument;

            switch (args.Command.Name)
            {
                case "DisplayContent":
                    var newArgs = new BrowseRequestEventArgs {Request = new BrowseRequest {ItemType = args.Command.Arguments["ItemType"], ItemId = args.Command.Arguments["ItemId"], ItemName = args.Command.Arguments["ItemName"]}};
                    BrowseRequest(this, newArgs);
                    break;

                case "DisplayMessage":
                    PopoutMessage(args.Command.Arguments["Header"], args.Command.Arguments["Text"]);
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

                case "VolumeUp":
                    WmcVolumeUp();
                    break;

                case "VolumeDown":
                    WmcVolumeDown();
                    break;

                case "SetVolume":
                    var amt = args.Command.Arguments["Volume"];
                    SetWmcVolume(Convert.ToInt32(amt) / 2);
                    break;

                case "ToggleMute":
                    WMCMute = !WMCMute;
                    break;

                case "MoveLeft":
                    Helper.ActivateMediaCenter();
                    Thread.Sleep(50);
                    try
                    {
                        Helper.SendKeyLeft();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error sending left key",e);
                    }
                    break;

                case "MoveRight":
                    Helper.ActivateMediaCenter();
                    Thread.Sleep(50);
                    try
                    {
                        Helper.SendKeyRight();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error sending right key",e);
                    }
                    break;

                case "MoveUp":
                    Helper.ActivateMediaCenter();
                    Thread.Sleep(50);
                    try
                    {
                        Helper.SendKeyUp();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error sending up key",e);
                    }
                    break;

                case "MoveDown":
                    Helper.ActivateMediaCenter();
                    Thread.Sleep(50);
                    try
                    {
                        Helper.SendKeyDown();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error sending down key",e);
                    }
                    break;

                case "PageUp":
                    Helper.ActivateMediaCenter();
                    Thread.Sleep(50);
                    try
                    {
                        Helper.SendKeyPageUp();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error sending page up key",e);
                    }
                    break;

                case "PageDown":
                    Helper.ActivateMediaCenter();
                    Thread.Sleep(50);
                    try
                    {
                        Helper.SendKeyPageDown();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error sending page down key",e);
                    }
                    break;

                case "Select":
                    Helper.ActivateMediaCenter();
                    Thread.Sleep(50);
                    try
                    {
                        Helper.SendKeyEnter();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error sending enter key",e);
                    }
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
                    Application.UIDeferredInvokeIfRequired(()=>OpenConfiguration(true));
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

            if (args.Request.ItemIds.Length > 1)
            {
                var items = args.Request.ItemIds.Select(i => Kernel.Instance.MB3ApiRepository.RetrieveItem(i));
                Logger.ReportInfo("Playing multiple items by request from remote client");
                Play(ItemFactory.Instance.Create(new IndexFolder(items.Where(i => i != null).ToList())), false, args.Request.PlayCommand != PlayCommand.PlayNow, false, false);
            }
            else
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
                    Information.AddInformationString("Unable to play requested item");
                }
                
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

                case PlaystateCommand.NextTrack:
                    currentPlaybackController.NextTrack();
                    break;

                case PlaystateCommand.PreviousTrack:
                    currentPlaybackController.PrevTrack();
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

            //And reset the recent list for and reload each top folder if anything was added or removed - just do them all
            if (args.UpdateInfo.ItemsAdded.Any() || args.UpdateInfo.ItemsRemoved.Any())
            {
                foreach (var top in RootFolder.Children.OfType<Folder>())
                {
                    top.ReloadChildren();
                    top.ResetQuickList();
                    top.OnQuickListChanged(null);
                }
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
                UIFirePropertyChange("PluginUpdatesAvailable");
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
                    UIFirePropertyChange("SystemUpdateCheckInProgress");
                }
            }
        }

        private bool _ScreenSaverActive = false;

        public bool ScreenSaverActive
        {
            get { return _ScreenSaverActive; }
            set { if (_ScreenSaverActive != value) { _ScreenSaverActive = value; UIFirePropertyChange("ScreenSaverActive"); } }
        }

        public bool ScreenSaverTempDisabled { get; set; }

        public string CurrentScreenSaver
        {
            get { return Kernel.Instance.ScreenSaverUI; }
        }

        public int CondensedFolderLimit = 25;

        public Item CurrentUser { get; set; }

        private List<Item> _availableServers;
        public List<Item> AvailableServers { get { return _availableServers ?? (_availableServers = Kernel.KnownServers.Values.Select(s => ItemFactory.Instance.Create(new Server { Name = s.Name, Id = new Guid(s.Id), Info = s, TagLine = s.UserLinkType != null ? s.RemoteAddress : s.LocalAddress })).ToList()); } } 
        private List<Item> _availableUsers; 
        public List<Item> AvailableUsers { get { return _availableUsers ?? (_availableUsers = Kernel.AvailableUsers.Select(u =>ItemFactory.Instance.Create(new User {Name=u.Name, Id = new Guid(u.Id ?? ""),  Dto = u, ParentalAllowed = !u.HasPassword, TagLine = "last seen" + Helper.FriendlyDateStr((u.LastActivityDate ?? DateTime.MinValue).ToLocalTime())})).ToList()); } } 
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
                    UIFirePropertyChange("MultipleUsersHere");
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
                UIFirePropertyChange("CurrentThemeStatus");
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
            Application.UIDeferredInvokeIfRequired(() =>
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
            );
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
                if (_currentlyPlayingItemId != value)
                {
                    //Logger.ReportVerbose("************* Updating currently playing item to {0}",value);
                    _currentlyPlayingItemId = value;
                    CurrentlyPlayingItem = null;
                    UIFirePropertyChange("CurrentlyPlayingItem");
                }
            }
        }

        public Item CurrentlyPlayingItem
        {
            get { return _currentlyPlayingItem ?? (_currentlyPlayingItem = GetCurrentlyPlayingItem()); }
            set { _currentlyPlayingItem = value; UIFirePropertyChange("CurrentlyPlayingItem"); }
        }

        private Item GetCurrentlyPlayingItem()
        {
            var baseItem = Kernel.Instance.FindItem(_currentlyPlayingItemId) ?? Kernel.Instance.MB3ApiRepository.RetrieveItem(_currentlyPlayingItemId) ?? new Movie {Id = _currentlyPlayingItemId, Name = "<Unknown>"};
            var item = ItemFactory.Instance.Create(baseItem);
            TVHelper.CreateEpisodeParents(item);
            item.SeekPositionIndex = 0;
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
                UIFirePropertyChange("ContextMenu");
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
            this.addinHost = host;
            if (session != null)
            {
                this.session.Application = this;
            }
            singleApplicationInstance = this;

            //wire up our mouseActiveHooker so we can know if the mouse is active
            if (Config.EnableMouseHook)
            {
                Kernel.Instance.MouseActiveHooker.MouseActive += mouseActiveHooker_MouseActive;
            }

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
            Logger.ReportVerbose("Keep alive call, IsCurretntlyVisible={0}", Application.ApplicationContext.IsCurrentlyVisible);

            if (LoggedIn && Config.EnableAutoLogoff && !IsPlaying)
            {
                if (Helper.SystemIdleTime > Config.AutoLogoffTimeOut * 60000)
                {
                    Logger.ReportInfo("System logging off automatically due to timeout of {0} minutes of inactivity...", Config.AutoLogoffTimeOut);
                    Config.StartupParms = "ShowLogin";
                    if (Application.ApplicationContext.IsCurrentlyVisible) 
                        Restart();
                    else 
                        Close();
                }
            }
            
            
        }

        void ScreenSaverTimer_Tick(object sender, EventArgs e)
        {
            if (LoggedIn && Config.EnableScreenSaver) 
            {
                if ((!IsPlayingVideo || PlaybackController.IsPaused) && !ScreenSaverTempDisabled)
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
            MediaCenterEnvironment ev = Application.MediaCenterEnvironment;
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
                        Dictionary<string, object> capabilities = Application.MediaCenterEnvironment.Capabilities;

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

                return _RunningOnExtender ?? false;

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
            Application.ApplicationContext.CloseApplication();
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

        public static Microsoft.MediaCenter.Hosting.AddInHost AddInHost
        {
            get
            {
                System.Diagnostics.Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == System.Threading.Thread.CurrentThread);
                if (Microsoft.MediaCenter.UI.Application.ApplicationThread != Thread.CurrentThread)
                    Debugger.Break();
                if (Microsoft.MediaCenter.UI.Application.ApplicationThread != System.Threading.Thread.CurrentThread)
                {
                    StackTrace stk = new StackTrace();
                    Logger.ReportWarning("AddInHost accessed from non-UI thread.\n" + stk.ToString());
                }
                return Application.CurrentInstance.addinHost;
            }
        }

        public static Microsoft.MediaCenter.Hosting.ApplicationContext ApplicationContext
        {
            get
            {
                //System.Diagnostics.Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == System.Threading.Thread.CurrentThread);
                if (Microsoft.MediaCenter.UI.Application.ApplicationThread != System.Threading.Thread.CurrentThread)
                {
                    StackTrace stk = new StackTrace();
                    Logger.ReportWarning("ApplicationContext accessed from non-UI thread.\n" + stk.ToString());
                }
                if (Application.AddInHost == null)
                {
                    StackTrace stk = new StackTrace();
                    Logger.ReportError("AddInHost is null\n" + stk.ToString());
                }
                return Application.AddInHost.ApplicationContext;
            }
        }

        public static MediaCenterEnvironment MediaCenterEnvironment
        {
            get
            {
                //System.Diagnostics.Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == System.Threading.Thread.CurrentThread);
                if (Microsoft.MediaCenter.UI.Application.ApplicationThread != System.Threading.Thread.CurrentThread)
                {
                    StackTrace stk = new StackTrace();
                    Logger.ReportWarning("MediaCenterEnvironment accessed from non-UI thread.\n" + stk.ToString());
                }
                if (Application.AddInHost == null)
                {
                    StackTrace stk = new StackTrace();
                    Logger.ReportError("AddInHost is null\n" + stk.ToString());
                }
                return Application.AddInHost.MediaCenterEnvironment;
            }
        }

        public static Microsoft.MediaCenter.MediaExperience MediaExperience
        {
            get
            {
                return Application.MediaCenterEnvironment.MediaExperience ?? GetMediaExperienceUsingReflection();
            }
        }


        public static void UIDeferredInvokeIfRequired(Action action)
        {
            if (Microsoft.MediaCenter.UI.Application.ApplicationThread != System.Threading.Thread.CurrentThread)
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_=>action());
            else
                action();
        }

        private static FieldInfo _CheckedMediaExperienceFIeldInfo;

        public static Microsoft.MediaCenter.MediaExperience GetMediaExperienceUsingReflection()
        {
            Logger.ReportVerbose("Having to get media experience by reflection due to bug in Windows 7");
            MediaCenterEnvironment env = Application.MediaCenterEnvironment;

            var mce = env.MediaExperience;

            // great window 7 has bugs, lets see if we can work around them 
            // http://mediacentersandbox.com/forums/thread/9287.aspx
            if (mce == null)
            {
                mce = env.MediaExperience;

                if (mce == null)
                {
                    try
                    {
                        if (_CheckedMediaExperienceFIeldInfo == null)
                        {
                            _CheckedMediaExperienceFIeldInfo = env.GetType().GetField("_checkedMediaExperience", BindingFlags.NonPublic | BindingFlags.Instance);
                        }

                        if (_CheckedMediaExperienceFIeldInfo != null)
                        {
                            _CheckedMediaExperienceFIeldInfo.SetValue(env, false);
                            mce = env.MediaExperience;
                        }

                    }
                    catch (Exception e)
                    {
                        // give up ... I do not know what to do 
                        Logger.ReportException("AddInHost.Current.MediaCenterEnvironment.MediaExperience is null", e);
                    }

                }

                if (mce == null)
                {
                    Logger.ReportVerbose("GetMediaExperienceUsingReflection was unsuccessful");
                }
                else
                {
                    Logger.ReportVerbose("GetMediaExperienceUsingReflection was successful");
                }

            }

            return mce;
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

        public bool CanSeek
        {
            get { return currentPlaybackController != null && currentPlaybackController.CanSeek; }
        }

        public bool CanPause
        {
            get { return currentPlaybackController != null && currentPlaybackController.CanPause; }
        }

        /// <summary>
        /// MCML helper
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool IsNumeric(string value)
        {
            int num;
            return int.TryParse(value, out num);
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
            
            Application.ApplicationContext.CloseApplication();
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
            Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == Thread.CurrentThread);
            //back up and close the app if that fails
            if (!session.BackPage())
                Close();
        }

        public void Back()
        {
            Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == Thread.CurrentThread);
            if (LoggedIn && Config.UseExitMenu && session.AtRoot)
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
            Application.MediaCenterEnvironment.Dialog(CurrentInstance.StringData("InitialConfigDial"), CurrentInstance.StringData("Restartstr"), DialogButtons.Ok, 60, true);
            Application.ApplicationContext.CloseApplication();

        }

        public void DeleteMediaItem(Item Item)
        {
            // Need to put delete on a thread because the play process is asynchronous and
            // we don't want to tie up the ui when we call sleep
            Async.Queue(Async.ThreadPoolName.DeleteMediaItem, () =>
            {
                if (!Kernel.CurrentUser.Dto.Policy.EnableContentDeletion)
                {
                    MessageBox("User not allowed to delete content.");
                    return;
                }

                // Setup variables
                MediaCenterEnvironment mce = Application.MediaCenterEnvironment;
                var msg = CurrentInstance.StringData("DeleteMediaDial");
                var caption = CurrentInstance.StringData("DeleteMediaCapDial");

                // Present dialog
                var dr = mce.Dialog(msg, caption, DialogButtons.No | DialogButtons.Yes, 0, true);

                if (dr == DialogResult.No)
                {
                    mce.Dialog(CurrentInstance.StringData("NotDeletedDial"), CurrentInstance.StringData("NotDeletedCapDial"), DialogButtons.Ok, 0, true);
                    return;
                }

                if (dr == DialogResult.Yes)
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
            Async.Queue(Async.ThreadPoolName.PostDeleteValidate, () =>
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
            //let's put some useful info in here for diagnostics
            if (!Config.AutoValidate)
                Logger.ReportWarning("*** AutoValidate is OFF.");
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

                _popoutMessageTimer.Tick += _popoutMessageTimerTick;

                MediaCenterEnvironment.AudioMixer.PropertyChanged += AudioMixerPropertyChanged;
                VolumePct = (int)(MediaCenterEnvironment.AudioMixer.Volume / 655.35);

                Login();
            }
            catch (Exception e)
            {
                Application.MediaCenterEnvironment.Dialog(CurrentInstance.StringData("CriticalErrorDial") + e + " " + e.StackTrace, CurrentInstance.StringData("CriticalErrorCapDial"), DialogButtons.Ok, 60, true);
                Application.ApplicationContext.CloseApplication();
            }
        }

        void AudioMixerPropertyChanged(IPropertyObject sender, string property)
        {
            if (property.Equals("volume", StringComparison.OrdinalIgnoreCase))
            {
                VolumePct = (int)(MediaCenterEnvironment.AudioMixer.Volume / 655.35);
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
                    Kernel.Instance.CommonConfigData.AutoLogonPw = Kernel.Instance.CommonConfigData.SavePassword ? Kernel.CurrentUser.PwHash : null;
                    Kernel.Instance.CommonConfigData.Save();
                    UIFirePropertyChange("AutoLogin");
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
            Async.Queue(Async.ThreadPoolName.Logout, () =>
                                      {
                                          if (YesNoBox(string.Format("Logout of user profile {0}?", Kernel.CurrentUser.Name)) == "Y")
                                          {
                                              if (AvailableUsers.Count > 1) Config.StartupParms = "ShowLogin";
                                              Restart();
                                          }
                                      });
            

        }

        public void RestartWithConnect()
        {
            Config.StartupParms = "ConnectLogin";
            Restart();
        }

        public void ClearConnectInfo()
        {
            Kernel.Instance.CommonConfigData.ConnectUserId = null;
            Kernel.Instance.CommonConfigData.ConnectUserToken = null;
            Kernel.Instance.CommonConfigData.Save();
        }

        public bool ConnectConfigured { get { return !String.IsNullOrEmpty(Kernel.Instance.CommonConfigData.ConnectUserToken); }}

        public bool IsRemoteConnection { get { return Kernel.ApiClient.IsRemoteConnection; }}

        bool ConnectAutomatically(int timeout)
        {
            var info = new ServerLocator().FindServer();
            if (info != null)
            {
                var fullAddress = info.UserLinkType != null ? info.RemoteAddress : info.LocalAddress;
                fullAddress = fullAddress.Substring(fullAddress.IndexOf("//", StringComparison.Ordinal) + 2);
                var parts = fullAddress.Split(':');
                var address = parts[0];
                var port = parts.Length > 1 ? parts[1] : "8096";
                int intPort;
                Int32.TryParse(port, out intPort);
                return Kernel.ConnectToServer(address, intPort > 0 ? intPort : 8096, timeout);
            }

            return false;
        }

        public void ConnectToServer(string address, string port)
        {
            ConnectToServer(address, port, null);
        }

        public void ConnectToServer(string address, string port, ServerInfo info)
        {
            //Manually connect
            int intPort;
            Int32.TryParse(port, out intPort);
            if (!Kernel.ConnectToServer(address, intPort, 10000, info))
            {
                DisplayDialog(String.Format("Unable to connect to server at {0}:{1}", address, port), "Connect Failed");
            }
            else
            {
                Logger.ReportInfo("Connected to server {0} at {1}:{2}", Kernel.ServerInfo.ServerName, address, port);
                Logger.ReportInfo("Server version: " + Kernel.ServerInfo.Version);
                Login();
            }
        }

        public void ConnectToServer(Item serverItem)
        {
            var server = serverItem.BaseItem as Server;
            if (server != null)
            {
                var fullAddress = server.Info.UserLinkType != null ? server.Info.RemoteAddress : server.Info.LocalAddress;
                fullAddress = fullAddress.Substring(fullAddress.IndexOf("//", StringComparison.Ordinal) + 2);
                var parts = fullAddress.Split(':');
                var address = parts[0];
                var port = parts.Length > 1 ? parts[1] : "8096";
                ConnectToServer(address, port, server.Info);
            }
            else
            {
                Logger.ReportError("Attempt to connect to invalid server item");
                throw new ApplicationException("Attempt to connect to invalid server item");
            }
        }

        public bool ConnectToServer(CommonConfigData config)
        {
            var connected = false;

            if (config.FindServerAutomatically)
            {
                connected = ConnectAutomatically(config.HttpTimeout);
            }
            else
            {
                if (config.ShowServerSelection) return false;

                //server specified
                var retries = 0;
                while (!connected && retries < 3)
                {
                    connected = Kernel.ConnectToServer(config.ServerAddress, config.ServerPort, config.HttpTimeout);
                    if (!connected) {
                        Logger.ReportInfo("Unable to connect to server at {0}. Will retry...", config.ServerAddress);
                        retries++;
                        Thread.Sleep(1500); //give it some time to wake up
                    }
                }

                if (!connected)
                {
                    Logger.ReportWarning("Unable to connect to configured server {0}:{1}. Will try automatic detection", config.ServerAddress, config.ServerPort);
                    connected = ConnectAutomatically(config.HttpTimeout);
                }
            }

            if (connected)
            {
                config.ServerPort = Kernel.ApiClient.ServerApiPort;
                config.Save();

            }

            return connected;
        }

        public string NewPin
        {
            get
            {
                var result = Kernel.ConnectApiClient.CreatePin(new DeviceId().Value);
                if (result != null)
                {
                    return result.Pin;
                }

                Logger.ReportError("Could not create Pin");
                return "";
            }
        }

        /// <summary>
        /// Log in to default or show a login screen with choices
        /// </summary>
        public void Login()
        {
            var parms = Config.StartupParms ?? "";
            // reset these
            Config.StartupParms = null;

            if (!Kernel.ServerConnected)
            {
                if (parms == "ConnectLogin")
                {
                    ConnectLogin();
                    return;
                }

                if (ConnectToServer(Kernel.Instance.CommonConfigData))
                {
                    Logger.ReportInfo("Connected to server {0} at {1}", Kernel.ServerInfo.ServerName, Kernel.ServerInfo.LocalAddress);
                    Logger.ReportInfo("Server version: " + Kernel.ServerInfo.Version);
                }
                else
                {
                    //Unable to connect through discovery or configuration
                    ConnectLogin();
                    return;
                }

            }

            // see if we are a connect server and direct authenticate if so
            if (Kernel.CurrentServer.UserLinkType != null)
            {
                var userDto = Kernel.ApiClient.AuthenticateConnectUser(Kernel.Instance.CommonConfigData.ConnectUserId, Kernel.CurrentServer.ExchangeToken);
                if (userDto != null)
                {
                    LoginUser(ItemFactory.Instance.Create(new User {Name = userDto.Name, Dto = userDto, Id = new Guid(userDto.Id ?? ""), ParentalAllowed = userDto.HasPassword}), false);
                    UsingDirectEntry = true;
                    OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/SplashPage", new Dictionary<string, object> { { "Application", this } });
                    return;
                }
                else
                {
                    Logger.ReportError("Error logging into server {0}/{1} with connect", Kernel.CurrentServer.Name, Kernel.CurrentServer.RemoteAddress);
                    Async.Queue(Async.ThreadPoolName.ConnectError, () => MessageBox("Could not connect to server.  Please try again later."));
                }
                return;
            }

            var user = !string.IsNullOrEmpty(parms) ? parms.Equals("ShowLogin", StringComparison.OrdinalIgnoreCase) ? null : AvailableUsers.FirstOrDefault(u => u.Name.Equals(parms, StringComparison.OrdinalIgnoreCase)) : 
                        Kernel.Instance.CommonConfigData.LogonAutomatically ? AvailableUsers.FirstOrDefault(u => u.Name.Equals(Kernel.Instance.CommonConfigData.AutoLogonUserName, StringComparison.OrdinalIgnoreCase)) 
                        : null;


            if (user == null && Kernel.Instance.CommonConfigData.LogonAutomatically && !parms.Equals("ShowLogin", StringComparison.OrdinalIgnoreCase))
            {
                //Must be a hidden user configured
                if (LoginUser(Kernel.Instance.CommonConfigData.AutoLogonUserName, Kernel.Instance.CommonConfigData.AutoLogonPw, true))
                {
                    // we're in - if this fails, we'll fall through to the login screen
                    UsingDirectEntry = true;
                    OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/SplashPage", new Dictionary<string, object> {{"Application",this}});
                    return;
                }
            }

            if (user != null)
            {
                // only one user or specified - log in automatically
                UsingDirectEntry = true;
                OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/SplashPage", new Dictionary<string, object> {{"Application", this}});
                LoginUser(user);
            }
            else
            {
                // show login screen
                OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/MetroLoginPage", new Dictionary<string, object> { { "Application", this } });
            }
        }

        public void ConnectLogin()
        {
            if (!String.IsNullOrEmpty(Kernel.Instance.CommonConfigData.ConnectUserToken))
            {
                //display server list from connect
                OpenServerSelectionPage();
            }
            else
            {
                //login with connect screen
                OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/ConnectLogin", new Dictionary<string, object> { { "Application", this } });
            }

        }

        public void PinLogin(string pin)
        {
            //Try to exchange the pin for a connect user id and token
            try
            {
                var result = Kernel.ConnectApiClient.ExchangePin(pin, new DeviceId().Value);
                Kernel.Instance.CommonConfigData.ConnectUserId = result.UserId;
                Kernel.Instance.CommonConfigData.ConnectUserToken = result.AccessToken;
                Kernel.Instance.CommonConfigData.Save();

                //now load available servers and display selection screen
                OpenServerSelectionPage();
            }
            catch (Exception e)
            {
                Logger.ReportException("Error trying to exchange pin {0}/{1}", e, pin, new DeviceId().Value);
                DisplayDialog("Unable to verify PIN.  Please ensure you properly confirmed it on the web site.", "PIN not confirmed");
            }
        }

        public void OpenServerSelectionPage()
        {
            Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == Thread.CurrentThread);
            //Get all available servers

            //First the local one
            var info = new ServerLocator().FindServer();
            if (info != null)
            {
                Kernel.AddServer(info);
            }

            //Then connect ones
            if (Kernel.Instance.CommonConfigData.ConnectUserToken != null)
            {
                Kernel.ConnectApiClient.SetUserToken(Kernel.Instance.CommonConfigData.ConnectUserToken);
                var servers = Kernel.ConnectApiClient.GetAvailableServers(Kernel.Instance.CommonConfigData.ConnectUserId);
                if (servers != null)
                {
                    foreach (var availableServer in servers)
                    {
                        Kernel.AddServer(availableServer);
                    }
                }
            }

            OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/ServerSelection", new Dictionary<string, object> { { "Application", this } });
            
        }

        public void LoginUser(string name, string pw)
        {
            LoginUser(name, pw, false);
        }

        public bool LoginUser(string name, string pw, bool isPwHashed)
        {
            try
            {
                var pwHash = isPwHashed ? pw : BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(pw ?? CustomPINEntry ?? "")));
                var result = Kernel.ApiClient.AuthenticateUserByName(name, pwHash);
                LoginUser(ItemFactory.Instance.Create(new User { Name = result.User.Name, Dto = result.User, Id = new Guid(result.User.Id ?? ""), PwHash = pwHash, ParentalAllowed = !result.User.HasPassword }), false);
                return true;
            }
            catch (HttpException e)
            {
                if (((WebException)e.InnerException).Status == WebExceptionStatus.ProtocolError)
                {
                    if (!UsingDirectEntry)
                    {
                        Application.MediaCenterEnvironment.Dialog("Incorrect User or Password.", "Access Denied", DialogButtons.Ok, 100, true);
                    }
                    UsingDirectEntry = false;
                    ShowSplash = false;
                    return false;
                }
                throw;
            }
        }

        public void LoginUser(Item user, bool authenticate = true)
        {
            Kernel.CurrentUser = user.BaseItem as User;
            CurrentUser = user;
            var ignore = CurrentUser.PrimaryImage; // force this to load
            UIFirePropertyChange("CurrentUser");
            if (authenticate && Kernel.CurrentUser != null && Kernel.CurrentUser.HasPassword)
            {
                // Try with saved pw
                if (!Kernel.Instance.CommonConfigData.LogonAutomatically || Kernel.CurrentUser.Name != user.Name || !LoadUser(Kernel.CurrentUser, Kernel.Instance.CommonConfigData.AutoLogonPw))
                {
                    // show pw screen
                    OpenSecurityPage("Please Enter Password for " + CurrentUser.Name + " (select or enter when done)");
                }
            }
            else
            {
                // just log in as we don't have a pw or we've already authenticated manually
                Async.Queue(Async.ThreadPoolName.LoadUser, () => LoadUser(user.BaseItem as User, "", authenticate));
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

        protected bool LoadUser(User user, string pw, bool authenticate = true)
        {
            ShowSplash = true;
            Kernel.ApiClient.CurrentUserId = user.Id;

            if (authenticate)
            {
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
                catch (HttpException e)
                {
                    if (((WebException)e.InnerException).Status == WebExceptionStatus.ProtocolError)
                    {
                        if (!UsingDirectEntry)
                        {
                            Application.MediaCenterEnvironment.Dialog("Incorrect Password.", "Access Denied", DialogButtons.Ok, 100, true);
                        }
                        UsingDirectEntry = false;
                        ShowSplash = false;
                        return false;
                    }
                    throw;
                }
            }

            LoggedIn = true;

            // load server config
            try
            {
                Kernel.ServerConfig = Kernel.ApiClient.GetServerConfiguration();
            }
            catch (Exception e)
            {
                Logger.ReportException("Error getting server configuration", e);
            }

            // re-load server info now that we have authorization for the whole thing
            Kernel.ServerInfo = Kernel.ApiClient.GetSystemInfo();
            Kernel.Instance.CommonConfigData.LastServerMacAddress = Kernel.ServerInfo.MacAddress;
            Kernel.Instance.CommonConfigData.Save();

            // load user config
            Kernel.Instance.LoadUserConfig();
            // and server-based user prefs
            ServerUserConfig = new UserConfig(Kernel.CurrentUser.Dto.Configuration);

            // init activity timer
            ActivityTimerInterval = Config.InputActivityTimeout * 1000;
            _inputActivityTimer.Elapsed += _activityTimerElapsed;

            // setup styles and fonts with user options
            try
            {
                CustomResourceManager.SetupStylesMcml(null, Config.Instance);
                CustomResourceManager.SetupFontsMcml(null, Config.Instance);
            }
            catch (Exception ex)
            {
                Application.MediaCenterEnvironment.Dialog(ex.Message, "Error", DialogButtons.Ok, 100, true);
                Application.ApplicationContext.CloseApplication();
                return false;
            }

            // load root
            Kernel.Instance.ReLoadRoot();

            // get server plug-ins
            var plugins = Kernel.ApiClient.GetServerPlugins();
            Kernel.ServerPlugins = plugins != null ? plugins.ToList() : new List<PluginInfo>();

            LoadPluginsAndModels();

            // build switch user menu
            BuildUserMenu();

            if (Kernel.Instance.RootFolder == null)
            {
                Async.Queue(Async.ThreadPoolName.LaunchError, () =>
                                                {
                                                    MessageBox("Unable to retrieve root folder.  Application will exit.");
                                                    Close();
                                                });
            }
            else
            {
                Logger.ReportInfo("*** Theme in use is: " + Config.ViewTheme);
                //Launch into our entrypoint
                Application.UIDeferredInvokeIfRequired(() => LaunchEntryPoint(EntryPointResolver.EntryPointPath));
            }

            //load plug-in catalog info
            if (user.Dto.Policy.IsAdministrator)
            {
                Async.Queue(Async.ThreadPoolName.PackageLoad,() =>
                {
                    LoadPackages();
                    RefreshPluginCollections();
                });
                
            }

            //supporter nag
            if (Kernel.Instance.CommonConfigData.LastNagDate == DateTime.MinValue || 
                Kernel.Instance.CommonConfigData.LastNagDate > DateTime.Now || 
                DateTime.Now > Kernel.Instance.CommonConfigData.LastNagDate.AddDays(2))
            {
                Async.Queue(Async.ThreadPoolName.SupporterCheck, () =>
                                                   {
                                                       var supporter = MBRegistration.GetRegistrationStatus("mbsupporter", Kernel.Instance.Version);
                                                       while (!supporter.RegChecked) { Thread.Sleep(500);}

                                                       if (!supporter.IsRegistered)
                                                       {
                                                           PopoutMessage("Please Support Media Browser", "Please become a Media Browser Supporter.  Go to your server dashboard Help/Become a Supporter.  Thanks!");
                                                           Kernel.Instance.CommonConfigData.LastNagDate = DateTime.Now;
                                                           Kernel.Instance.CommonConfigData.Save();
                                                       }
                                                   },10000);
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
            Async.Queue(Async.ThreadPoolName.PluginUpdate, () => Updater.UpdateAllPlugins(InstalledPluginsCollection, silent));
        }

        public void UpdatePlugin(PluginItem plugin)
        {
            Async.Queue(Async.ThreadPoolName.PluginUpdate, () => Updater.UpdatePlugin(plugin));
        }

        public void InstallPlugin(PluginItem plugin)
        {
            Async.Queue(Async.ThreadPoolName.PluginUpdate, () => Updater.UpdatePlugin(plugin, "Installing"));
        }

        public void RemovePlugin(PluginItem plugin)
        {
            Async.Queue(Async.ThreadPoolName.PluginRemove, () =>
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
                        Async.Queue(Async.ThreadPoolName.Msg, () => MessageBox("Could not delete plugin " + plugin.Name));
                    }
                }
            });
        }

        public void RatePlugin(PluginItem plugin, int rating, bool recommend)
        {
            Async.Queue(Async.ThreadPoolName.PackageRating, () => Kernel.ApiClient.RatePackage(plugin.Id, rating, recommend));
            Information.AddInformationString("Thank you for submitting your rating");
        }

        /// <summary>
        /// Open the dash in the default browser to the proper plugin page for registration
        /// </summary>
        /// <param name="plugin"></param>
        public void RegisterPlugin(PluginItem plugin)
        {
            Async.Queue(Async.ThreadPoolName.Registration, () =>
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
            Application.UIDeferredInvokeIfRequired(() =>
            {
                foreach (var otherUser in AvailableUsers.Where(u => u.Name != CurrentUser.Name))
                {
                    Kernel.Instance.AddMenuItem(new MenuItem(otherUser.Name, otherUser.BaseItem.PrimaryImagePath ?? "resx://MediaBrowser/MediaBrowser.Resources/UserLoginDefault", SwitchUser, new List<MenuType> { MenuType.User }));
                }
            }
            );
        }

        protected void LoadPluginsAndModels()
        {
            Application.UIDeferredInvokeIfRequired(() =>
            {
                // add plug-ins config panel if admin
                if (Kernel.CurrentUser.Dto.Policy.IsAdministrator)
                    Kernel.Instance.AddConfigPanel(LocalizedStrings.Instance.GetString("PluginsConfig"), "resx://MediaBrowser/MediaBrowser.Resources/AdvancedConfigPanel#PluginsPanel");

                // add view config panel if legacy views not selected
                if (!Config.UseLegacyFolders) Kernel.Instance.AddConfigPanel(LocalizedStrings.Instance.GetString("ViewConfigurationConfig"), "resx://MediaBrowser/MediaBrowser.Resources/ViewConfigPanel#ViewConfigPanel");

                // add legacy config panel
                Kernel.Instance.AddConfigPanel(LocalizedStrings.Instance.GetString("LegacyConfigurationConfig"), "resx://MediaBrowser/MediaBrowser.Resources/LegacyConfigPanel#LegacyConfigPanel");

                // load plugins
                Kernel.Instance.LoadPlugins();

                // add advanced config panel to end
                if (Kernel.CurrentUser.Dto.Policy.IsAdministrator)
                    Kernel.Instance.AddConfigPanel(LocalizedStrings.Instance.GetString("AdvancedConfig"), "resx://MediaBrowser/MediaBrowser.Resources/AdvancedConfigPanel#AdvancedPanel");

                //populate the config model choice
                ConfigModel = new Choice { Options = ConfigPanelNames };

            }
            );
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
            Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == Thread.CurrentThread);
            
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
                    WebSocket.Connect(Kernel.ApiClient.ServerHostName, Kernel.ApiClient.ServerApiPort, Kernel.ApiClient.ClientType, Kernel.ApiClient.DeviceId);
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
                        Async.Queue(Async.ThreadPoolName.StartupQueue, () =>
                                                             {
                                                                 while (!PackagesRetrieved) {Thread.Sleep(500);}
                                                                 RefreshPluginCollections();
                                                                 while (InstalledPluginsCollection == null) {Thread.Sleep(500);}
                                                                 UpdateAllPlugins(true);
                                                             });
                    }

                    // We check config here instead of in the Updater class because the Config class 
                    // CANNOT be instantiated outside of the application thread.
                    Async.Queue(Async.ThreadPoolName.StartupQueue, () => CheckForSystemUpdate(Config.EnableUpdates && !RunningOnExtender), 10000);

                    if (Kernel.CurrentUser.Dto.Policy.IsAdministrator) // don't show these prompts to non-admins
                    {
                        // Let the user know if the server needs to be restarted
                        // Put it on the same thread as the update checks so it will be behind them
                        Async.Queue(Async.ThreadPoolName.StartupQueue, () =>
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

                    if (!Config.NewViewsIntroShown)
                    {
                        //Make this come up after the home screen
                        Async.Queue(Async.ThreadPoolName.NewViews, () => OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/NewViewsIntro", new Dictionary<string, object> {{"Application", CurrentInstance}}), 3000);
                    }

                    Navigate(this.RootFolderModel);
                }
                catch (Exception ex)
                {
                    Application.MediaCenterEnvironment.Dialog(CurrentInstance.StringData("EntryPointErrorDial") + this.EntryPointPath + ". " + ex.ToString() + " " + ex.StackTrace.ToString(), CurrentInstance.StringData("EntryPointErrorCapDial"), DialogButtons.Ok, 30, true);
                    Close();
                }
            }
        }

        public void CheckForSystemUpdate(bool prompt)
        {
            if (!systemUpdateCheckInProgress)
            {
                Async.Queue(Async.ThreadPoolName.UpdateCheck, () =>
                                                {
                                                    if (prompt && RunningOnExtender)
                                                    {
                                                        Information.AddInformationString("Cannot update from an extender.");
                                                        return;
                                                    }
                                                    SystemUpdateCheckInProgress = true;
                                                    UpdateAvailable = Updater.CheckForUpdate(prompt);
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
                return true;  //new install, don't need to migrate
            }

            
            //Clear old ImageCache
            Logger.ReportInfo("=========== Clearing old image cache...");
            try
            {
                foreach (var file in new DirectoryInfo(ApplicationPaths.AppImagePath).GetFiles())
                {
                    file.Delete();
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error clearing Image path {0}", e, ApplicationPaths.AppImagePath);
            }


            try
            {
                var oldPlugin = Path.Combine(ApplicationPaths.AppPluginPath, "ThemeVideoBackdrops.dll");
                if (File.Exists(oldPlugin))
                {
                    Logger.ReportInfo("Removing old theme video backdrop plug-in");
                    File.Delete(oldPlugin);
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error trying to remove old plugins", e);
            }
            return true;
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
            Async.Queue(Async.ThreadPoolName.ManualFullRefresh, () =>
                                                                               {
                                                                                   MessageBox(CurrentInstance.StringData("ManualRefreshDial"));
                                                                                   Kernel.ApiClient.StartLibraryScan();
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
                UIFirePropertyChange("DisplayPopupPlay");
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
                UIFirePropertyChange("DisplayUserMenu");
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
                UIFirePropertyChange("DisplayMultiMenu");
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
                UIFirePropertyChange("DisplayExitMenu");
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
                    UIFirePropertyChange("ShowSearchPanel");
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
                    UIFirePropertyChange("ShowPluginDetailPage");
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
                    
                    UIFirePropertyChange("ShowNowPlaying");
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

                }
                catch (Exception e)
                {
                    // never crash here
                    Logger.ReportException("Something strange happend while getting media name, please report to community.mediabrowser.tv", e);                    

                }
                return "Unknown";
            }
        }

        private readonly System.Timers.Timer _inputActivityTimer = new System.Timers.Timer {AutoReset = false, Enabled = false, Interval = 8000};
        public double ActivityTimerInterval 
        {
            get { return _inputActivityTimer.Interval; }
            set { _inputActivityTimer.Interval = value; }
        }

        private void _activityTimerElapsed(object sender, System.Timers.ElapsedEventArgs args)
        {
            RecentUserInput = false;
        }

        public bool RecentUserInput
        {
            get { return _recentUserInput; }
            set
            {
                if (value)
                {
                    //restart the timer if we had input
                    _inputActivityTimer.Stop();
                    _inputActivityTimer.Start();
                    //and be sure screensaver not going
                    ScreenSaverActive = false;
                }
                else
                {
                    //stop timer if we are turning off manually
                    _inputActivityTimer.Stop();
                }
                if (_recentUserInput != value)
                {
                    _recentUserInput = value;
                    UIFirePropertyChange("RecentUserInput");
                }
            }
        }

        public bool SuppressInitialOverlay { get; set; }

        /// <summary>
        /// Legacy support - does not bind
        /// </summary>
        public bool IsMouseActive { get; set; }

        void mouseActiveHooker_MouseActive(IsMouseActiveHooker m, MouseActiveEventArgs e)
        {
            RecentUserInput = e.MouseActive;
            //Logger.ReportVerbose("************* Mouse Active {0}", e.MouseActive);
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
            
            DialogResult r = Application.MediaCenterEnvironment.Dialog(CurrentInstance.StringData("ClearCacheDial"), CurrentInstance.StringData("ClearCacheCapDial"), DialogButtons.Yes | DialogButtons.No, 60, true);
            if (r == DialogResult.Yes)
            {
                bool ok = Kernel.Instance.MB3ApiRepository.ClearEntireCache();
                if (!ok)
                {
                    Application.MediaCenterEnvironment.Dialog(string.Format(CurrentInstance.StringData("ClearCacheErrorDial"), ApplicationPaths.AppCachePath), CurrentInstance.StringData("Errorstr"), DialogButtons.Ok, 60, true);
                }
                else
                {
                    Application.MediaCenterEnvironment.Dialog(CurrentInstance.StringData("RestartMBDial"), CurrentInstance.StringData("CacheClearedDial"), DialogButtons.Ok, 60, true);
                }
                Application.ApplicationContext.CloseApplication();
            }
        }

        public void OpenCatalogPage()
        {
            Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == Thread.CurrentThread);
            var properties = new Dictionary<string, object>();
            properties["Application"] = this;

            if (session != null)
            {
                OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/PluginCatalog#PluginCatalog", properties);
            }
            else
            {
                Logger.ReportError("Session is null in OpenPage");
            }
            
        }

        public void OpenConfiguration(bool showFullOptions)
        {
            Application.UIDeferredInvokeIfRequired(() =>
            {
                var properties = new Dictionary<string, object>();
                properties["Application"] = this;
                properties["ShowFull"] = showFullOptions;

                if (session != null)
                {
                    OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/ConfigPage", properties);
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
            );
        }


        // accessed from Item
        internal void OpenExternalPlaybackPage(Item item)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties["Application"] = this;
            properties["Item"] = item;

            if (session != null)
            {
                OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/ExternalPlayback", properties);
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
                    UIFirePropertyChange("CurrentFolderModel");
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
                    UIFirePropertyChange("CurrentPluginItem");
                }
            }
        }

        public void OpenFolderPage(FolderModel folder)
        {
            Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == Thread.CurrentThread);
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
                OpenMCMLPage(folder.Folder.CustomUI ?? CurrentTheme.FolderPage, properties);
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
            Async.Queue(Async.ThreadPoolName.PersonNavigation, () =>
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

                                                    Application.UIDeferredInvokeIfRequired(() => Navigate(ItemFactory.Instance.Create(index)));
                                                     
                                                 });
            
        }


        public void NavigateToGenre(string genre, Item currentMovie)
        {
            this.NavigateToGenre(genre, currentMovie, "Movie");
        }

        public void NavigateToGenre(string genre, Item currentMovie, string itemType)
        {
            switch (currentMovie.BaseItem.GetType().Name)
            {
                case "Series":
                case "Season":
                case "Episode":
                    itemType = "Series";
                    break;

                case "MusicAlbum":
                case "MusicArtist":
                case "MusicGenre":
                case "Song":
                    itemType = "MusicAlbum";
                    break;

                case "Game":
                    itemType = "Game";
                    break;
            }

            Async.Queue(Async.ThreadPoolName.GenreNavigation, () =>
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

                                                    Navigate(ItemFactory.Instance.Create(index));
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

            Async.Queue(Async.ThreadPoolName.SimilarNavigation, () =>
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
                                                        Navigate(ItemFactory.Instance.Create(index));
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

        public ThemeBackdropController BackdropController = new ThemeBackdropController();

        public void Navigate(Item item)
        {
            Application.UIDeferredInvokeIfRequired(() =>
                {
                    Logger.ReportVerbose("Navigating to {0} item type {1}", item.Name, item.GetType());
                    currentContextMenu = null; //any sort of navigation should reset our context menu so it will properly re-evaluate on next ref

                    if (item.BaseItem is Person)
                    {
                        NavigateToActor(item);
                        return;
                    }

                    if (Config.EnableThemeBackgrounds && (currentPlaybackController == null || !currentPlaybackController.IsPlaying))
                    {
                        BackdropController.Play(item.BaseItem);
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
                            OpenMCMLPage(item.BaseItem.CustomUI ?? CurrentTheme.DetailPage, properties);

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
                            CurrentFolder = folder;
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
            );
        }

        public void OpenSecurityPage(object prompt)
        {
            var properties = new Dictionary<string, object>();
            properties["Application"] = this;
            properties["PromptString"] = prompt;
            this.RequestingPIN = true; //tell page we are calling it (not a back action)
            OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/ParentalPINEntry", properties);
        }

        public void OpenCustomPlayerUi()
        {
            ShowNowPlaying = true; // be sure this is set when we enter so we don't just back right out
            var properties = new Dictionary<string, object>();
            properties["Application"] = this;
                
            OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/CustomPlayer", properties);
        }

        public void OpenMCMLPage(string page, Dictionary<string, object> properties)
        {
            Application.UIDeferredInvokeIfRequired(() =>
                {
                    currentContextMenu = null; //good chance this has happened as a result of a menu item selection so be sure this is reset
                    Logger.ReportVerbose("GoToPage: {0}", page);
                    session.GoToPage(page, properties);
                }
            );
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
            Play(chosen.ItemToPlay, false, false, chosen.PlayIntros, false, chosen.Resume ? chosen.ItemToPlay.PlayState.PositionTicks : 0, true);
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

        private object _playLock = new object();

        public void Play(Item item, bool resume, bool queue, bool? playIntros, bool shuffle, long startPos, bool forceFirstPart = false)
        {
            lock(_playLock)
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
                            PlayMultiItem(new MultiPartPlayOption { Name = LocalizedStrings.Instance.GetString("PlayDetail") + " " + LocalizedStrings.Instance.GetString("AllParts"), ItemToPlay = allparts, PlayIntros = playIntros});
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
                            UIFirePropertyChange("MultiPartOptions");
                            DisplayMultiMenu = true;
                            return;
                        }
                    }
                }

                if (Config.WarnOnStream && !item.IsFolder && !item.IsRemoteContent && !item.IsChannelItem && !Directory.Exists(Path.GetDirectoryName(item.Path) ?? ""))
                {
                    Logger.ReportWarning("Unable to directly access {0}.  Attempting to stream.", item.Path);

                    Async.Queue(Async.ThreadPoolName.AccessError, () => MessageBox("Could not access media. Will attempt to stream.  Use UNC paths or path substitution on server and ensure this machine can access them.", true, 10000));

                }

                PlayableItem introPlayable = null;

                if (!resume && !queue && playIntros != false)
                {
                    // Get intros for this item
                    // if we're playing multiples, look for intros for the first one
                    var container = item.BaseItem as Folder;
                    var baseId = container != null ? container.FirstChild.ApiId : item.BaseItem.ApiId; 
                    var introItems = Kernel.Instance.MB3ApiRepository.RetrieveIntros(baseId ?? Guid.Empty.ToString("N")).Cast<BaseItem>().ToList();
                    if (introItems.Any())
                    {
                        //convert our playable into a collection and play them together
                        Logger.ReportInfo("Playing {0} Intros before {1}", introItems.Count, item.Name);
                        introPlayable = PlayableItemFactory.Instance.Create(ItemFactory.Instance.Create(new IndexFolder(introItems)));
                    }
                    else
                    {
                        Logger.ReportInfo("No intros found for {0}", item.Name);
                    }
                }
                else
                {
                    Logger.ReportVerbose("Not playing intros for {0}", item.Name);
                }

                var playable = PlayableItemFactory.Instance.Create(item);

                // This could happen if both item.IsFolder and item.IsPlayable are false
                if (playable == null)
                {
                    return;
                }

                playable.Resume = resume;
                if (startPos > 0) playable.StartPositionTicks = startPos;
                playable.QueueItem = queue;
                playable.Shuffle = shuffle;

                Play(playable, introPlayable);
                
            }
        }

        /// <summary>
        /// Resumes an Item
        /// </summary>
        public void Resume(Item item)
        {
            Play(item, true, false, null, false);
        }

        private void IntroPlaybackFinished(object sender, PlaybackStateEventArgs e)
        {
            // unhook us
            currentPlaybackController.PlaybackFinished -= IntroPlaybackFinished;
            SuppressInitialOverlay = false;

            // and kick off main item
            if (MainPlayable != null) Play(MainPlayable);
        }

        private PlayableItem MainPlayable { get; set; }

        /// <summary>
        /// Play with intros - this is an overload in order not to break sig with existing mcml
        /// </summary>
        /// <param name="playable"></param>
        /// <param name="introPlayable"></param>
        public void Play(PlayableItem playable, PlayableItem introPlayable)
        {
            if (BackdropController.IsPlaying) PlaybackControllerHelper.Stop();

            if (introPlayable == null)
            {
                // Simulate optional param
                Play(playable);
            }
            else
            {
                CurrentlyPlayingItemId = playable.HasMediaItems ? playable.CurrentMedia.Id : CurrentItem.Id;

                Async.Queue(Async.ThreadPoolName.PlayIntros, () =>
                {
                    // save the main playable so we can play it when we're finished
                    MainPlayable = playable;
                    currentPlaybackController = introPlayable.PlaybackController;

                    // hook to finished event so we can kick off the main
                    introPlayable.PlaybackController.PlaybackFinished += IntroPlaybackFinished;

                    RecentUserInput = false;
                    SuppressInitialOverlay = true; // suppress the overlay for intros
                    introPlayable.Play();

                });
            }
        }

        public void Play(PlayableItem playable)
        {
            CurrentlyPlayingItemId = playable.HasMediaItems ? playable.CurrentMedia.Id : CurrentItem.Id;
            MainPlayable = null; // just make sure this doesn't hang around

            Async.Queue(Async.ThreadPoolName.PlayAction, () =>
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
                    Async.Queue(Async.ThreadPoolName.QuicklistUpdate, () => { foreach (var item in playableItem.PlayedMediaItems.Select(i => i.TopParent).Where(i => i != null).Distinct()) UpdateQuicklist(item); });
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
            Async.Queue(Async.ThreadPoolName.LoadUser, () => LoadUser(CurrentUser.BaseItem as User, BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(CustomPINEntry)))));
        }
        public void BackToRoot()
        {
            Application.UIDeferredInvokeIfRequired(() =>
            {
                //back up the app to the root page - used when library re-locks itself
                while (!session.AtRoot)
                {
                    session.BackPage();
                }
            }
            );
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
            Debug.Assert(Microsoft.MediaCenter.UI.Application.ApplicationThread == Thread.CurrentThread);
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
        private bool _recentUserInput;
        private bool _updateAvailable;

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
