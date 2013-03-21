using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
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
            try {        
                Initialize();
            } catch (Exception ex) {
                MessageBox.Show("Failed to start up, please post this contents on http://community.mediabrowser.tv " + ex.ToString());
            }

        }

        private void Initialize() {
            Instance = this;
            Kernel.Init(KernelLoadDirective.ShadowPlugins);
            ratings = new Ratings();
            //Logger.ReportVerbose("======= Kernel intialized. Building window...");
            InitializeComponent();
            pluginList.MouseDoubleClick += pluginList_DoubleClicked;
            PopUpMsg = new PopupMsg(alertText);
            config = Kernel.Instance.ConfigData;
            //put this check here because it will run before the first run of MB and we need it now
            if (config.MBVersion != Kernel.Instance.Version.ToString() && Kernel.Instance.Version.ToString() == "2.3.0.0")
            {
                try
                {
                    config.PluginSources.RemoveAt(config.PluginSources.FindIndex(s => s.ToLower() == "http://www.mediabrowser.tv/plugins/plugin_info.xml"));
                }
                catch
                {
                    //wasn't there - no biggie
                }
                if (config.PluginSources.Find(s => s == "http://www.mediabrowser.tv/plugins/multi/plugin_info.xml") == null)
                {
                    config.PluginSources.Add("http://www.mediabrowser.tv/plugins/multi/plugin_info.xml");
                    Logger.ReportInfo("Plug-in Source migrated to multi-version source");
                }
                //not going to re-set version in case there is something we want to do in MB itself
            }

            //Logger.ReportVerbose("======= Loading combo boxes...");
            LoadComboBoxes();
            lblVersion.Content = lblVersion2.Content = "Version " + Kernel.Instance.VersionStr;

            //we're showing, but disabling the media collection detail panel until the user selects one
            infoPanel.IsEnabled = false;

            // first time the wizard has run 
            if (config.InitialFolder != ApplicationPaths.AppInitialDirPath) {
                try {
                    MigrateOldInitialFolder();
                } catch {
                    MessageBox.Show("For some reason we were not able to migrate your old initial path, you are going to have to start from scratch.");
                }
            }


            config.InitialFolder = ApplicationPaths.AppInitialDirPath;
            //Logger.ReportVerbose("======= Refreshing Items...");
            RefreshItems();
            //Logger.ReportVerbose("======= Refreshing Podcasts...");
            RefreshPodcasts();
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
            //Logger.ReportVerbose("======= Podcast Details...");
            podcastDetails(false);
            //Logger.ReportVerbose("======= Saving Config...");
            SaveConfig();

            //Logger.ReportVerbose("======= Initializing Plugin Manager...");
            PluginManager.Instance.Init();
            //Logger.ReportVerbose("======= Loading Plugin List...");
            CollectionViewSource src = new CollectionViewSource();
            src.Source = PluginManager.Instance.InstalledPlugins;
            src.GroupDescriptions.Add(new PropertyGroupDescription("PluginClass"));

            pluginList.ItemsSource = src.View;

            //pluginList.Items.Refresh();

            //Logger.ReportVerbose("======= Kicking off plugin update check thread...");
            Async.Queue("Plugin Update Check", () =>
            {
                using (new MediaBrowser.Util.Profiler("Plugin update check"))
                {
                    while (!PluginManager.Instance.PluginsLoaded) { } //wait for plugins to load
                    ForceUpgradeCheck(); //remove incompatable plug-ins
                    if (PluginManager.Instance.UpgradesAvailable())
                        Dispatcher.Invoke(DispatcherPriority.Background, (System.Windows.Forms.MethodInvoker)(() =>
                        {
                            PopUpMsg.DisplayMessage("Some of your plug-ins have upgrades available.");
                        }));
                }
            });

            SupportImprovementNag();

            //Logger.ReportVerbose("======= Kicking off validations thread...");
            Async.Queue("Startup Validations", () =>
            {
                RefreshEntryPoints(false);
                ValidateMBAppDataFolderPermissions();
            });
            //Logger.ReportVerbose("======= Initialize Finised.");
        }

        private void SupportImprovementNag()
        {
            if (!config.SuppressStatsNag && !config.SendStats)
            {
                config.SuppressStatsNag = SuppImproveDialog.Show("Please consider participating in our User Support Enhancement Program.  Only OS version and memory size are collected and only used to help target our future efforts.  Thank you for your support.");
                config.Save();
            }
        }

        private void ForceUpgradeCheck()
        {
            List<IPlugin> foundPlugins = new List<IPlugin>();

            foreach (var entry in PluginManager.RequiredVersions)
            {
                foreach (var plugin in PluginManager.Instance.InstalledPlugins)
                {
                    if (plugin.Name.ToLower() == entry.Key && plugin.Version < entry.Value)
                    {
                        foundPlugins.Add(plugin);
                    }
                }
            }
            if (foundPlugins.Count > 0)
            {
                string plugins = "";
                foreach (var plugin in foundPlugins)
                {
                    plugins += plugin.Name + " version " + plugin.Version + "\n";
                }
                Dispatcher.Invoke(DispatcherPriority.Normal, (System.Windows.Forms.MethodInvoker)(() =>
                {
                    MessageBox.Show("The following plugin versions are not compatible with this version of MB." +
                        "They will be un-installed.\n\nYou can re-install compatible versions through the plug-ins tab.\n\n" +
                        plugins, "Incompatible Plug-ins");
                    foreach (var plugin in foundPlugins)
                    {
                        try
                        {
                            Logger.ReportInfo("Removing incompatable plug-in " + plugin.Name + " version " + plugin.Version);
                            PluginManager.Instance.RemovePlugin(plugin);
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show("Error removing plugin " + plugin.Name + ".  Please remove manually.", "Error");
                            Logger.ReportException("Error force removing plugin " + plugin.Name, e);
                        }
                    }
                }));
            }
        }

        public void ValidateMBAppDataFolderPermissions()
        {
            String windowsAccount = "Users"; 
            FileSystemRights fileSystemRights = FileSystemRights.FullControl;
            DirectoryInfo folder = new DirectoryInfo(ApplicationPaths.AppConfigPath);

            if(!folder.Exists)
            {
                MessageBox.Show(folder.FullName + " does not exist. Cannot validate permissions.");
                return;
            }
            

            if (!ValidateFolderPermissions(windowsAccount, fileSystemRights, folder))
            {
                // removed popup question - just going to confuse the user and we *have* to do this if its not right -ebr
                {
                    object[] args = new object[3] {folder, windowsAccount, fileSystemRights };
                    this.Dispatcher.Invoke(new SetAccessProcess(setAccess),args);
                }
            }
        }

        public delegate void SetAccessProcess(DirectoryInfo folder, string account,FileSystemRights fsRights);
        public void setAccess(DirectoryInfo folder, string account, FileSystemRights fsRights)
        {
            //hide our main window and throw up a quick dialog to tell user what is going on
            this.Visibility = Visibility.Hidden;
            waitWin = new PermissionDialog();
            waitWin.Show();
            Async.Queue("Set Directory Permissions", () => {
                SetDirectoryAccess(folder, account, fsRights, AccessControlType.Allow);
            }, () => { this.Dispatcher.Invoke(new doneProcess(permissionsDone)); });
        }

        public delegate void doneProcess();
        public void permissionsDone()
        {
            //close window and make us visible
            waitWin.Close();
            this.Visibility = Visibility.Visible;
        }
    


        public bool ValidateFolderPermissions(String windowsAccount, FileSystemRights fileSystemRights, DirectoryInfo folder)
        { 
            try
            {                              
                DirectorySecurity dSecurity = folder.GetAccessControl();

                foreach (FileSystemAccessRule rule in dSecurity.GetAccessRules(true, false, typeof(SecurityIdentifier)))
                {
                    //NTAccount account = new NTAccount(windowsAccount);
                    //SecurityIdentifier sID = account.Translate(typeof(SecurityIdentifier)) as SecurityIdentifier;
                    SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null); 
                    if (sid.CompareTo(rule.IdentityReference as SecurityIdentifier) == 0)
                    {
                        if (fileSystemRights == rule.FileSystemRights)
                            return true; // Validation complete 
                            //return false; //test
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
                DirectorySecurity dSecurity = folder.GetAccessControl();
                SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                dSecurity.AddAccessRule(new FileSystemAccessRule(sid, rights, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, controlType));                
                folder.SetAccessControl(dSecurity);
            }
            catch (Exception ex)
            {
                string msg = "Error applying permissions to " + folder.FullName + " for the Account \"" + windowsAccount + "\"";
                Logger.ReportException(msg, ex);
                MessageBox.Show(msg);
            }
        }

        public void InitFolderTree()
        {
            tvwLibraryFolders.BeginInit();
            tvwLibraryFolders.Items.Clear();
            tabMain.Cursor = Cursors.Wait;
            string[] vfs = Directory.GetFiles(ApplicationPaths.AppInitialDirPath,"*.vf");
            foreach (string vfName in vfs)
            {
                TreeViewItem dummyNode = new TreeViewItem();
                dummyNode.Header = new DummyTreeItem();

                TreeViewItem aNode = new TreeViewItem();
                LibraryFolder aFolder = new LibraryFolder(vfName);
                aNode.Header = aFolder;
                aNode.Items.Add(dummyNode);
                
                tvwLibraryFolders.Items.Add(aNode);
            }
            tvwLibraryFolders.EndInit();
            tabMain.Cursor = Cursors.Arrow;
        }

        private void getLibrarySubDirectories(string dir, TreeViewItem parent)
        {
            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(dir);
            }
            catch (Exception ex)
            {
                //something wrong - can't access the directory try to move on
                Logger.ReportException("Couldn't access directory " + dir, ex);
                return;
            }
            foreach (string subdir in dirs)
            {
                //only want directories that don't directly contain movies and are not boxsets in our tree...
                if (!containsMedia(subdir) && !subdir.ToLower().Contains("[boxset]"))
                {
                    TreeViewItem aNode; // = new TreeViewItem();
                    //LibraryFolder aFolder = new LibraryFolder(subdir);
                    //aNode.Header = aFolder;
                    
                    // Throw back up to main thread to add to TreeView
                    // (System.Windows.Forms.MethodInvoker)(() => { aNode = addLibraryFolderNode(parent, subdir); }));
                    AddLibraryFolderCB addNode = new AddLibraryFolderCB(addLibraryFolderNode);

                    Object returnType;
                    returnType = Dispatcher.Invoke(addNode, DispatcherPriority.Background, parent, subdir);

                    aNode = (TreeViewItem)returnType;

                    getLibrarySubDirectories(subdir, aNode);
                }
            }
        }

        private bool containsMedia(string path)
        {
            if (!File.Exists(path + "\\series.xml")
                && !Directory.Exists(path + "\\VIDEO_TS")
                && !Directory.Exists(path + "\\BDMV")
                && !Directory.Exists(path + "\\HVDVD_TS")
                && Directory.GetFiles(path, "*.iso").Length == 0
                && Directory.GetFiles(path, "*.IFO").Length == 0
                && Directory.GetFiles(path, "*.VOB").Length == 0
                && Directory.GetFiles(path, "*.avi").Length == 0
                && Directory.GetFiles(path, "*.mpg").Length == 0
                && Directory.GetFiles(path, "*.mpeg").Length == 0
                && Directory.GetFiles(path, "*.mp3").Length == 0
                && Directory.GetFiles(path, "*.mp4").Length == 0
                && Directory.GetFiles(path, "*.mkv").Length == 0
                && Directory.GetFiles(path, "*.m4v").Length == 0
                && Directory.GetFiles(path, "*.mov").Length == 0
                && Directory.GetFiles(path, "*.m2ts").Length == 0
                && Directory.GetFiles(path, "*.wmv").Length == 0 )
                return false;
            else return true;
        }



        private void RefreshPodcasts() {
            var podcasts = Kernel.Instance.GetItem<Folder>(config.PodcastHome);
            podcastList.Items.Clear();

            if (podcasts != null) {

                RefreshPodcasts(podcasts);

                Async.Queue("Podcast Refresher", () =>
                {
                    podcasts.ValidateChildren();

                    foreach (var item in podcasts.Children) {
                        if (item is VodCast) {
                            (item as VodCast).ValidateChildren();
                        }
                    }

                }, () =>
                {
                    Dispatcher.Invoke(DispatcherPriority.Background, (System.Windows.Forms.MethodInvoker)(() =>
                    {
                        RefreshPodcasts(podcasts);
                    }));
                });
            } 
        }

        private void RefreshPodcasts(Folder podcasts) {
            podcastList.Items.Clear();
            foreach (var item in podcasts.Children) {
                podcastList.Items.Add(item);
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

                CollectionViewSource src = new CollectionViewSource();
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
            lblSSTimeout.Content = config.ScreenSaverTimeOut.ToString()+" Mins";
            cbxSendStats.IsChecked = config.SendStats;

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

            tbxMinResumeDuration.Text = config.MinResumeDuration.ToString();
            lblRecentItemCollapse.Content = config.RecentItemCollapseThresh;
            sldrMinResumePct.Value = config.MinResumePct;
            sldrMaxResumePct.Value = config.MaxResumePct;

            ddlLoglevel.SelectedItem = config.MinLoggingSeverity;

            //Parental Control
            cbxEnableParentalControl.IsChecked = config.ParentalControlEnabled;
            cbxOptionBlockUnrated.IsChecked = config.ParentalBlockUnrated;
            cbxOptionHideProtected.IsChecked = config.HideParentalDisAllowed;
            cbxOptionAutoUnlock.IsChecked = config.UnlockOnPinEntry;
            gbPCGeneral.IsEnabled = gbPCPIN.IsEnabled = gbPCFolderSecurity.IsEnabled = config.ParentalControlEnabled;
            ddlOptionMaxAllowedRating.SelectedItem = Ratings.ToString(config.MaxParentalLevel);
            slUnlockPeriod.Value = config.ParentalUnlockPeriod;
            txtPCPIN.Password = config.ParentalPIN;

            //supporter key
            tbxSupporterKey.Text = Config.Instance.SupporterKey;

            //logging
            cbxEnableLogging.IsChecked = config.EnableTraceLogging;

            //library validation
            cbxAutoValidate.IsChecked = config.AutoValidate;

            //metadata
            cbxInetProviders.IsChecked = gbTmdb.IsEnabled = config.AllowInternetMetadataProviders;
            cbxSaveMetaLocally.IsChecked = config.SaveLocalMeta;
            cbxDownloadPeople.IsChecked = config.DownloadPeopleImages;
            cbxSaveSeasonBD.IsChecked = config.SaveSeasonBackdrops;
            cbxRefreshImages.IsChecked = config.RefreshItemImages;
            tbxMaxBackdrops.Text = config.MaxBackdrops.ToString();
            tbxMetadataUpdateAge.Text = config.MetadataCheckForUpdateAge.ToString();
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
            // Parental Ratings
            ddlOptionMaxAllowedRating.ItemsSource = ratings.ToStrings();
            //create a set of ratings strings that makes more sense for the folder list
            folderSettings = ratings.ToStrings().Select(r => r != "Any" ? r : "None").ToList();
            ddlFolderRating.ItemsSource = folderSettings;
            //meta
            AllLanguages = GetLanguages(CultureInfo.GetCultures(CultureTypes.NeutralCultures));
            ddlMetadataLanguage.ItemsSource = AllLanguages;
            AllRegions = GetRegions(AllCultures);
            ddlMetadataCountry.ItemsSource = AllRegions;
            ddlMetadataLanguage.SelectedItem = AllLanguages.FirstOrDefault(c => c.LanguageCode == config.PreferredMetaDataLanguage);
            ddlMetadataCountry.SelectedItem = AllRegions.FirstOrDefault(r => r.TwoLetterISORegionName == config.MetadataCountryCode);
            ddlPosterSize.ItemsSource = new List<string>() { "w500", "w342", "w185", "original" };
            ddlPosterSize.SelectedItem = config.FetchedPosterSize;
            ddlBackdropSize.ItemsSource = new List<string>() { "w1280", "w780", "original" };
            ddlBackdropSize.SelectedItem = config.FetchedBackdropSize;
            ddlPersonImageSize.ItemsSource = new List<string>() { "w185", "w45", "h632", "original" };
            ddlPersonImageSize.SelectedItem = config.FetchedProfileSize;


            ddlLoglevel.ItemsSource = Enum.GetValues(typeof(LogSeverity));

        }

        private List<RegionInfo> GetRegions(IEnumerable<CultureInfo> cultures)
        {
            List<RegionInfo> regions = new List<RegionInfo>();
            foreach (var culture in cultures)
            {
                try
                {
                    RegionInfo region = new RegionInfo(culture.LCID);
                    if (!regions.Contains(region))
                    {
                        regions.Add(region);
                    }
                }
                catch { } //some don't have regions
            }
            return regions.OrderBy(i => i.Name).ToList();
        }

        private List<Language> GetLanguages(IEnumerable<CultureInfo> cultures)
        {
            List<Language> languages = new List<Language>();
            foreach (var culture in cultures)
            {
                
                {
                    languages.Add(new Language() {Name = culture.DisplayName, LanguageCode = culture.TwoLetterISOLanguageName});
                }
            }
            return languages;
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

        private void RefreshItems()
        {

            folderList.Items.Clear();

            List<VirtualFolder> vfs = new List<VirtualFolder>();
            int i = 0; //use this to fill in sortorder if not there

            foreach (var filename in Directory.GetFiles(config.InitialFolder))
            {
                try
                {
                    if (filename.ToLowerInvariant().EndsWith(".vf") ||
                        filename.ToLowerInvariant().EndsWith(".lnk"))
                    {
                        //add to our sorted list
                        VirtualFolder vf = new VirtualFolder(filename);
                        if (vf.SortName == null)
                        {
                            //give it a sortorder if its not there
                            vf.SortName = i.ToString("D3");
                            vf.Save();
                        }
                        vfs.Add(vf);
                        i = i + 10;
                    }
                    //else
                    //    throw new ArgumentException("Invalid virtual folder file extension: " + filename);
                }
                catch (ArgumentException)
                {
                    Logger.ReportWarning("Ignored file: " + filename);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Invalid file detected in the initial folder!" + e.ToString());
                    // TODO : alert about dodgy VFs and delete them
                }
            }

            vfs.Sort((a,b) => a.SortName.CompareTo(b.SortName));

            //now add our items in sorted order
            foreach (VirtualFolder v in vfs)
                folderList.Items.Add(v);
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

        #region Media Collection methods

        private void MigrateOldInitialFolder()
        {
            var path = config.InitialFolder;
            if (config.InitialFolder == Helper.MY_VIDEOS)
            {
                path = Helper.MyVideosPath;
            }

            foreach (var file in Directory.GetFiles(path))
            {
                if (file.ToLower().EndsWith(".vf"))
                {
                    File.Copy(file, System.IO.Path.Combine(ApplicationPaths.AppInitialDirPath, System.IO.Path.GetFileName(file)), true);
                }
                else if (file.ToLower().EndsWith(".lnk"))
                {
                    WriteVirtualFolder(Helper.ResolveShortcut(file));
                }
            }

            foreach (var dir in Directory.GetDirectories(path))
            {

                WriteVirtualFolder(dir);
            }
        }

        private void WriteVirtualFolder(string dir)
        {
            int sortorder = 0;
            if (folderList.Items != null)
                sortorder = folderList.Items.Count*10;
            var imagePath = FindImage(dir);
            string vf = string.Format(
@"
folder: {0}
sortorder: {2}
{1}
", dir, imagePath,sortorder.ToString("D3"));

            string name = System.IO.Path.GetFileName(dir);
            // workaround for adding c:\
            if (name.Length == 0) {
                name = dir;
                foreach (var chr in System.IO.Path.GetInvalidFileNameChars()) {
                    name = name.Replace(chr.ToString(), "");
                }
            }
            var destination = System.IO.Path.Combine(ApplicationPaths.AppInitialDirPath, name + ".vf");

     
            for (int i = 1; i < 999; i++) {
                if (!File.Exists(destination)) break;
                destination = System.IO.Path.Combine(ApplicationPaths.AppInitialDirPath, name  + i.ToString() + ".vf");
            }

            File.WriteAllText(destination,
                vf.Trim());
        }

        private void updateFolderSort(int start)
        {
            if (folderList.Items != null && (folderList.Items.Count*10) > start)
            {
                //update the sortorder in the list starting with the specified index (we just removed or moved something)
                for (int i = start; i < folderList.Items.Count*10; i = i + 10)
                {
                    VirtualFolder vf = (VirtualFolder)folderList.Items[i/10];
                    vf.SortName = i.ToString("D3");
                    vf.Save();
                }
            }
        }

        private static string FindImage(string dir)
        {
            string imagePath = "";
            foreach (var file in new string[] { "folder.png", "folder.jpeg", "folder.jpg" })
                if (File.Exists(System.IO.Path.Combine(dir, file)))
                {
                    imagePath = "image: " + System.IO.Path.Combine(dir, file);
                }
            return imagePath;
        }

        #endregion

        #region events
        private void btnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFolderDialog dlg = new BrowseForFolderDialog();

            if (true == dlg.ShowDialog(this))
            {
                WriteVirtualFolder(dlg.SelectedFolder);
                RefreshItems();
                RefreshEntryPoints(false);
            }
        }

        private void btnFolderTree_Click(object sender, RoutedEventArgs e)
        {
            InitFolderTree();
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

        private void btnRename_Click(object sender, RoutedEventArgs e)
        {
            String CurrentName = String.Empty;
            String NewName = String.Empty;
            String CurrentContext = String.Empty;
            String NewContext = String.Empty;

            var virtualFolder = folderList.SelectedItem as VirtualFolder;
            if (virtualFolder != null)
            {
                CurrentName = virtualFolder.Name;
                CurrentContext = virtualFolder.Path;

                var form = new RenameForm(virtualFolder.Name);
                form.Owner = this;
                form.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var result = form.ShowDialog();
                if (result == true)
                {
                    virtualFolder.Name = form.tbxName.Text;
                    NewName = virtualFolder.Name;
                    NewContext = virtualFolder.Path;
                    this.RenameVirtualFolderEntryPoint(CurrentName, NewName, CurrentContext, NewContext);

                    RefreshItems();
                    RefreshEntryPoints(false);

                    foreach (VirtualFolder item in folderList.Items)
                    {
                        if (item.Name == virtualFolder.Name)
                        {
                            folderList.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void RenameVirtualFolderEntryPoint(String OldName, String NewName, String OldContext, String NewContext)
        {
            EntryPointManager epm = null;

            try
            {
                epm = new EntryPointManager();
            }
            catch (Exception ex)
            {
                //Write to error log, don't prompt user.
                Logger.ReportError("Error starting Entry Point Manager in RenameVirtualFolderEntryPoint(). " + ex.Message);
                return;
            }

            try
            {
                epm.RenameEntryPointTitle(OldName, NewName, OldContext, NewContext);
            }
            catch (Exception ex)
            {
                String msg = "Error renaming Entry Points. " + ex.Message;
                Logger.ReportError(msg);
                MessageBox.Show(msg);
            }
        }

        private void btnRemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            var virtualFolder = folderList.SelectedItem as VirtualFolder;
            if (virtualFolder != null)
            {
                int current = folderList.SelectedIndex*10;

                var message = "About to remove the folder \"" + virtualFolder.Name + "\" from the menu.\nAre you sure?";
                if (
                   MessageBox.Show(message, "Remove folder", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
                {

                    File.Delete(virtualFolder.Path);
                    folderList.Items.Remove(virtualFolder);
                    updateFolderSort(current);
                    infoPanel.IsEnabled = false;
                    RefreshEntryPoints(false);
                }
            }
        }

        private void btnChangeImage_Click(object sender, RoutedEventArgs e)
        {
            var virtualFolder = folderList.SelectedItem as VirtualFolder;
            if (virtualFolder == null) return;

            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Title = "Select your image";
            dialog.Filter = "Image files (*.png;*.jpg;)|*.png;*.jpg;";
            dialog.FilterIndex = 1;
            dialog.RestoreDirectory = true;
            //var result = dialog.ShowDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                virtualFolder.ImagePath = dialog.FileName;
                folderImage.Source = new BitmapImage(new Uri(virtualFolder.ImagePath));
            }
        }

        private void btnRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            var virtualFolder = folderList.SelectedItem as VirtualFolder;
            if (virtualFolder == null) return;
            if (MessageBox.Show("Remove association to this image.  Are you sure? \n\n(The image itself will not be deleted)", "Remove Image", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                virtualFolder.ImagePath = "";
                folderImage.Source = null;
            }
        }

        private void btnAddSubFolder_Click(object sender, RoutedEventArgs e)
        {
            var virtualFolder = folderList.SelectedItem as VirtualFolder;
            if (virtualFolder == null) return;

            BrowseForFolderDialog dlg = new BrowseForFolderDialog();
            
            if (true == dlg.ShowDialog(this))
            {
                virtualFolder.AddFolder(dlg.SelectedFolder);
                folderList_SelectionChanged(this, null);
            }
        }

        private void btnRemoveSubFolder_Click(object sender, RoutedEventArgs e)
        {
            var virtualFolder = folderList.SelectedItem as VirtualFolder;
            if (virtualFolder == null) return;

            var path = internalFolder.SelectedItem as string;
            if (path != null)
            {
                var message = "Remove \"" + path + "\"?";
                if (
                  MessageBox.Show(message, "Remove folder", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
                {
                    virtualFolder.RemoveFolder(path);
                    folderList_SelectionChanged(this, null);
                }
            }
        }

        private void folderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            internalFolder.Items.Clear();

            var virtualFolder = folderList.SelectedItem as VirtualFolder;
            if (virtualFolder != null)
            {
                foreach (var folder in virtualFolder.Folders)
                {
                    internalFolder.Items.Add(folder);
                }

                if (!string.IsNullOrEmpty(virtualFolder.ImagePath))
                {
                    if (File.Exists(virtualFolder.ImagePath)) {
                        folderImage.Source = new BitmapImage(new Uri(virtualFolder.ImagePath));
                    }
                }
                else
                {
                    folderImage.Source = null;
                }
                //enable the rename, delete, up and down buttons if a media collection is selected.
                btnRename.IsEnabled = btnRemoveFolder.IsEnabled = true;

                //enable the infoPanel
                infoPanel.IsEnabled = true;
            }
        }

        private void pluginList_DoubleClicked(object sender, RoutedEventArgs e)
        {
            configurePlugin_Click(sender, e);
        }

        private void pluginList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pluginList.SelectedItem != null)
            {
                IPlugin plugin = pluginList.SelectedItem as IPlugin;
                System.Version v = PluginManager.Instance.GetLatestVersion(plugin);
                var latest = PluginManager.Instance.AvailablePlugins.Find(plugin, v);
                System.Version rv = latest != null ? latest.RequiredMBVersion : plugin.RequiredMBVersion;
                System.Version bv = PluginManager.Instance.GetBackedUpVersion(plugin);
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
                IPlugin plugin = pluginList.SelectedItem as IPlugin;
                //get our latest version so we can upgrade...
                IPlugin newPlugin = PluginManager.Instance.AvailablePlugins.Find(plugin, PluginManager.Instance.GetLatestVersion(plugin));
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
                    PluginInstaller p = new PluginInstaller();
                    callBack done = new callBack(UpgradeFinished);
                    this.IsEnabled = false;
                    p.InstallPlugin(newPlugin, progress, this, done);
                    KernelModified = true;
                }
            }
        }

        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            //move the current item up in the list
            VirtualFolder vf = (VirtualFolder)folderList.SelectedItem;
            int current = folderList.SelectedIndex*10;
            if (vf != null && current > 0)
            {
                //remove from current location
                folderList.Items.RemoveAt(current/10);
                //add back above item above us
                folderList.Items.Insert((current/10) - 1, vf);
                //and re-index the items below us
                updateFolderSort(current - 10);
                //finally, re-select this item
                folderList.SelectedItem = vf;
            }
        }

        private void btnDn_Click(object sender, RoutedEventArgs e)
        {
            //move the current item down in the list
            VirtualFolder vf = (VirtualFolder)folderList.SelectedItem;
            int current = folderList.SelectedIndex*10;
            if (vf != null && folderList.SelectedIndex < folderList.Items.Count-1)
            {
                //remove from current location
                folderList.Items.RemoveAt(current/10);
                //add back below item below us
                folderList.Items.Insert((current/10) + 1, vf);
                //and re-index the items below us
                updateFolderSort(current);
                //finally, re-select this item
                folderList.SelectedItem = vf;
            }

        }

        private delegate void callBack();

        public void UpgradeFinished()
        {
            //called when the upgrade process finishes - we just hide progress bar and re-enable
            this.IsEnabled = true;
            IPlugin plugin = pluginList.SelectedItem as IPlugin;
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
            var form = new AddExtenderFormat();
            form.Owner = this;
            form.WindowStartupLocation = WindowStartupLocation.CenterOwner;
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
            dialog.Filter = "*.exe|*.exe";
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
            string title = "Remove External Player Confirmation";

            if (lstExternalPlayers.SelectedItems.Count > 1)
            {
                message = "About to remove the selected external players. Are you sure?";
            }
            else
            {
                var mediaPlayer = lstExternalPlayers.SelectedItem as ConfigData.ExternalPlayer;

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
            var form = new ExternalPlayerForm(isNew);
            form.Owner = this;

            form.WindowStartupLocation = WindowStartupLocation.CenterOwner;

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
            int selectedIndex = lstExternalPlayers.SelectedIndex;
            bool hasSelection = selectedIndex >= 0;
            bool hasMultiSelection = lstExternalPlayers.SelectedItems.Count > 1;

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
        private void cbxOptionBlockUnrated_Click(object sender, RoutedEventArgs e)
        {
            config.ParentalBlockUnrated = (bool)cbxOptionBlockUnrated.IsChecked;
            SaveConfig();
        }
        private void cbxEnableParentalControl_Click(object sender, RoutedEventArgs e)
        {
            //enable/disable other controls on screen
            gbPCGeneral.IsEnabled = gbPCPIN.IsEnabled = gbPCFolderSecurity.IsEnabled = (bool)cbxEnableParentalControl.IsChecked;

            config.ParentalControlEnabled = (bool)cbxEnableParentalControl.IsChecked;
            SaveConfig();

        }

        private void cbxOptionHideProtected_Click(object sender, RoutedEventArgs e)
        {
            config.HideParentalDisAllowed = (bool)cbxOptionHideProtected.IsChecked;
            SaveConfig();
        }
        private void cbxOptionAutoUnlock_Click(object sender, RoutedEventArgs e)
        {
            config.UnlockOnPinEntry = (bool)cbxOptionAutoUnlock.IsChecked;
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

        private void cbxSendStats_Click(object sender, RoutedEventArgs e)
        {
            config.SendStats = (bool)cbxSendStats.IsChecked;
            if (config.SendStats) config.EnableUpdates = true; //need this on too
            SaveConfig();
        }

        private void cbxInetProviders_Checked(object sender, RoutedEventArgs e)
        {
            config.AllowInternetMetadataProviders = gbTmdb.IsEnabled = cbxInetProviders.IsChecked.Value;
            config.Save();

        }

        private void cbxSaveMetaLocally_Checked(object sender, RoutedEventArgs e)
        {
            config.SaveLocalMeta = gbSaveMeta.IsEnabled = cbxSaveMetaLocally.IsChecked.Value;
            config.Save();
        }

        private void cbxDownloadPeople_Checked(object sender, RoutedEventArgs e)
        {
            config.DownloadPeopleImages = cbxDownloadPeople.IsChecked.Value;
            config.Save();
        }

        private void cbxSaveSeasonBD_Checked(object sender, RoutedEventArgs e)
        {
            config.SaveSeasonBackdrops = cbxSaveSeasonBD.IsChecked.Value;
            config.Save();
        }

        private void cbxRefreshImages_Checked(object sender, RoutedEventArgs e)
        {
            config.RefreshItemImages = cbxRefreshImages.IsChecked.Value;
            config.Save();
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
        private void ddlOptionMaxAllowedRating_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ddlOptionMaxAllowedRating.SelectedItem != null)
            {
                config.MaxParentalLevel = Ratings.Level((string)ddlOptionMaxAllowedRating.SelectedItem);
                SaveConfig();
            }
        }

        private void slUnlockPeriod_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {

                if (config != null && slUnlockPeriod != null)
                {
                    config.ParentalUnlockPeriod = (int)slUnlockPeriod.Value;
                    SaveConfig();
                }
            }
            catch (Exception ex){
                Logger.ReportException("Recovered from crash in slUnlockPeriod", ex);
            }
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

        private void hdrBasic_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetHeader(hdrBasic);
            externalPlayersTab.Visibility = extendersTab.Visibility = parentalControlTab.Visibility = helpTab.Visibility = Visibility.Collapsed;
            mediacollectionTab.Visibility = podcastsTab.Visibility = displayTab.Visibility = plugins.Visibility = metadataTab.Visibility = Visibility.Visible;
        }

        private void hdrAdvanced_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetHeader(hdrAdvanced);
            externalPlayersTab.Visibility = displayTab.Visibility = extendersTab.Visibility = metadataTab.Visibility = parentalControlTab.Visibility = Visibility.Visible;
            mediacollectionTab.Visibility = podcastsTab.Visibility = plugins.Visibility = Visibility.Visible;
            helpTab.Visibility = Visibility.Collapsed;
        }

        private void hdrHelpAbout_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetHeader(hdrHelpAbout);
            externalPlayersTab.Visibility = displayTab.Visibility = extendersTab.Visibility = parentalControlTab.Visibility = Visibility.Collapsed;
            mediacollectionTab.Visibility = podcastsTab.Visibility = plugins.Visibility = metadataTab.Visibility = Visibility.Collapsed;
            helpTab.Visibility = Visibility.Visible;
            helpTab.IsSelected = true;
        }

        private void ClearHeaders()
        {
            hdrAdvanced.Foreground = hdrBasic.Foreground = hdrHelpAbout.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Gray);
            hdrAdvanced.FontWeight = hdrBasic.FontWeight = hdrHelpAbout.FontWeight = FontWeights.Normal;
            tabMain.SelectedIndex = 0;
        }
        private void SetHeader(System.Windows.Controls.Label label)
        {
            ClearHeaders();
            label.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
            label.FontWeight = FontWeights.Bold;
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

        private void addPodcast_Click(object sender, RoutedEventArgs e) {
            var form = new AddPodcastForm();
            form.Owner = this;
            var result = form.ShowDialog();
            if (result == true) {
                form.RSSFeed.Save(config.PodcastHome);
                RefreshPodcasts();
                RefreshEntryPoints(false);
            } 

        }

        private void podcastList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            VodCast vodcast = podcastList.SelectedItem as VodCast;
            if (vodcast != null) {
                podcastDetails(true);
                podcastUrl.Text = vodcast.Url;
                podcastName.Content = vodcast.Name;
                podcastDescription.Text = vodcast.Overview;

                //enable the rename and delete buttons if a podcast is selected.
                renamePodcast.IsEnabled = removePodcast.IsEnabled = true;
            }
        }

        private void removePodcast_Click(object sender, RoutedEventArgs e) {
            VodCast vodcast = podcastList.SelectedItem as VodCast;
            if (vodcast != null) {
                var message = "Remove \"" + vodcast.Name + "\"?";
                if (
                  MessageBox.Show(message, "Remove folder", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes) {
                    File.Delete(vodcast.Path);
                    vodcast.Parent.ValidateChildren();
                    podcastDetails(false);
                    RefreshPodcasts();
                    RefreshEntryPoints(false);
                }
            }
        }

        private void renamePodcast_Click(object sender, RoutedEventArgs e) {
            VodCast vodcast = podcastList.SelectedItem as VodCast;
            if (vodcast != null) {
                var form = new RenameForm(vodcast.Name);
                form.Owner = this;
                var result = form.ShowDialog();
                if (result == true) {
                    vodcast.Name = form.tbxName.Text;
                    Kernel.Instance.ItemRepository.SaveItem(vodcast);

                    RefreshPodcasts();

                    foreach (VodCast item in podcastList.Items) {
                        if (item.Name == vodcast.Name) {
                            podcastList.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
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
            AddPluginWindow window = new AddPluginWindow();
            window.Owner = this;
            window.Top = 10;
            window.Left = this.Left + 50;
            if (window.Left + window.Width > SystemParameters.WorkArea.Width) window.Left = SystemParameters.WorkArea.Width - window.Width - 5;
            if (window.Left < 0) window.Left = 5;
            if (SystemParameters.WorkArea.Height - 10 < (window.Height)) window.Height = SystemParameters.WorkArea.Height - 10;
            window.ShowDialog();
            Async.Queue("Refresh after plugin add", () =>
            {
                RefreshEntryPoints(true);
            });
            int current = pluginList.SelectedIndex;
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

        private void podcastDetails(bool display)
        {
            if (display)
            {
                podcastName.Visibility = podcastDescription.Visibility = podcastUrl.Visibility = Visibility.Visible;
            }
            else
            {
                podcastName.Visibility = podcastDescription.Visibility = podcastUrl.Visibility = Visibility.Hidden;
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

                



        private void savePCPIN(object sender, RoutedEventArgs e)
        {
            //first be sure its valid
            if (txtPCPIN.Password.Length != 4)
            {
                MessageBox.Show("PIN Must be EXACTLY FOUR digits.", "Invalid PIN");
                return;
            }
            else try
                {
                    //try and convert to a number - it should convert to an integer
                    int test = Convert.ToInt16(txtPCPIN.Password);
                }
                catch
                {
                    MessageBox.Show("PIN Must be four DIGITS (that can be typed on a remote)", "Invalid PIN");
                    return;
                }
            //appears to be valid - save it
            config.ParentalPIN = txtPCPIN.Password;
            SaveConfig();
        }

        private void tvwLibraryFolders_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem curItem = (TreeViewItem)tvwLibraryFolders.SelectedItem;
            LibraryFolder curFolder = (LibraryFolder)curItem.Header;
            if (curFolder != null)
            {
                ddlFolderRating.IsEnabled = true;
                ddlFolderRating.SelectedItem = curFolder.CustomRating;
                if (!String.IsNullOrEmpty(curFolder.CustomRating))
                    btnDelFolderRating.IsEnabled = true;
                else
                    btnDelFolderRating.IsEnabled = false;

            }
        }

        private void ddlFolderRating_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!tvwLibraryFolders.Items.IsEmpty && ddlFolderRating.SelectedItem != null)
            {
                TreeViewItem curItem = (TreeViewItem)tvwLibraryFolders.SelectedItem;
                if (curItem != null)
                {
                    LibraryFolder curFolder = (LibraryFolder)curItem.Header;
                    if (curFolder != null && ddlFolderRating.SelectedValue != null)
                    {
                        curFolder.CustomRating = ddlFolderRating.SelectedValue.ToString().Replace("Any","None");
                        if (curFolder.CustomRating != null)
                        {
                            curFolder.SaveXML();
                            btnDelFolderRating.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void btnDelFolderRating_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem curItem = (TreeViewItem)tvwLibraryFolders.SelectedItem;
            LibraryFolder curFolder = (LibraryFolder)curItem.Header;
            if (curFolder != null)
            {
                curFolder.CustomRating = null;
                ddlFolderRating.SelectedItem = null;
                curFolder.DeleteXML();
                btnDelFolderRating.IsEnabled = false;
            }
        }

        private void tabControl1_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            // Any SelectionChanged event from any controls contained in the TabControl will bubble up and be handled by this event.
            // We are only interested in events related to the Tab selection changing so ignore everything else.
            if (e.OriginalSource.ToString().Contains("Controls.Tab")) {
                TabControl tabControl = (sender as TabControl);

                if (tabControl.SelectedItem != null) {
                    TabItem tab = (tabControl.SelectedItem as TabItem);
                    if (tab.Name == "parentalControlTab") {
                        // Initialise the Folder list by populating the top level items based on the .vf files
                        InitFolderTree();
                    }
                }
            }
        }

        private void tvwLibraryFolders_ItemExpanded(object sender, RoutedEventArgs e) {
            TreeViewItem item = e.OriginalSource as TreeViewItem;
            if (item != null) {
                if ((item.Items.Count == 1) && (((TreeViewItem)item.Items[0]).Header is DummyTreeItem)) {
                    tvwLibraryFolders.Cursor = Cursors.Wait;
                    item.Items.Clear();

                    LibraryFolder aFolder = item.Header as LibraryFolder;
                    VirtualFolder vf = new VirtualFolder(aFolder.FullPath);

                    Async.Queue("LibraryFoldersExpand", () => {
                        foreach (string folder in vf.Folders) {
                            getLibrarySubDirectories(folder, item);
                        }
                    }, () => {
                        Dispatcher.Invoke(DispatcherPriority.Background, (System.Windows.Forms.MethodInvoker)(() => {
                            tvwLibraryFolders.Cursor = Cursors.Hand;
                        }));
                    });
                    }
                }
        }

        TreeViewItem addLibraryFolderNode(TreeViewItem parent, string dir) {
            if (parent.Dispatcher.CheckAccess()) {

                TreeViewItem aNode = new TreeViewItem();
                LibraryFolder aFolder = new LibraryFolder(dir);
                aNode.Header = aFolder;

                parent.Items.Add(aNode);

                return aNode;
            }
            else {
                parent.Dispatcher.Invoke(new AddLibraryFolderCB(this.addLibraryFolderNode), parent, dir);
                return null;
            }
        }

        private delegate TreeViewItem AddLibraryFolderCB(TreeViewItem parent, string dir);

        private void enableLogging_Click(object sender, RoutedEventArgs e)
        {
            config.EnableTraceLogging = (bool)cbxEnableLogging.IsChecked;
            SaveConfig();
        }


        private void openLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("" + ApplicationPaths.AppLogPath + "");
            }
            catch
            {
                MessageBox.Show("We were unable to open the Logs folder:\n\n" + ApplicationPaths.AppLogPath + "\n\nMake sure the actual folder exists on the local disk.");
            }
        }

        private void btnValidateKey_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.SupporterKey = tbxSupporterKey.Text.Trim();
            //with new store there is no one item we can validate.  Just save the key
            //if (ValidateKey("trailers")) //use trailers because it is the lowest level
            //{
                MessageBox.Show("Supporter key saved.  Thank you for your support.", "Save Key");
            //}
            //else
            //{
            //    MessageBox.Show("Supporter key does not appear to be valid.  Please double check. Copy and paste from the email for best results.", "Supporter Key Invalid");
            //}
        }

        private bool ValidateKey(string feature)
        {
            this.Cursor = Cursors.Wait;
            bool valid = false;
            string path = "http://www.mediabrowser.tv/registration/registrations?feature=" + feature + "&key=" + Config.Instance.SupporterKey;
            WebRequest request = WebRequest.Create(path);

            using (var response = request.GetResponse()) using (Stream stream = response.GetResponseStream())
            {
                byte[] buffer = new byte[5];
                stream.Read(buffer, 0, 5);
                string res = System.Text.Encoding.ASCII.GetString(buffer).Trim();
                valid = res.StartsWith("true");
            }
            this.Cursor = Cursors.Arrow;
            return valid;
        }

        private void sldrMinResumePct_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            lblMinResumePct.Content = ((int)e.NewValue).ToString() + "%";
            config.MinResumePct = (int)e.NewValue;
            SaveConfig();
        }

        private void sldrMaxResumePct_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            lblMaxResumePct.Content = ((int)e.NewValue).ToString() + "%";
            config.MaxResumePct = (int)e.NewValue;
            SaveConfig();
        }

        private void tbxMinResumeDuration_LostFocus(object sender, RoutedEventArgs e)
        {
            Int32.TryParse(tbxMinResumeDuration.Text, out config.MinResumeDuration);
            SaveConfig();
        }

        private void tbxNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Char.IsDigit(e.Text[0]);
            base.OnPreviewTextInput(e);
        }

        private void tbxSSTimeout_PreviewTextInput(object sender, TextCompositionEventArgs e)
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
            IPlugin plugin = pluginList.SelectedItem as IPlugin;
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

        private void tbxMaxBackdrops_LostFocus(object sender, RoutedEventArgs e)
        {
            config.MaxBackdrops = Convert.ToInt32(tbxMaxBackdrops.Text);
            config.Save();
        }

        private void tbxMetadataUpdateAge_LostFocus(object sender, RoutedEventArgs e)
        {
            config.MetadataCheckForUpdateAge = Convert.ToInt32(tbxMetadataUpdateAge.Text);
            config.Save();
        }

        private void ddlMetadataLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var language = ddlMetadataLanguage.SelectedItem as Language;
            if (language != null)
            {
                config.PreferredMetaDataLanguage = language.LanguageCode;
                config.Save();
            }
        }

        private void ddlMetadataCountry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var country = ddlMetadataCountry.SelectedItem as RegionInfo;
            if (country != null)
            {
                config.MetadataCountryCode = country.TwoLetterISORegionName;
                //also need to re-init our ratings
                ratings = new Ratings();
                // and the options
                ddlOptionMaxAllowedRating.ItemsSource = ratings.ToStrings();
                ddlOptionMaxAllowedRating.Items.Refresh();
                ddlOptionMaxAllowedRating.SelectedItem = Ratings.ToString(config.MaxParentalLevel);
                //create a set of ratings strings that makes more sense for the folder list
                folderSettings = ratings.ToStrings().Select(r => r != "Any" ? r : "None").ToList();
                ddlFolderRating.ItemsSource = folderSettings;
                ddlFolderRating.Items.Refresh();
                config.Save();
            }
        }

        private void ddlPosterSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            config.FetchedPosterSize = ddlPosterSize.SelectedItem.ToString();
            config.Save();
        }

        private void ddlBackdropSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            config.FetchedBackdropSize = ddlBackdropSize.SelectedItem.ToString();
            config.Save();
        }

        private void ddlPersonImageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            config.FetchedProfileSize = ddlPersonImageSize.SelectedItem.ToString();
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
