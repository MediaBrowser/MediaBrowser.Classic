using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Globalization;
using Configurator.Code;
using MediaBrowser;
using MediaBrowser.Library;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Playables.ExternalPlayer;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library.Threading;
using MediaBrowser.LibraryManagement;

namespace Configurator
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public ConfigData config;
        Ratings ratings;
        PermissionDialog waitWin;
        PopupMsg PopUpMsg;
        public bool KernelModified = false;
        public static MainWindow Instance;
        private List<ConfigMember> configMembers;
        private ConfigMember currentConfigMember;

        public MainWindow()
        { 
            try 
            {        
                Initialize();
            } 
            catch (Exception ex) 
            {
                MessageBox.Show("Failed to start up, please post this on http://community.mediabrowser.tv \n\n" + ex, "Error",MessageBoxButton.OK);
                Logger.ReportException("Error Starting up",ex);
            }

        }

        private void Initialize() {
            Instance = this;
            Kernel.Init(KernelLoadDirective.ShadowPlugins);
            Logger.ReportVerbose("======= Kernel intialized. Building window...");
            InitializeComponent();
            pluginList.MouseDoubleClick += pluginList_DoubleClicked;
            PopUpMsg = new PopupMsg(alertText);
            config = Kernel.Instance.ConfigData;

            //Logger.ReportVerbose("======= Loading combo boxes...");
            LoadComboBoxes();
            lblVersion.Content = lblVersion2.Content = "Version " + Kernel.Instance.VersionStr;

            //Logger.ReportVerbose("======= Refreshing Podcasts...");
            //RefreshPodcasts();
            //Logger.ReportVerbose("======= Refreshing Ext Players...");
            RefreshPlayers();

            //Logger.ReportVerbose("======= Loading Config Settings...");
            LoadConfigurationSettings();
            //Logger.ReportVerbose("======= Config Settings Loaded.");

            for (char c = 'D'; c <= 'Z'; c++) {
                daemonToolsDrive.Items.Add(c.ToString());
            }

            try {
                daemonToolsDrive.SelectedValue = config.DaemonToolsDrive;
            } catch {
                // someone bodged up the config
            }

            //daemonToolsLocation.Content = config.DaemonToolsLocation; /// old
            daemonToolsLocation.Text = config.DaemonToolsLocation;


            //Logger.ReportVerbose("======= Refreshing Extender Formats...");
            RefreshExtenderFormats();
            //Logger.ReportVerbose("======= Refreshing Display Settings...");
            RefreshDisplaySettings();
            //Logger.ReportVerbose("======= Saving Config...");
            SaveConfig();

            //Logger.ReportVerbose("======= Initializing Plugin Manager...");
            PluginManager.Instance.Init();
            //Logger.ReportVerbose("======= Loading Plugin List...");
            var src = new CollectionViewSource();
            src.Source = PluginManager.Instance.InstalledPlugins;
            src.GroupDescriptions.Add(new PropertyGroupDescription("PluginClass"));

            pluginList.ItemsSource = src.View;

            //Logger.ReportVerbose("======= Kicking off plugin update check thread...");
            Async.Queue("Plugin Update Check", () =>
            {
                using (new MediaBrowser.Util.Profiler("Plugin update check"))
                {
                    while (!PluginManager.Instance.PluginsLoaded) { } //wait for plugins to load
                    if (PluginManager.Instance.UpgradesAvailable())
                        Dispatcher.Invoke(DispatcherPriority.Background, 
                            (System.Windows.Forms.MethodInvoker)(() => PopUpMsg.DisplayMessage("Some of your plug-ins have upgrades available.")));
                }
            });

            //Logger.ReportVerbose("======= Kicking off validations thread...");
            Async.Queue("Startup Validations", () =>
            {
                //RefreshEntryPoints(false);
                ValidateMBAppDataFolderPermissions();
            });
            //Logger.ReportVerbose("======= Initialize Finised.");
        }

        public void ValidateMBAppDataFolderPermissions()
        {
            const string windowsAccount = "Users"; 
            const FileSystemRights fileSystemRights = FileSystemRights.FullControl;
            var folder = new DirectoryInfo(ApplicationPaths.AppConfigPath);

            if(!folder.Exists)
            {
                MessageBox.Show(folder.FullName + " does not exist. Cannot validate permissions.");
                return;
            }
            

            if (!ValidateFolderPermissions(windowsAccount, fileSystemRights, folder))
            {
                // removed popup question - just going to confuse the user and we *have* to do this if its not right -ebr
                {
                    var args = new object[3] {folder, windowsAccount, fileSystemRights };
                    this.Dispatcher.Invoke(new SetAccessProcess(SetAccess),args);
                }
            }
        }

        public delegate void SetAccessProcess(DirectoryInfo folder, string account,FileSystemRights fsRights);
        public void SetAccess(DirectoryInfo folder, string account, FileSystemRights fsRights)
        {
            //hide our main window and throw up a quick dialog to tell user what is going on
            this.Visibility = Visibility.Hidden;
            waitWin = new PermissionDialog();
            waitWin.Show();
            Async.Queue("Set Directory Permissions", 
                () => SetDirectoryAccess(folder, account, fsRights, AccessControlType.Allow), 
                () => this.Dispatcher.Invoke(new DoneProcess(PermissionsDone)));
        }

        public delegate void DoneProcess();
        public void PermissionsDone()
        {
            //close window and make us visible
            waitWin.Close();
            this.Visibility = Visibility.Visible;
        }
    


        public bool ValidateFolderPermissions(String windowsAccount, FileSystemRights fileSystemRights, DirectoryInfo folder)
        { 
            try
            {                              
                var dSecurity = folder.GetAccessControl();

                foreach (FileSystemAccessRule rule in dSecurity.GetAccessRules(true, false, typeof(SecurityIdentifier)))
                {
                    var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null); 
                    if (sid.CompareTo(rule.IdentityReference as SecurityIdentifier) == 0)
                    {
                        if (fileSystemRights == rule.FileSystemRights)
                            return true; // Validation complete 
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                string msg = "Error validating permissions set on " + folder.FullName + " for the Account \"" + windowsAccount + "\"";
                Logger.ReportException(msg, ex);
                MessageBox.Show(msg);
                return false;
            }                       
        }

        public void SetDirectoryAccess(DirectoryInfo folder, String windowsAccount, FileSystemRights rights, AccessControlType controlType)
        {
            try
            {
                var dSecurity = folder.GetAccessControl();
                var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                dSecurity.AddAccessRule(new FileSystemAccessRule(sid, rights, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, controlType));                
                folder.SetAccessControl(dSecurity);
            }
            catch (Exception ex)
            {
                var msg = "Error applying permissions to " + folder.FullName + " for the Account \"" + windowsAccount + "\"";
                Logger.ReportException(msg, ex);
                MessageBox.Show(msg);
            }
        }


        private void InitExpertMode()
        {
            if (configMembers == null)
            {
                configMembers = new List<ConfigMember>();
                foreach (var member in typeof(ConfigData).GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (XmlSettings<ConfigData>.IsSetting(member) && !XmlSettings<ConfigData>.IsHidden(member))
                        configMembers.Add(new ConfigMember(member, config));
                }

                var src = new CollectionViewSource();
                src.Source = configMembers;
                src.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
                configMemberList.ItemsSource = src.View;
                configMemberList.SelectedIndex = -1;
            }
        }
            

        #region Config Loading / Saving        
        private void LoadConfigurationSettings()
        {
            enableTranscode360.IsChecked = config.EnableTranscode360;
            useAutoPlay.IsChecked = config.UseAutoPlayForIso;
            
            cbxOptionClock.IsChecked = config.ShowClock;            
            cbxOptionTransparent.IsChecked = config.ShowThemeBackground;
            cbxOptionIndexing.IsChecked = config.RememberIndexing;
            cbxOptionDimPoster.IsChecked = config.DimUnselectedPosters;
            cbxOptionHideFrame.IsChecked = config.HideFocusFrame;
            cbxOptionAutoEnter.IsChecked = config.AutoEnterSingleDirs;
            cbxScreenSaver.IsChecked = config.EnableScreenSaver;
            cbxOptionShowFavorites.IsChecked = config.ShowFavoritesCollection;
            tbxFavoriteName.Text = config.FavoriteFolderName;
            lblSSTimeout.Content = config.ScreenSaverTimeOut.ToString()+" Mins";
            //cbxSendStats.IsChecked = config.SendStats;

            cbxOptionUnwatchedCount.IsChecked      = config.ShowUnwatchedCount;
            cbxOptionUnwatchedOnFolder.IsChecked   = config.ShowWatchedTickOnFolders;
            cbxOptionUnwatchedOnVideo.IsChecked    = config.ShowWatchTickInPosterView;
            cbxOptionUnwatchedDetailView.IsChecked = config.EnableListViewTicks;
            cbxOptionDefaultToUnwatched.IsChecked  = config.DefaultToFirstUnwatched;
            cbxRootPage.IsChecked                  = config.EnableRootPage;
            if (config.MaximumAspectRatioDistortion == Constants.MAX_ASPECT_RATIO_STRETCH)
                cbxOptionAspectRatio.IsChecked = true;
            else
                cbxOptionAspectRatio.IsChecked = false;
            
            
            ddlOptionViewTheme.SelectedItem = config.ViewTheme;
            ddlOptionThemeColor.SelectedItem = config.Theme;
            ddlOptionThemeFont.SelectedItem = config.FontTheme;

            tbxWeatherID.Text = config.YahooWeatherFeed;
            if (config.YahooWeatherUnit.ToLower() == "f")
                ddlWeatherUnits.SelectedItem = "Fahrenheit";
            else
                ddlWeatherUnits.SelectedItem = "Celsius";

            lblRecentItemCollapse.Content = config.RecentItemCollapseThresh;

            ddlLoglevel.SelectedItem = config.MinLoggingSeverity;

            //logging
            cbxEnableLogging.IsChecked = config.EnableTraceLogging;

            //library validation
            cbxAutoValidate.IsChecked = config.AutoValidate;

        }

        private void SaveConfig()
        {
            config.Save();
        }

        private void RefreshThemes()
        {
            ddlOptionViewTheme.ItemsSource = Kernel.Instance.AvailableThemes.Keys;
            if (ddlOptionViewTheme.Items != null)
            {
                if (!ddlOptionViewTheme.Items.Contains(config.ViewTheme))
                {
                    //must have just deleted our theme plugin - set to default
                    config.ViewTheme = "Default";
                    SaveConfig();
                    ddlOptionViewTheme.SelectedItem = config.ViewTheme;
                }
            }
        }

        private IEnumerable<CultureInfo> AllCultures = CultureInfo.GetCultures(CultureTypes.AllCultures & ~CultureTypes.NeutralCultures).OrderBy(c => c.Name);
        private List<RegionInfo> AllRegions;
        private List<Language> AllLanguages;
        class Language
        {
            public string Name;
            public string LanguageCode;

            public override string ToString()
            {
                return Name;
            }
        }

        private List<string> folderSettings;
        private void LoadComboBoxes()
        {
            // Themes
            RefreshThemes();            
            // Colors
            ddlOptionThemeColor.Items.Add("Default");
            ddlOptionThemeColor.Items.Add("Black");
            ddlOptionThemeColor.Items.Add("Extender Default");
            ddlOptionThemeColor.Items.Add("Extender Black");
            // Fonts 
            ddlOptionThemeFont.Items.Add("Default");
            ddlOptionThemeFont.Items.Add("Small");
            // Weather Units
            ddlWeatherUnits.Items.Add("Celsius");
            ddlWeatherUnits.Items.Add("Fahrenheit");

            ddlLoglevel.ItemsSource = Enum.GetValues(typeof(LogSeverity));

        }

        #endregion

        private void RefreshExtenderFormats()
        {
            extenderFormats.Items.Clear();
            foreach (var format in config.ExtenderNativeTypes.Split(','))
            {
                extenderFormats.Items.Add(format);
            }
        }

        private void RefreshDisplaySettings()
        {
            extenderFormats.Items.Clear();
            foreach (var format in config.ExtenderNativeTypes.Split(','))
            {
                extenderFormats.Items.Add(format);
            }
        }

        private void RefreshPlayers()
        {
            lstExternalPlayers.Items.Clear();
            foreach (ConfigData.ExternalPlayer item in config.ExternalPlayers)
            {
                if (!String.IsNullOrEmpty(item.ExternalPlayerName))
                    lstExternalPlayers.Items.Add(item);
            }
        }

        private void RefreshEntryPoints()
        {
            this.RefreshEntryPoints(true);
        }

        private void RefreshEntryPoints(bool RefreshPlugins)
        {
            Async.Queue("Configurator ep refresh", () =>
            {
                using (new MediaBrowser.Util.Profiler("Entry Point Refresh"))
                {
                    EntryPointManager epm = null;

                    try
                    {
                        epm = new EntryPointManager();
                    }
                    catch (Exception ex)
                    {
                        //Write to error log, don't prompt user.
                        Logger.ReportError("Error starting Entry Point Manager in RefreshEntryPoints(). " + ex.Message);
                        return;
                    }

                    try
                    {
                        List<EntryPointItem> entryPoints = new List<EntryPointItem>();

                        try
                        {
                            Logger.ReportInfo("Reloading Virtual children");
                            if (RefreshPlugins)
                            {
                                //Kernel.Init(KernelLoadDirective.ShadowPlugins);
                                Kernel.Instance.ReLoadRoot();
                            }

                            Kernel.Instance.RootFolder.ValidateChildren();
                        }
                        catch (Exception ex)
                        {
                            Logger.ReportError("Error validating children. " + ex.Message, ex);
                            throw new Exception("Error validating children. " + ex.Message);
                        }

                        foreach (var folder in Kernel.Instance.RootFolder.Children)
                        {
                            String displayName = folder.Name;
                            if (displayName == null || displayName.Length <= 0)
                                continue;

                            String path = string.Empty;

                            if (folder.GetType() == typeof(Folder) && folder.Path != null && folder.Path.Length > 1)
                            {
                                path = folder.Path;
                            }
                            else
                            {
                                path = folder.Id.ToString();
                            }

                            EntryPointItem ep = new EntryPointItem(displayName, path);
                            entryPoints.Add(ep);
                        }

                        epm.ValidateEntryPoints(entryPoints);
                    }
                    catch (Exception ex)
                    {
                        String msg = "Error Refreshing Entry Points. " + ex.Message;
                        Logger.ReportError(msg, ex);
                        //MessageBox.Show(msg);
                    }
                }
            });
        }

        #region events


        private void pluginList_DoubleClicked(object sender, RoutedEventArgs e)
        {
            configurePlugin_Click(sender, e);
        }

        private void pluginList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pluginList.SelectedItem != null)
            {
                var plugin = (IPlugin)pluginList.SelectedItem;
                var v = PluginManager.Instance.GetLatestVersion(plugin);
                var latest = PluginManager.Instance.AvailablePlugins.Find(plugin, v);
                var rv = latest != null ? latest.RequiredMBVersion : plugin.RequiredMBVersion;
                var bv = PluginManager.Instance.GetBackedUpVersion(plugin);
                //enable the remove button if a plugin is selected.
                removePlugin.IsEnabled = true;

                //show the pluginPanel
                pluginPanel.Visibility = Visibility.Visible;
                if (v != null)
                {
                    if (v > plugin.Version && rv <= Kernel.Instance.Version)
                        {
                        upgradePlugin.IsEnabled = true;
                    }
                    else
                    {
                        upgradePlugin.IsEnabled = false;
                    }
                    latestPluginVersion.Content = v.ToString();
                }
                else
                {
                    latestPluginVersion.Content = "Unknown";
                    upgradePlugin.IsEnabled = false;
                }
                //show backup if exists
                if (bv != null)
                {
                    lblBackedUpVersion.Content = bv.ToString();
                    btnRollback.IsEnabled = (bv != plugin.Version);
                    rollbackPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    btnRollback.IsEnabled = false;
                    rollbackPanel.Visibility = Visibility.Hidden;
                }
            }
            else
            {
                pluginPanel.Visibility = Visibility.Hidden;
            }
        }

        private void upgradePlugin_Click(object sender, RoutedEventArgs e) {
            if (pluginList.SelectedItem != null)
            {
                var plugin = pluginList.SelectedItem as IPlugin;
                //get our latest version so we can upgrade...
                var newPlugin = PluginManager.Instance.AvailablePlugins.Find(plugin, PluginManager.Instance.GetLatestVersion(plugin));
                if (newPlugin != null)
                {
                    if (!string.IsNullOrEmpty(newPlugin.UpgradeInfo))
                    {
                        //confirm upgrade
                        if (MessageBox.Show("This upgrade has the following information:\n\n" + newPlugin.UpgradeInfo + "\n\nDo you still wish to upgrade?", "Upgrade " + plugin.Name, MessageBoxButton.YesNo) == MessageBoxResult.No)
                        {
                            PopUpMsg.DisplayMessage("Upgrade Cancelled");
                            return;
                        }
                    }
                    var p = new PluginInstaller();
                    var done = new CallBack(UpgradeFinished);
                    this.IsEnabled = false;
                    p.InstallPlugin(newPlugin, progress, this, done);
                    KernelModified = true;
                }
            }
        }


        private delegate void CallBack();

        public void UpgradeFinished()
        {
            //called when the upgrade process finishes - we just hide progress bar and re-enable
            this.IsEnabled = true;
            var plugin = pluginList.SelectedItem as IPlugin;
            try
            {
                PluginManager.Instance.RefreshInstalledPlugins(); //refresh list
            }
            catch (Exception e)
            {
                Logger.ReportException("Error refreshing plugins after upgrade", e);
            }
            if (plugin != null)
            {
                Logger.ReportInfo(plugin.Name + " Upgraded to v" + PluginManager.Instance.GetLatestVersion(plugin));
            }
            progress.Value = 0;
            progress.Visibility = Visibility.Hidden;
        }

        private void addExtenderFormat_Click(object sender, RoutedEventArgs e)
        {
            var form = new AddExtenderFormat {Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner};
            var result = form.ShowDialog();
            if (result == true)
            {
                var parser = new FormatParser(config.ExtenderNativeTypes);
                parser.Add(form.formatName.Text);
                config.ExtenderNativeTypes = parser.ToString();
                RefreshExtenderFormats();
                SaveConfig();
            }
        }

        private void removeExtenderFormat_Click(object sender, RoutedEventArgs e)
        {
            var format = extenderFormats.SelectedItem as string;
            if (format != null)
            {
                var message = "Remove \"" + format + "\"?";
                if (
                  MessageBox.Show(message, "Remove folder", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
                {
                    var parser = new FormatParser(config.ExtenderNativeTypes);
                    parser.Remove(format);
                    config.ExtenderNativeTypes = parser.ToString();
                    RefreshExtenderFormats();
                    SaveConfig();
                }
            }
        }

        private void changeDaemonToolsLocation_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Filter = "Executable Files (*.exe)|*.exe";
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                config.DaemonToolsLocation = dialog.FileName;
                //daemonToolsLocation.Content = config.DaemonToolsLocation;
                daemonToolsLocation.Text = config.DaemonToolsLocation;
                SaveConfig();
            }
        }

        private void daemonToolsDrive_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (daemonToolsDrive.SelectedValue != null)
            {
                config.DaemonToolsDrive = (string)daemonToolsDrive.SelectedValue;
            }
            SaveConfig();
        }

        private void btnRemovePlayer_Click(object sender, RoutedEventArgs e)
        {
            string message;
            const string title = "Remove External Player Confirmation";

            if (lstExternalPlayers.SelectedItems.Count > 1)
            {
                message = "About to remove the selected external players. Are you sure?";
            }
            else
            {
                var mediaPlayer = (ConfigData.ExternalPlayer)lstExternalPlayers.SelectedItem;

                message = "About to remove " + mediaPlayer.ExternalPlayerName + ". Are you sure?";                             
            }

            if (MessageBox.Show(message, title, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (ConfigData.ExternalPlayer player in lstExternalPlayers.SelectedItems)
            {
                config.ExternalPlayers.Remove(player);               
            }

            SaveConfig();
            RefreshPlayers();
        }

        private void btnAddPlayer_Click(object sender, RoutedEventArgs e)
        {
            EditExternalPlayer(new PlayableExternalConfigurator().GetDefaultConfiguration(), true);
        }

        private void lstExternalPlayers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstExternalPlayers.SelectedItem != null)
            {
                btnEditPlayer_Click(sender, e);
            }
        }

        private void btnEditPlayer_Click(object sender, RoutedEventArgs e)
        {
            var externalPlayer = lstExternalPlayers.SelectedItem as ConfigData.ExternalPlayer;
            
            EditExternalPlayer(externalPlayer, false);
        }

        private void EditExternalPlayer(ConfigData.ExternalPlayer externalPlayer, bool isNew)
        {
            var form = new ExternalPlayerForm(isNew) {Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner};

            form.FillControlsFromObject(externalPlayer);

            if (form.ShowDialog() == true)
            {
                form.UpdateObjectFromControls(externalPlayer);

                if (isNew)
                {
                    config.ExternalPlayers.Add(externalPlayer);
                }

                SaveConfig();

                RefreshPlayers();

                lstExternalPlayers.SelectedItem = externalPlayer;
            }
        }

        private void lstExternalPlayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedIndex = lstExternalPlayers.SelectedIndex;
            var hasSelection = selectedIndex >= 0;
            var hasMultiSelection = lstExternalPlayers.SelectedItems.Count > 1;

            btnRemovePlayer.IsEnabled = hasSelection;
            btnEditPlayer.IsEnabled = hasSelection && !hasMultiSelection;
            btnMoveExternalPlayerUp.IsEnabled = hasSelection && !hasMultiSelection && selectedIndex > 0;
            btnMoveExternalPlayerDown.IsEnabled = hasSelection && !hasMultiSelection && selectedIndex < (lstExternalPlayers.Items.Count - 1);
        }

        void btnMoveExternalPlayerUp_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = lstExternalPlayers.SelectedIndex;

            MoveExternalPlayer(selectedIndex, selectedIndex - 1);
        }

        void btnMoveExternalPlayerDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = lstExternalPlayers.SelectedIndex;

            MoveExternalPlayer(selectedIndex, selectedIndex + 1);
        }
        private void MoveExternalPlayer(int oldIndex, int newIndex)
        {
            var externalPlayer = config.ExternalPlayers[oldIndex];

            //remove from current location
            config.ExternalPlayers.RemoveAt(oldIndex);
            //add back above item above us
            config.ExternalPlayers.Insert(newIndex, externalPlayer);
            SaveConfig();
            RefreshPlayers();
            //finally, re-select this item
            lstExternalPlayers.SelectedItem = externalPlayer;
        }
        #endregion

        #region CheckBox Events

        private void useAutoPlay_Click(object sender, RoutedEventArgs e)
        {
            config.UseAutoPlayForIso = (bool)useAutoPlay.IsChecked;
            SaveConfig();
        }
        private void enableTranscode360_Click(object sender, RoutedEventArgs e)
        {
            config.EnableTranscode360 = (bool)enableTranscode360.IsChecked;
            SaveConfig();
        }

        private void cbxOptionClock_Click(object sender, RoutedEventArgs e)
        {
            config.ShowClock = (bool)cbxOptionClock.IsChecked;
            SaveConfig();
        }

        private void cbxOptionTransparent_Click(object sender, RoutedEventArgs e)
        {
            config.ShowThemeBackground = (bool)cbxOptionTransparent.IsChecked;
            SaveConfig();
        }

        private void cbxOptionIndexing_Click(object sender, RoutedEventArgs e)
        {
            config.RememberIndexing = (bool)cbxOptionIndexing.IsChecked;
            SaveConfig();
        }

        private void cbxOptionDimPoster_Click(object sender, RoutedEventArgs e)
        {
            config.DimUnselectedPosters = (bool)cbxOptionDimPoster.IsChecked;
            SaveConfig();
        }

        private void cbxOptionUnwatchedCount_Click(object sender, RoutedEventArgs e)
        {
            config.ShowUnwatchedCount = (bool)cbxOptionUnwatchedCount.IsChecked;
            SaveConfig();
        }

        private void cbxOptionUnwatchedOnFolder_Click(object sender, RoutedEventArgs e)
        {
            config.ShowWatchedTickOnFolders = (bool)cbxOptionUnwatchedOnFolder.IsChecked;
            SaveConfig();
        }

        private void cbxOptionUnwatchedOnVideo_Click(object sender, RoutedEventArgs e)
        {
            config.ShowWatchTickInPosterView = (bool)cbxOptionUnwatchedOnVideo.IsChecked;
            SaveConfig();
        }

        private void cbxOptionUnwatchedDetailView_Click(object sender, RoutedEventArgs e)
        {
            config.EnableListViewTicks = (bool)cbxOptionUnwatchedDetailView.IsChecked;
            SaveConfig();
        }

        private void cbxOptionDefaultToUnwatched_Click(object sender, RoutedEventArgs e)
        {
            config.DefaultToFirstUnwatched = (bool)cbxOptionDefaultToUnwatched.IsChecked;
            SaveConfig();
        }

        private void cbxOptionHideFrame_Click(object sender, RoutedEventArgs e)
        {
            config.HideFocusFrame = (bool)cbxOptionHideFrame.IsChecked;
            SaveConfig();
        }

        private void cbxScreenSaver_Click(object sender, RoutedEventArgs e)
        {
            config.EnableScreenSaver = (bool)cbxScreenSaver.IsChecked;
            SaveConfig();

        }

        private void cbxOptionAspectRatio_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)cbxOptionAspectRatio.IsChecked)
            {
                config.MaximumAspectRatioDistortion = Constants.MAX_ASPECT_RATIO_STRETCH;
            }
            else
            {
                config.MaximumAspectRatioDistortion = Constants.MAX_ASPECT_RATIO_DEFAULT;
            }

            SaveConfig();
        }
        private void cbxRootPage_Click(object sender, RoutedEventArgs e)
        {
            WeatherGrid.IsEnabled = (bool)cbxRootPage.IsChecked;

            config.EnableRootPage = (bool)cbxRootPage.IsChecked;
            SaveConfig();
        }
        private void cbxOptionAutoEnter_Click(object sender, RoutedEventArgs e)
        {
            config.AutoEnterSingleDirs = (bool)cbxOptionAutoEnter.IsChecked;
            SaveConfig();
        }

        private void cbxAutoValidate_Click(object sender, RoutedEventArgs e)
        {
            config.AutoValidate = (bool)cbxAutoValidate.IsChecked;
            if (!config.AutoValidate) PopUpMsg.DisplayMessage("Warning! Media Changes May Not Be Reflected in Library.");
            SaveConfig();
        }

        private void enableLogging_Click(object sender, RoutedEventArgs e)
        {
            config.EnableTraceLogging = (bool)cbxEnableLogging.IsChecked;
            SaveConfig();
        }

        private void CbxOptionShowFavorites_OnClick(object sender, RoutedEventArgs e)
        {
            config.ShowFavoritesCollection = (bool)cbxOptionShowFavorites.IsChecked;
            SaveConfig();
        }

        #endregion

        #region ComboBox Events
        private void ddlOptionViewTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ddlOptionViewTheme.SelectedValue != null)
            {
                config.ViewTheme = ddlOptionViewTheme.SelectedValue.ToString();
            }
            SaveConfig();
        }

        private void ddlOptionThemeColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ddlOptionThemeColor.SelectedValue != null)
            {
                config.Theme = ddlOptionThemeColor.SelectedValue.ToString();
            }
            SaveConfig();
        }

        private void ddlOptionThemeFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ddlOptionThemeFont.SelectedValue != null)
            {
                config.FontTheme = ddlOptionThemeFont.SelectedValue.ToString();
            }
            SaveConfig();
        }
        #endregion

        #region Header Selection Methods
        private void eggExpert_Click(object sender, MouseButtonEventArgs e)
        {
            if (System.Windows.Forms.Control.ModifierKeys == (System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift))
            {
                InitExpertMode();
                expertTab.Visibility = Visibility.Visible;
                helpTab.Visibility = Visibility.Collapsed;
                tabMain.SelectedItem = expertTab;
            }
        }

        #endregion

        private void btnWeatherID_Click(object sender, RoutedEventArgs e)
        {
            if (ddlWeatherUnits.SelectedItem.ToString() == "Fahrenheit")
                config.YahooWeatherUnit = "f";
            else
                config.YahooWeatherUnit = "c";
            config.YahooWeatherFeed = tbxWeatherID.Text;
            SaveConfig();
        }


        private void removePlugin_Click(object sender, RoutedEventArgs e) {
            var plugin = pluginList.SelectedItem as IPlugin;
            if (plugin != null)
            {
                var message = "Would you like to remove the plugin " + plugin.Name + "?";
                if (
                      MessageBox.Show(message, "Remove plugin", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    PluginManager.Instance.RemovePlugin(plugin);
                    PluginManager.Instance.UpdateAvailableAttributes(plugin, false);
                    RefreshEntryPoints(true);
                    RefreshThemes();
                }
            }
        }

        private void addPlugin_Click(object sender, RoutedEventArgs e) {
            var window = new AddPluginWindow();
            window.Owner = this;
            window.Top = 10;
            window.Left = this.Left + 50;
            if (window.Left + window.Width > SystemParameters.WorkArea.Width) window.Left = SystemParameters.WorkArea.Width - window.Width - 5;
            if (window.Left < 0) window.Left = 5;
            if (SystemParameters.WorkArea.Height - 10 < (window.Height)) window.Height = SystemParameters.WorkArea.Height - 10;
            window.ShowDialog();
            Async.Queue("Refresh after plugin add", () => RefreshEntryPoints(true));
            var current = pluginList.SelectedIndex;
            PluginManager.Instance.RefreshInstalledPlugins(); //refresh list
            if (current > pluginList.Items.Count) current = pluginList.Items.Count;
            pluginList.SelectedIndex = current;
            RefreshThemes();
        }

        private void configurePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (pluginList.SelectedItem != null && (pluginList.SelectedItem as Plugin).IsConfigurable)
            {
                ((Plugin)pluginList.SelectedItem).Configure();

                this.RefreshEntryPoints(true);
                KernelModified = true;
            }
        }

        void HandleRequestNavigate(object sender, RoutedEventArgs e)
        {
            Hyperlink hl = (Hyperlink)sender;
            string navigateUri = hl.NavigateUri.ToString();
            // if the URI somehow came from an untrusted source, make sure to
            // validate it before calling Process.Start(), e.g. check to see
            // the scheme is HTTP, etc.
            Process.Start(new ProcessStartInfo(navigateUri));
            e.Handled = true;
        }

        void HandleDashboardNavigate(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(Kernel.ApiClient.DashboardUrl));
            e.Handled = true;
        }


        private void openLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("" + ApplicationPaths.AppLogPath + "");
            }
            catch
            {
                MessageBox.Show("We were unable to open the Logs folder:\n\n" + ApplicationPaths.AppLogPath + "\n\nMake sure the actual folder exists on the local disk.");
            }
        }

        private void tbxNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Char.IsDigit(e.Text[0]);
            base.OnPreviewTextInput(e);
        }

        private void btnSSTimeUp_Click(object sender, RoutedEventArgs e)
        {
            config.ScreenSaverTimeOut++;
            config.Save();
            lblSSTimeout.Content = config.ScreenSaverTimeOut.ToString() + " Mins";
        }

        private void btnSSTimeDn_Click(object sender, RoutedEventArgs e)
        {
            config.ScreenSaverTimeOut--;
            if (config.ScreenSaverTimeOut < 1) config.ScreenSaverTimeOut = 1;
            config.Save();
            lblSSTimeout.Content = config.ScreenSaverTimeOut.ToString() + " Mins";
        }

        private void btnRICUp_Click(object sender, RoutedEventArgs e)
        {
            config.RecentItemCollapseThresh++;
            config.Save();
            lblRecentItemCollapse.Content = config.RecentItemCollapseThresh;
        }

        private void btnRICDn_Click(object sender, RoutedEventArgs e)
        {
            config.RecentItemCollapseThresh--;
            if (config.RecentItemCollapseThresh < 1) config.RecentItemCollapseThresh = 1;
            config.Save();
            lblRecentItemCollapse.Content = config.RecentItemCollapseThresh;
        }


        private void btnRollback_Click(object sender, RoutedEventArgs e)
        {
            var plugin = pluginList.SelectedItem as IPlugin;
            if (plugin == null) return;
            if (MessageBox.Show("Are you sure you want to overwrite your current version of "+plugin.Name, "Rollback Plug-in", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                this.Cursor = Cursors.Wait;
                if (PluginManager.Instance.RollbackPlugin(plugin))
                {
                    PluginManager.Instance.RefreshInstalledPlugins();
                    pluginList.SelectedIndex = 0;
                    this.Cursor = Cursors.Arrow;
                    Logger.ReportInfo(plugin.Name + " rolled back.");
                    PopUpMsg.DisplayMessage("Plugin " + plugin.Name + " rolled back.");
                }
                else
                {
                    Logger.ReportError("Error attempting to rollback plugin " + plugin.Name);
                    this.Cursor = Cursors.Arrow;
                    MessageBox.Show("Error attempting to rollback plugin " + plugin.Name, "Rollback Failed");
                }
            }
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            //if we modified anything in kernel reload the service so it will pick up any changes we made
            if (KernelModified)
                MBServiceController.RestartService();
        }

        private void ddlLoglevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ddlLoglevel.SelectedItem != null)
            {
                config.MinLoggingSeverity = (LogSeverity)ddlLoglevel.SelectedItem;
                config.Save();
            }
        }

        private void configMemberList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            currentConfigMember = configMemberList.SelectedItem as ConfigMember;
            if (currentConfigMember != null)
            {
                txtMemberComment.Text = currentConfigMember.Comment;
                lblDangerous.Visibility = currentConfigMember.IsDangerous ? Visibility.Visible : Visibility.Hidden;
                switch (currentConfigMember.Type.Name)
                {
                    case "Boolean":
                        stringGrid.Visibility = numGrid.Visibility = Visibility.Hidden;
                        cbxBoolMember.Visibility = System.Windows.Visibility.Visible;
                        cbxBoolMember.Content = currentConfigMember.Name;
                        cbxBoolMember.IsChecked = (bool)currentConfigMember.Value;
                        break;

                    case "String":
                        if (currentConfigMember.PresentationStyle == "BrowseFolder")
                            btnFolderBrowse.Visibility = Visibility.Visible;
                        else
                            btnFolderBrowse.Visibility = Visibility.Hidden;
                        lblString.Content = currentConfigMember.Name;
                        tbxString.Text = currentConfigMember.Value.ToString();
                        stringGrid.Visibility = Visibility.Visible;
                        cbxBoolMember.Visibility = numGrid.Visibility = Visibility.Hidden;
                        break;

                    case "Int":
                    case "Int32":
                    case "Int16":
                    case "Double":
                    case "Single":
                        lblNum.Content = currentConfigMember.Name;
                        tbxNum.Text = currentConfigMember.Value.ToString();
                        numGrid.Visibility = Visibility.Visible;
                        cbxBoolMember.Visibility = stringGrid.Visibility = Visibility.Hidden;
                        break;

                    default:
                        cbxBoolMember.Visibility = stringGrid.Visibility = numGrid.Visibility = System.Windows.Visibility.Hidden;
                        break;
                }

            }
            else
            {
                txtMemberComment.Text = "";
                lblDangerous.Visibility = cbxBoolMember.Visibility = stringGrid.Visibility = Visibility.Hidden;
            }
        }

        private void memberList_Collapse(object sender, RoutedEventArgs e)
        {
            //un-select in case current item was collapsed from view
            configMemberList.SelectedIndex = -1;

        }

        private void cbxBoolMember_Checked(object sender, RoutedEventArgs e)
        {
            if (currentConfigMember != null)
            {
                currentConfigMember.Value = cbxBoolMember.IsChecked;
                config.Save();
            }
        }

        private void tbxNum_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            //called when one of our dynamic member items loses focus - save current member
            if (currentConfigMember != null)
            {
                currentConfigMember.Value = Convert.ToInt32(tbxNum.Text);
                config.Save();
            }
        }

        private void tbxString_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            //called when one of our dynamic member items loses focus - save current member
            if (currentConfigMember != null)
            {
                currentConfigMember.Value = tbxString.Text;
                config.Save();
            }
        }

        private void btnFolderBrowse_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFolderDialog dlg = new BrowseForFolderDialog();

            if (true == dlg.ShowDialog(this))
            {
                currentConfigMember.Value = tbxString.Text =  dlg.SelectedFolder;
            }

        }

        private void BtnFavoritesName_OnClick(object sender, RoutedEventArgs e)
        {
            config.FavoriteFolderName = tbxFavoriteName.Text;
            config.Save();
        }
    }

    #region FormatParser Class
    class FormatParser
    {

        List<string> currentFormats = new List<string>();

        public FormatParser(string value)
        {
            currentFormats.AddRange(value.Split(','));
        }

        public void Add(string format)
        {
            format = format.Trim();
            if (!format.StartsWith("."))
            {
                format = "." + format;
            }
            format = format.ToLower();

            if (format.Length > 1)
            {
                if (!currentFormats.Contains(format))
                {
                    currentFormats.Add(format);
                }
            }
        }

        public void Remove(string format)
        {
            currentFormats.Remove(format);
        }

        public override string ToString()
        {
            return String.Join(",", currentFormats.ToArray());
        }


    }
    #endregion

    #region DummyTreeItem Class
    class DummyTreeItem {
    }
    #endregion

}
