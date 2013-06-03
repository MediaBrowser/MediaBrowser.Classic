using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using System.Globalization;
using Configurator.Code;
using MediaBrowser;
using MediaBrowser.ApiInteraction;
using MediaBrowser.Library;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Playables.ExternalPlayer;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library.Threading;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Updates;

namespace Configurator
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        //public ConfigData config;
        public CommonConfigData commonConfig;
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
                // set up assembly resolution hooks, so earlier versions of the plugins resolve properly 
                AppDomain.CurrentDomain.AssemblyResolve += Kernel.OnAssemblyResolve;

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
            if (!Kernel.ServerConnected)
            {
                MessageBox.Show("Cannot connect to the MB3 server.  Please start it or configure address.", "Cannot find server");
            }
            else
            {
                var user = Kernel.AvailableUsers.OrderBy(u => u.Name).FirstOrDefault();
                Kernel.CurrentUser = new User { Name = user.Name, Id = new Guid(user.Id ?? ""), Dto = user, ParentalAllowed = user.HasPassword };
            }
            //Kernel.Instance.LoadUserConfig();
            Kernel.Instance.LoadPlugins();
            Logger.ReportVerbose("======= Kernel intialized. Building window...");
            InitializeComponent();
            commonConfig = Kernel.Instance.CommonConfigData;
            pluginList.MouseDoubleClick += pluginList_DoubleClicked;
            PopUpMsg = new PopupMsg(alertText);
            //config = Kernel.Instance.ConfigData;

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
                daemonToolsDrive.SelectedValue = commonConfig.DaemonToolsDrive;
            } catch {
                // someone bodged up the config
            }

            //daemonToolsLocation.Content = config.DaemonToolsLocation; /// old
            daemonToolsLocation.Text = commonConfig.DaemonToolsLocation;


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
            //Async.Queue("Startup Validations", () =>
            //{
            //    //RefreshEntryPoints(false);
            //    ValidateMBAppDataFolderPermissions();
            //});
            //Logger.ReportVerbose("======= Initialize Finised.");
        }

        public void ValidateMBAppDataFolderPermissions()
        {
            const string windowsAccount = "Users"; 
            const FileSystemRights fileSystemRights = FileSystemRights.FullControl;
            var folder = new DirectoryInfo(ApplicationPaths.AppProgramPath);

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



        #region Config Loading / Saving        
        private void LoadConfigurationSettings()
        {
            enableTranscode360.IsChecked = commonConfig.EnableTranscode360;
            useAutoPlay.IsChecked = commonConfig.UseAutoPlayForIso;

            ddlLoglevel.SelectedItem = commonConfig.MinLoggingSeverity;

            if (commonConfig.FindServerAutomatically)
            {
                rbServerConnectAuto.IsChecked = true;
            }
            else
            {
                rbServerConnectManual.IsChecked = true;
            }
            
            if (commonConfig.LogonAutomatically)
            {
                rbLogonAuto.IsChecked = true;
                ddlUserProfile.SelectedItem = Kernel.AvailableUsers.FirstOrDefault(u => u.Name.Equals(commonConfig.AutoLogonUserName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                rbShowUserSelection.IsChecked = true;
            }

            if (Kernel.ServerConnected)
            {
                tbxServerAddress.Text = Kernel.ApiClient.ServerHostName;
                tbxPort.Text = Kernel.ApiClient.ServerApiPort.ToString();
            }

            //logging
            cbxEnableLogging.IsChecked = commonConfig.EnableTraceLogging;

            //library validation
            cbxAutoValidate.IsChecked = commonConfig.AutoValidate;

            //updates
            cbxCheckForUpdates.IsChecked = commonConfig.EnableUpdates;
            ddlSystemUpdateLevel.SelectedItem = commonConfig.SystemUpdateClass.ToString();
            ddlPluginUpdateLevel.SelectedItem = commonConfig.PluginUpdateClass.ToString();

        }

        private void SaveConfig()
        {
            commonConfig.Save();
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
            ddlSystemUpdateLevel.ItemsSource = ddlPluginUpdateLevel.ItemsSource = Enum.GetNames(typeof(PackageVersionClass));
            ddlLoglevel.ItemsSource = Enum.GetValues(typeof(LogSeverity));
            RefreshUsers();

        }

        private void RefreshUsers()
        {
            ddlUserProfile.ItemsSource = Kernel.AvailableUsers.OrderBy(u => u.Name);
        }

        #endregion

        private void RefreshExtenderFormats()
        {
            extenderFormats.Items.Clear();
            foreach (var format in commonConfig.ExtenderNativeTypes.Split(','))
            {
                extenderFormats.Items.Add(format);
            }
        }

        private void RefreshDisplaySettings()
        {
            extenderFormats.Items.Clear();
            foreach (var format in commonConfig.ExtenderNativeTypes.Split(','))
            {
                extenderFormats.Items.Add(format);
            }
        }

        private void RefreshPlayers()
        {
            lstExternalPlayers.Items.Clear();
            foreach (var item in commonConfig.ExternalPlayers)
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
                var parser = new FormatParser(commonConfig.ExtenderNativeTypes);
                parser.Add(form.formatName.Text);
                commonConfig.ExtenderNativeTypes = parser.ToString();
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
                    var parser = new FormatParser(commonConfig.ExtenderNativeTypes);
                    parser.Remove(format);
                    commonConfig.ExtenderNativeTypes = parser.ToString();
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
                commonConfig.DaemonToolsLocation = dialog.FileName;
                daemonToolsLocation.Text = commonConfig.DaemonToolsLocation;
                SaveConfig();
            }
        }

        private void daemonToolsDrive_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (daemonToolsDrive.SelectedValue != null)
            {
                commonConfig.DaemonToolsDrive = (string)daemonToolsDrive.SelectedValue;
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
                var mediaPlayer = (CommonConfigData.ExternalPlayer)lstExternalPlayers.SelectedItem;

                message = "About to remove " + mediaPlayer.ExternalPlayerName + ". Are you sure?";                             
            }

            if (MessageBox.Show(message, title, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (CommonConfigData.ExternalPlayer player in lstExternalPlayers.SelectedItems)
            {
                commonConfig.ExternalPlayers.Remove(player);               
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
            var externalPlayer = lstExternalPlayers.SelectedItem as CommonConfigData.ExternalPlayer;
            
            EditExternalPlayer(externalPlayer, false);
        }

        private void EditExternalPlayer(CommonConfigData.ExternalPlayer externalPlayer, bool isNew)
        {
            var form = new ExternalPlayerForm(isNew) {Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner};

            form.FillControlsFromObject(externalPlayer);

            if (form.ShowDialog() == true)
            {
                form.UpdateObjectFromControls(externalPlayer);

                if (isNew)
                {
                    commonConfig.ExternalPlayers.Add(externalPlayer);
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
            var externalPlayer = commonConfig.ExternalPlayers[oldIndex];

            //remove from current location
            commonConfig.ExternalPlayers.RemoveAt(oldIndex);
            //add back above item above us
            commonConfig.ExternalPlayers.Insert(newIndex, externalPlayer);
            SaveConfig();
            RefreshPlayers();
            //finally, re-select this item
            lstExternalPlayers.SelectedItem = externalPlayer;
        }
        #endregion

        #region CheckBox Events

        private void useAutoPlay_Click(object sender, RoutedEventArgs e)
        {
            commonConfig.UseAutoPlayForIso = (bool)useAutoPlay.IsChecked;
            SaveConfig();
        }
        private void enableTranscode360_Click(object sender, RoutedEventArgs e)
        {
            commonConfig.EnableTranscode360 = (bool)enableTranscode360.IsChecked;
            SaveConfig();
        }


        private void cbxAutoValidate_Click(object sender, RoutedEventArgs e)
        {
            commonConfig.AutoValidate = (bool)cbxAutoValidate.IsChecked;
            if (!commonConfig.AutoValidate) PopUpMsg.DisplayMessage("Warning! Media Changes May Not Be Reflected in Library.");
            SaveConfig();
        }

        private void enableLogging_Click(object sender, RoutedEventArgs e)
        {
            commonConfig.EnableTraceLogging = (bool)cbxEnableLogging.IsChecked;
            SaveConfig();
        }

        #endregion

        #region ComboBox Events
        #endregion

        #region Header Selection Methods
        #endregion


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
        }

        private void ddlLoglevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ddlLoglevel.SelectedItem != null)
            {
                commonConfig.MinLoggingSeverity = (LogSeverity)ddlLoglevel.SelectedItem;
                SaveConfig();
            }
        }

        private void DdlUserProfile_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var user = ddlUserProfile.SelectedItem as UserDto;
            if (user != null)
            {
                if (user.HasPassword)
                {
                    lblPw.Visibility = tbxUserPassword.Visibility = Visibility.Visible;
                    tbxUserPassword.IsEnabled = true;
                }
                else
                {
                    lblPw.Visibility = tbxUserPassword.Visibility = Visibility.Hidden;
                    tbxUserPassword.IsEnabled = false;
                    tbxUserPassword.Password = "";
                }
            }
        }

        private void RbServerConnectAuto_OnChecked(object sender, RoutedEventArgs e)
        {
            commonConfig.FindServerAutomatically = true;
            tbxServerAddress.IsEnabled = tbxPort.IsEnabled = false;
        }

        private void RbShowUserSelection_OnChecked(object sender, RoutedEventArgs e)
        {
            commonConfig.LogonAutomatically = false;
            ddlUserProfile.IsEnabled = false;
            tbxUserPassword.Visibility = Visibility.Hidden;
            lblPw.Visibility = Visibility.Hidden;
        }

        private void rbServerConnectManual_Checked(object sender, RoutedEventArgs e)
        {
            commonConfig.FindServerAutomatically = false;
            tbxServerAddress.IsEnabled = tbxPort.IsEnabled = true;
        }

        private void rbLogonAuto_Checked(object sender, RoutedEventArgs e)
        {
            commonConfig.LogonAutomatically = true;
            ddlUserProfile.IsEnabled = true;
            if (ddlUserProfile.SelectedItem == null && ddlUserProfile.Items.Count > 0) ddlUserProfile.SelectedIndex = 0;
            DdlUserProfile_OnSelectionChanged(this, null);
        }

        private void btnSaveConnection_Click(object sender, RoutedEventArgs e)
        {
            //Validate server address
            if (rbServerConnectAuto.IsChecked == true)
            {
                var endpoint = new ServerLocator().FindServer();
                if (endpoint == null)
                {
                    MessageBox.Show("Unable to find server.  Please specify an address or start the server.", "Error locating server");
                    return;
                }
                tbxServerAddress.Text = endpoint.Address.ToString();
                tbxPort.Text = endpoint.Port.ToString();
            }
            PopUpMsg.DisplayMessage("Attempting to contact server...");
            var address = tbxServerAddress.Text;
            var port = Convert.ToInt32(tbxPort.Text);
            this.Cursor = Cursors.AppStarting;
            btnSaveConnection.IsEnabled = false;
            Async.Queue("ConnectionCheck", () => Kernel.ConnectToServer(address, port), () => Dispatcher.Invoke(DispatcherPriority.Background,(System.Windows.Forms.MethodInvoker)ConnectionValidationDone));
        }

        public void ConnectionValidationDone()
        {
            this.Cursor = Cursors.Arrow;
            btnSaveConnection.IsEnabled = true;
            if (!Kernel.ServerConnected)
            {
                MessageBox.Show("Could not connect to server. Please verify address and port.", "Error");
                PopUpMsg.DisplayMessage("Connection Information NOT Saved");
                return;
            }
            //RefreshUsers();
            //if (commonConfig.LogonAutomatically)
            //{
            //    ddlUserProfile.SelectedItem = Kernel.AvailableUsers.FirstOrDefault(u => u.Name.Equals(commonConfig.AutoLogonUserName, StringComparison.OrdinalIgnoreCase));
            //}

            //Validate user
            var user = ddlUserProfile.SelectedItem as UserDto;
            var pw = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(tbxUserPassword.Password ?? string.Empty));
            if (rbLogonAuto.IsChecked == true)
            {
                try
                {
                    Kernel.AvailableUsers = Kernel.ApiClient.GetAllUsers().ToList();
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Unable to get users from server",ex);
                    MessageBox.Show("Error connecting to get users.  Please check server address.", "Cannot get users");
                    PopUpMsg.DisplayMessage("Connection Information NOT Saved");
                    return;
                }
                try
                {
                    if (user != null)
                    {
                        Kernel.ApiClient.AuthenticateUser(user.Id, pw);
                    }
                }
                catch (MediaBrowser.Model.Net.HttpException ex)
                {
                    if (((System.Net.WebException)ex.InnerException).Status == System.Net.WebExceptionStatus.ProtocolError)
                    {
                        MessageBox.Show(string.Format("Incorrect password for user {0}", user) , "Error");
                        PopUpMsg.DisplayMessage("Connection Information NOT Saved");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error validating user", ex);
                    PopUpMsg.DisplayMessage("Connection Information NOT Saved");
                    return;
                }
            }

            //Everything validated - change settings and save
            commonConfig.FindServerAutomatically = rbServerConnectAuto.IsChecked == true;
            commonConfig.ServerAddress = tbxServerAddress.Text;
            commonConfig.ServerPort = Convert.ToInt32(tbxPort.Text);
            commonConfig.LogonAutomatically = rbLogonAuto.IsChecked == true;
            commonConfig.AutoLogonUserName = user != null ? user.Name : null;
            commonConfig.AutoLogonPw = BitConverter.ToString(pw);

            SaveConfig();
            PopUpMsg.DisplayMessage("Connection information validated and saved.");
        }

        private void ddlSystemUpdateLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ddlSystemUpdateLevel.SelectedItem != null)
            {
                commonConfig.SystemUpdateClass = (PackageVersionClass)Enum.Parse(typeof(PackageVersionClass), ddlSystemUpdateLevel.SelectedItem.ToString());
                SaveConfig();
            }
        }

        private void ddlPluginUpdateLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ddlPluginUpdateLevel.SelectedItem != null)
            {
                commonConfig.PluginUpdateClass = (PackageVersionClass)Enum.Parse(typeof(PackageVersionClass), ddlPluginUpdateLevel.SelectedItem.ToString());
                SaveConfig();
            }

        }

        private void btnCheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
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
                        if (MessageBox.Show(string.Format("Update to Version {0} found.  Update now?", newVersion.versionStr), "Update Found", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            MessageBox.Show("Configurator will close to execute update.");
                            // execute update and close us
                            var info = new ProcessStartInfo
                                           {
                                               FileName = ApplicationPaths.UpdaterExecutableFile,
                                               Arguments = "product=mbc class=" + commonConfig.SystemUpdateClass + " admin=true",
                                               Verb = "runas"
                                           };

                            Process.Start(info);
                            Close();
                        }
                    }
                    else
                    {
                        PopUpMsg.DisplayMessage("MB Classic is up to date.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error checking for update: " + ex.Message);
            }

            this.Cursor = Cursors.Arrow;
        }

        private void cbxCheckForUpdates_Checked(object sender, RoutedEventArgs e)
        {
            commonConfig.EnableUpdates = cbxCheckForUpdates.IsChecked == true;
            SaveConfig();
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
