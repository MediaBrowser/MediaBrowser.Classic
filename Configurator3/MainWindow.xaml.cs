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
using System.Windows.Forms;
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
using MediaBrowser.Util;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;

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
                Async.Queue(Async.ThreadPoolName.Error, () =>
                                         {
                                             MessageBox.Show("We encountered a critical error and will need to shut down.\n\n  It is possible we cannot contact your MB Server or the internet.\n\n" +
                                                             " If the problem persists, please post the following on http://mediabrowser3.com/community \n\n" + ex, "Critical Error", MessageBoxButton.OK);
                                             Close();
                                         });
                Logger.ReportException("Error Starting up",ex);
            }

        }

        private void Initialize() {
            Instance = this;
            InitializeComponent();
            Kernel.Init(KernelLoadDirective.ShadowPlugins);
            Logger.ReportVerbose("======= Kernel intialized. Building window...");
            commonConfig = Kernel.Instance.CommonConfigData;
            PopUpMsg = new PopupMsg(alertText);

            //Logger.ReportVerbose("======= Loading combo boxes...");
            lblVersion.Content = lblVersion2.Content = "Version " + Kernel.Instance.VersionStr;

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
            Async.Queue(Async.ThreadPoolName.SetDirectoryPermissions, 
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
            useAutoPlay.IsChecked = commonConfig.UseAutoPlayForIso;

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

        #region events


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
        #endregion

        #region ComboBox Events
        #endregion

        #region Header Selection Methods
        #endregion


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



        private void Window_Closing(object sender, EventArgs e)
        {
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
