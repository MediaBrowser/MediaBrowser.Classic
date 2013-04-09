using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using MediaBrowser.ApiInteraction;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Events;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.Library.Input;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Localization;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Metadata;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.UI;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Model.Dto;
using MediaBrowser.Util;
using Microsoft.MediaCenter.UI;

namespace MediaBrowser.Library {

    [Flags]
    public enum KernelLoadDirective { 
        None,

        /// <summary>
        /// Ensure plugin dlls are not locked 
        /// </summary>
        ShadowPlugins, 

        LoadServicePlugins
    }

    [Flags]
    public enum MBLoadContext
    {
        None = 0x0,
        Service = 0x1,
        Core = 0x2,
        Other = 0x4,
        Configurator = 0x8,
        All = Service | Core | Other | Configurator
    }

    /// <summary>
    /// This is the one class that contains all the dependencies. 
    /// </summary>
    public class Kernel {

        /**** Version extension is used to provide for specific versions between current releases without having to actually change the 
         * actual assembly version number.  Suggested Values:
         * "R" Released major version
         * "R+" Trunk build (not released as a build to anyone but modified since last true release)
         * "SP1", "SP2", "SPn" Service release without major version change
         * "SPn+" Trunk build after a service release
         * "A1", "A2", "An" Alpha versions
         * "B1", "B2", "Bn" Beta versions
         * 
         * This should be set to "R" (or "SPn") with each official release and then immediately changed back to "R+" (or "SPn+")
         * so future trunk builds will indicate properly.
         * */
        private const string versionExtension = "A4-8.2";

        public const string MBSERVICE_MUTEX_ID = "Global\\{E155D5F4-0DDA-47bb-9392-D407018D24B1}";
        public const string MBCLIENT_MUTEX_ID = "Global\\{9F043CB3-EC8E-41bf-9579-81D5F6E641B9}";

        static object sync = new object();
        static Kernel kernel;

        public static bool IgnoreFileSystemMods = false;

        public bool MajorActivity
        {
            get
            {
                if (Application.CurrentInstance != null)
                    return Application.CurrentInstance.Information.MajorActivity;
                else
                    return false;
            }
            set
            {
                if (Application.CurrentInstance != null)
                if (Application.CurrentInstance.Information.MajorActivity != value)
                    Application.CurrentInstance.Information.MajorActivity = value;
            }
        }

        private static MultiLogger GetDefaultLogger(ConfigData config) {
            var logger = new MultiLogger(config.MinLoggingSeverity);

            if (config.EnableTraceLogging) {
                logger.AddLogger(new FileLogger(ApplicationPaths.AppLogPath));
#if (!DEBUG)
                logger.AddLogger(new TraceLogger());
#endif
            }
#if DEBUG
            logger.AddLogger(new TraceLogger());
#endif
            return logger;
        }

        public static void Init() {
            Init(KernelLoadDirective.None);
        }

        public static void Init(ConfigData config) {
           Init(KernelLoadDirective.None, config);
        }


        public static void Init(KernelLoadDirective directives) {
            ConfigData config = null;

            config = ConfigData.FromFile(ApplicationPaths.ConfigFile);
           
            Init(directives, config);
        } 

        public static void Init(KernelLoadDirective directives, ConfigData config) {
            lock (sync) {

                // we must set up some paths as well as a side effect (should be refactored) 
                if (!string.IsNullOrEmpty(config.UserSettingsPath) && Directory.Exists(config.UserSettingsPath)) {
                    ApplicationPaths.SetUserSettingsPath(config.UserSettingsPath.Trim());
                }

                // Its critical to have the logger initialized early so initialization 
                //   routines can use the right logger.
                if (Logger.LoggerInstance != null) {
                    Logger.LoggerInstance.Dispose();
                }
                    
                Logger.LoggerInstance = GetDefaultLogger(config);
                
                var kernel = GetDefaultKernel(config, directives);
                Kernel.Instance = kernel;

                // setup IBN if not there
                string ibnLocation = ApplicationPaths.AppIBNPath;
                if (!Directory.Exists(ibnLocation))
                {
                    try
                    {
                        Logger.ReportInfo("****Creating IBN...");
                        Directory.CreateDirectory(ibnLocation);
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Genre"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "People"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Studio"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Year"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "General"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "MediaInfo"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Video"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Movie"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Episode"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Series"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Season"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Folder"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\BoxSet"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Actor"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Genre"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Year"));
                        Directory.CreateDirectory(Path.Combine(ibnLocation, "Default\\Studio"));
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Unable to create IBN location.", e);
                    }
                }
                
                //if (LoadContext == MBLoadContext.Core || LoadContext == MBLoadContext.Configurator)
                //{
                //    Async.Queue("Start Service", () =>
                //    {
                //        //start our service if its not already going
                //        if (!MBServiceController.IsRunning)
                //        {
                //            Logger.ReportInfo("Starting MB Service...");
                //            MBServiceController.StartService();
                //        }
                //    });
                //}
                //if (LoadContext == MBLoadContext.Core)
                //{
                //    //listen for commands 
                //    if (!MBClientConnector.StartListening())
                //    { 
                //        //we couldn't start our listener - probably another instance going so we shut down
                //        Logger.ReportInfo("Could not start listener - assuming another instance of MB.  Closing...");
                //        Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.CloseApplication();
                //        return;
                //    }
                //    MBServiceController.ConnectToService(); //set up for service to tell us to do things
                //}

                // create filewatchers for each of our top-level folders (only if we are in MediaCenter, though)
                bool isMC = AppDomain.CurrentDomain.FriendlyName.Contains("ehExtHost");
                //if (isMC && config.EnableDirectoryWatchers) //only do this inside of MediaCenter as we don't want to be trying to refresh things if MB isn't actually running
                //{
                //    Async.Queue("Create Filewatchers", () =>
                //    {
                //        foreach (BaseItem item in kernel.RootFolder.Children)
                //        {
                //            Folder folder = item as Folder;
                //            if (folder != null)
                //            {
                //                folder.directoryWatcher = new MBDirectoryWatcher(folder, false);
                //            }
                //        }

                //        // create a watcher for the startup folder too - and watch all changes there
                //        kernel.RootFolder.directoryWatcher = new MBDirectoryWatcher(kernel.RootFolder, true);
                //    });
                //}


                // add the podcast home
                //var podcastHome = kernel.GetItem<Folder>(kernel.ConfigData.PodcastHome);
                //if (podcastHome != null && podcastHome.Children.Count > 0) {
                //    kernel.RootFolder.AddVirtualChild(podcastHome);
                //}
            }
        }

        private static void DisposeKernel(Kernel kernel)
        {
            if (kernel.PlaybackControllers != null)
            {
                foreach (var playbackController in kernel.PlaybackControllers)
                {
                    var disposable = playbackController as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        #region Item Added/Deleted EventHandler
        volatile EventHandler<GenericEventArgs<BaseItem>> _ItemAddedToLibrary;
        /// <summary>
        /// Fires whenever an item is added via validation
        /// </summary>
        public event EventHandler<GenericEventArgs<BaseItem>> ItemAddedToLibrary
        {
            add
            {
                _ItemAddedToLibrary += value;
            }
            remove
            {
                _ItemAddedToLibrary -= value;
            }
        }

        internal void OnItemAddedToLibrary(BaseItem item)
        {
            if (_ItemAddedToLibrary != null)
            {
                _ItemAddedToLibrary(this, new GenericEventArgs<BaseItem>() { Item = item });
            }
        }
        volatile EventHandler<GenericEventArgs<BaseItem>> _ItemRemovedFromLibrary;
        /// <summary>
        /// Fires whenever an item is removed via validation
        /// </summary>
        public event EventHandler<GenericEventArgs<BaseItem>> ItemRemovedFromLibrary
        {
            add
            {
                _ItemRemovedFromLibrary += value;
            }
            remove
            {
                _ItemRemovedFromLibrary -= value;
            }
        }

        internal void OnItemRemovedFromLibrary(BaseItem item)
        {
            if (_ItemRemovedFromLibrary != null)
            {
                _ItemRemovedFromLibrary(this, new GenericEventArgs<BaseItem>() { Item = item });
            }
        }
        #endregion

        #region PlayStateSaved EventHandler
        volatile EventHandler<PlayStateSaveEventArgs> _PlayStateSaved;
        public event EventHandler<PlayStateSaveEventArgs> PlayStateSaved
        {
            add
            {
                _PlayStateSaved += value;
            }
            remove
            {
                _PlayStateSaved -= value;
            }
        }
        internal void OnPlayStateSaved(BaseItem media, PlaybackStatus playstate)
        {
            // Fire off event in async so we don't tie anything up
            if (_PlayStateSaved != null)
            {
                Async.Queue("OnPlayStateSaved", () =>
                {
                    _PlayStateSaved(this, new PlayStateSaveEventArgs() { PlaybackStatus = playstate, Item = media });
                });
            }
        }
        #endregion


        #region ApplicationInitialized EventHandler
        volatile EventHandler<EventArgs> _ApplicationInitialized;
        /// <summary>
        /// Fires when Application.CurrentInstance is created.
        /// </summary>
        public event EventHandler<EventArgs> ApplicationInitialized
        {
            add
            {
                _ApplicationInitialized += value;
            }
            remove
            {
                _ApplicationInitialized -= value;
            }
        }

        internal void OnApplicationInitialized()
        {
            if (_ApplicationInitialized != null)
            {
                _ApplicationInitialized(this, new EventArgs());
            }
        }
        #endregion
        
        private static string ResolveInitialFolder(string start)
        {
            if (start == Helper.MY_VIDEOS)
                start = Helper.MyVideosPath;
            return start;
        }

        //private static ChainedEntityResolver DefaultResolver(ConfigData config) {
        //    return
        //        new ChainedEntityResolver() { 
        //        new VodCastResolver(),
        //        new EpisodeResolver(), 
        //        new SeasonResolver(), 
        //        new SeriesResolver(),
        //        new BoxSetResolver(),
        //        new MovieResolver(
        //                config.EnableMoviePlaylists?config.PlaylistLimit:1, 
        //                config.EnableNestedMovieFolders, 
        //                config.EnableLocalTrailerSupport), 
        //        new FolderResolver(),
        //    };
        //}

        private static List<ImageResolver> DefaultImageResolvers(bool enableProxyLikeCaching) {
            return new List<ImageResolver>() {
                (path, canBeProcessed, item) =>  { 
                    if (path != null && path.ToLower().StartsWith("http")) {
                        return new RemoteImage();
                    }
                    return null;
                }
            };
        }

        protected static List<System.Reflection.Assembly> PluginAssemblies = new List<System.Reflection.Assembly>();

        static List<IPlugin> DefaultPlugins(bool forceShadow) {
            List<IPlugin> plugins = new List<IPlugin>();
            foreach (var file in Directory.GetFiles(ApplicationPaths.AppPluginPath)) {
                if (file.ToLower().EndsWith(".dll"))
                {
                    try
                    {
                        var plugin = new Plugin(Path.Combine(ApplicationPaths.AppPluginPath, file), forceShadow);
                        plugins.Add(plugin);
                        PluginAssemblies.Add(plugin.PluginAssembly);
                        Logger.ReportVerbose("Added Plugin assembly: " + plugin.PluginAssembly.FullName);
                    }
                    catch (Exception ex)
                    {
                        Debug.Assert(false, "Failed to load plugin: " + ex.ToString());
                        Logger.ReportException("Failed to load plugin", ex);
                    }
                }
            }
            return plugins;
        }

 

        private static bool? _isVista;
        public static bool isVista
        {
            get
            {
                if (_isVista == null)
                {
                    System.Version ver = System.Environment.OSVersion.Version;
                    _isVista = ver.Major == 6 && ver.Minor == 0;
                }
                return _isVista.Value;
            }
        }

        static MBLoadContext? _loadContext;
        public static MBLoadContext LoadContext 
        {
            get 
            {
                if (_loadContext == null)
                {
                    string assemblyName = AppDomain.CurrentDomain.FriendlyName.ToLower();
                    if (assemblyName.Contains("mediabrowserservice"))
                        _loadContext = MBLoadContext.Service;
                    else
                        if (assemblyName.Contains("ehexthost"))
                            _loadContext = MBLoadContext.Core;
                        else
                            if (assemblyName.Contains("configurator"))
                                _loadContext = MBLoadContext.Configurator;
                            else
                                _loadContext = MBLoadContext.Other;
                }
                return _loadContext.Value;
            }
        }

        static IItemRepository GetLocalRepository(ConfigData config)
        {
            IItemRepository repository = null;
            if (kernel != null && kernel.ItemRepository != null) kernel.ItemRepository.ShutdownDatabase(); //we need to do this for SQLite
            string sqliteDb = Path.Combine(ApplicationPaths.AppCachePath, "localcache.db");
            string sqliteDll = Path.Combine(ApplicationPaths.AppConfigPath, "system.data.sqlite.dll");
            if (File.Exists(sqliteDll))
            {
                try
                {
                    repository = new SafeItemRepository(
                        new MemoizingRepository(
                            SqliteItemRepository.GetRepository(sqliteDb, sqliteDll)
                        )
                     );
                }
                catch (Exception e)
                {
                    Logger.ReportException("Failed to init sqlite!", e);
                    repository = null;
                }
            }

            return repository;
        }

        public static ApiClient ApiClient;
        public static User CurrentUser;
        public static List<UserDto> AvailableUsers; 
        public bool ServerConnected { get; set; }

        static Kernel GetDefaultKernel(ConfigData config, KernelLoadDirective loadDirective) {

            // set up assembly resolution hooks, so earlier versions of the plugins resolve properly 
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(OnAssemblyResolve);

            //Find MB 3 server
            var connected = false;
            var endPoint = new ServerLocator().FindServer();
            if (endPoint != null)
            {
                ApiClient = new ApiClient
                {
                    ServerHostName = endPoint.Address.ToString(),
                    ServerApiPort = endPoint.Port,
                    DeviceId = Guid.NewGuid().ToString(),
                    ClientType = "MB-Classic",
                    DeviceName = Environment.MachineName
                };
                connected = true;
                AvailableUsers = ApiClient.GetAllUsers().ToList();
            }

            var repository = new MB3ApiRepository();
            var localRepo = GetLocalRepository(config);

            var kernel = new Kernel()
            {
             PlaybackControllers = new List<BasePlaybackController>(),
             //MetadataProviderFactories = MetadataProviderHelper.DefaultProviders(),
             ConfigData = config,
             ServiceConfigData = ServiceConfigData.FromFile(ApplicationPaths.ServiceConfigFile),
             StringData = LocalizedStrings.Instance,
             ImageResolvers = DefaultImageResolvers(config.EnableProxyLikeCaching),
             ItemRepository = repository,
             ServerConnected = connected,
             LocalRepo = localRepo,
             MediaLocationFactory = new MediaLocationFactory(),
             };

            if (!connected) return kernel;

            //Kernel.UseNewSQLRepo = config.UseNewSQLRepo;

            // kernel.StringData.Save(); //save this in case we made mods (no other routine saves this data)
            if (LoadContext == MBLoadContext.Core)
            {
                kernel.PlaybackControllers.Add(new PlaybackController());
            }
       
            //kernel.EntityResolver = DefaultResolver(kernel.ConfigData);

            //need a blank root in case plug-ins will add virtual items
            kernel.RootFolder = new AggregateFolder {Name = "My Media", Id = new Guid("{F6109BAE-CA26-4746-9EBC-1CD233A7B56F}")};

            //create our default config panels with localized names
            kernel.AddConfigPanel(kernel.StringData.GetString("GeneralConfig"), "");
            kernel.AddConfigPanel(kernel.StringData.GetString("MediaOptionsConfig"), "");
            kernel.AddConfigPanel(kernel.StringData.GetString("ThemesConfig"), "");
            //kernel.AddConfigPanel(kernel.StringData.GetString("ParentalControlConfig"), "");

            using (new Profiler("Plugin Loading and Init"))
            {
                kernel.Plugins = DefaultPlugins((loadDirective & KernelLoadDirective.ShadowPlugins) == KernelLoadDirective.ShadowPlugins);

                // initialize our plugins (maybe we should add a kernel.init ? )
                // The ToList enables us to remove stuff from the list if there is a failure
                foreach (var plugin in kernel.Plugins.ToList())
                {
                    try
                    {
                        //Logger.ReportInfo("LoadContext is: " + LoadContext + " " + plugin.Name + " Initdirective is: " + plugin.InitDirective);
                        if ((LoadContext & plugin.InitDirective) > 0)
                        {
                            plugin.Init(kernel);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Failed to initialize Plugin : " + plugin.Name, e);
                        kernel.Plugins.Remove(plugin);
                    }
                }
            }
            return kernel;
        }

        public void ReLoadRoot()
        {
            //save the items added by plugins before we re-load
            var virtualItems = new List<BaseItem>();
            virtualItems.AddRange(kernel.RootFolder.VirtualChildren);

            //and re-load the repo
            ItemRepository = new MB3ApiRepository();

            // our root folder needs metadata
            kernel.RootFolder = kernel.ItemRepository.RetrieveRoot();

            //now add back the plug-in children
            if (virtualItems.Any() && kernel.RootFolder != null)
            {
                foreach (var item in virtualItems)
                {
                    Logger.ReportVerbose("Adding back " + item.Name);
                    kernel.RootFolder.AddVirtualChild(item);
                }
            }

            //clear image factory cache to free memory
            LibraryImageFactory.Instance.ClearCache();

        }

        public void ReLoadConfig()
        {
            Logger.ReportVerbose("Reloading config file (probably due to change in other process).");
            this.ConfigData = ConfigData.FromFile(ApplicationPaths.ConfigFile);
            Config.Reload();
        }

        public void NotifyConfigChange()
        {
            switch (LoadContext)
            {
                case MBLoadContext.Core:
                case MBLoadContext.Other:
                case MBLoadContext.Configurator:
                    //tell the service to re-load the config
                    //MBServiceController.SendCommandToService(IPCCommands.ReloadConfig);
                    break;
                case MBLoadContext.Service:
                    //tell the core to re-load the config
                    //MBServiceController.SendCommandToCore(IPCCommands.ReloadConfig);
                    break;
            }
        }

        private static System.Reflection.Assembly _jsonAssembly;
        private static System.Reflection.Assembly _protoAssembly;

        static System.Reflection.Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            Logger.ReportVerbose("=========System looking for assembly: " + args.Name);
            if (args.Name.StartsWith("MediaBrowser,"))
            {
                Logger.ReportInfo("Plug-in reference to " + args.Name + " is being linked to version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
                return typeof(Kernel).Assembly;
            }
            else
            if (args.Name.StartsWith("Newtonsoft.Json,"))
            {
                Logger.ReportInfo("Resolving " + args.Name + " to " + Path.Combine(ApplicationPaths.AppConfigPath, "Newtonsoft.Json.dll"));
                return _jsonAssembly ?? (_jsonAssembly = System.Reflection.Assembly.LoadFile(System.IO.Path.Combine(ApplicationPaths.AppConfigPath, "Newtonsoft.Json.dll")));
            }
            else
            if (args.Name.StartsWith("protobuf-net,"))
            {
                Logger.ReportInfo("Resolving " + args.Name + " to " + Path.Combine(ApplicationPaths.AppConfigPath, "protobuf-net.dll"));
                return _protoAssembly ?? (_protoAssembly = System.Reflection.Assembly.LoadFile(System.IO.Path.Combine(ApplicationPaths.AppConfigPath, "protobuf-net.dll")));
            }

            else
            {
                var assembly = PluginAssemblies.Find(a => a.FullName.StartsWith(args.Name+","));
                if (assembly != null)
                {
                    Logger.ReportInfo("Resolving plug-in reference to: " + args.Name);
                    return assembly;
                }
            }
            return null;
        }

        public System.Reflection.Assembly FindPluginAssembly(string name)
        {
            return PluginAssemblies.Find(a => a.FullName.ToLower().Contains(name.ToLower()));
        }


        public static Kernel Instance {
            get {
                if (kernel != null) return kernel;
                lock (sync) {
                    if (kernel != null) return kernel;
                    Init(); 
                }
                return kernel;
            }
            set {
                lock (sync) {
                    kernel = value;
                }
            }
        }

        public System.Version Version
        {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public string VersionStr
        {
            get { return Version + " " + versionExtension; }
        }


        public List<ITrailerProvider> TrailerProviders{ get; set; }
        public AggregateFolder RootFolder { get; set; }
        public List<IPlugin> Plugins { get; set; }
        public List<BasePlaybackController> PlaybackControllers { get; set; }
        public List<MetadataProviderFactory> MetadataProviderFactories { get; set; }
        public List<ImageResolver> ImageResolvers { get; set; }
        public ConfigData ConfigData { get; set; }
        public ServiceConfigData ServiceConfigData { get; set; }
        public LocalizedStrings StringData { get; set; }
        public MB3ApiRepository ItemRepository { get; set; }
        public IItemRepository LocalRepo { get; set; }
        public IMediaLocationFactory MediaLocationFactory { get; set; }
        public delegate System.Drawing.Image ImageProcessorRoutine(System.Drawing.Image image, BaseItem item);
        public ImageProcessorRoutine ImageProcessor;


        IsMouseActiveHooker _mouseActiveHooker;
        public IsMouseActiveHooker MouseActiveHooker
        {
            get 
            {
                lock (this)
                {
                    if (_mouseActiveHooker == null)
                    {
                        _mouseActiveHooker = new IsMouseActiveHooker();
                    }
                    return _mouseActiveHooker;
                }
            }
        }
        
        
        private ParentalControl parentalControls;
        public ParentalControl ParentalControls
        {
            get
            {
                if (this.parentalControls == null)
                    this.parentalControls = new ParentalControl();
                return this.parentalControls;
            }

        }
        public MBPropertySet LocalStrings
        {
            get
            {
                return StringData.LocalStrings;
            }
        }

        public IEnumerable<string> GetTrailers(Movie movie)
        {
            foreach (var trailerProvider in TrailerProviders)
            {
                var trailers = trailerProvider.GetTrailers(movie).ToList();
                if (trailers.Count > 0)
                {
                    return trailers;
                }
            }
            return new List<string>();
        }

        public string GetString(string name)
        {
            return this.StringData.GetString(name);
        }

        public bool ParentalAllowed(Item item)
        {
            return this.ParentalControls.Allowed(item);
        }
        public bool ProtectedFolderAllowed(Folder folder)
        {
            return this.ParentalControls.ProtectedFolderEntered(folder);
        }

        public void ClearProtectedAllowedList()
        {

            this.ParentalControls.ClearEnteredList();
        }
        public Dictionary<string, ConfigPanel> ConfigPanels = new Dictionary<string, ConfigPanel>();

        //method for external entities (plug-ins) to add a new config panels
        //panel should be a resx reference to a UI that fits within the config panel area and takes Application and FocusItem as parms
        public void AddConfigPanel(string name, string panel)
        {
            ConfigPanels.Add(name, new ConfigPanel(panel));
        }

        public void AddConfigPanel(string name, string panel, ModelItem configObject)
        {
            ConfigPanels.Add(name, new ConfigPanel(panel,configObject));
        }

        public Dictionary<string, ViewTheme> AvailableThemes = new Dictionary<string, ViewTheme>()
            {
                {"Default", new ViewTheme()}        
            };

        //method for external entities (plug-ins) to add a new theme - only support replacing detail areas for now...
        public void AddTheme(string name, string pageArea, string detailArea)
        {
            if (AvailableThemes.ContainsKey(name)) AvailableThemes.Remove(name); //clear it if previously was there
            AvailableThemes.Add(name, new ViewTheme(name, pageArea, detailArea));
        }

        public void AddTheme(string name, string pageArea, string detailArea, ModelItem config)
        {
            if (AvailableThemes.ContainsKey(name)) AvailableThemes.Remove(name); //clear it if previously was there
            AvailableThemes.Add(name, new ViewTheme(name, pageArea, detailArea, config));
        }

        //this list tells us which themes have their own icons in resources
        private List<string> themesWithIcons = new List<string>();
        public void AddInternalIconTheme(string theme)
        {
            if (!themesWithIcons.Contains(theme.ToLower())) themesWithIcons.Add(theme.ToLower());
        }
        public bool HasInternalIcons(string theme)
        {
            return themesWithIcons.Contains(theme.ToLower());
        }
        private List<MenuItem> menuOptions = new List<MenuItem>();
        private List<Type> externalPlayableItems = new List<Type>();
        private List<Type> externalPlayableFolders = new List<Type>();

        public string ScreenSaverUI = "";

        public List<Type> ExternalPlayableItems { get { return externalPlayableItems; } }
        public List<Type> ExternalPlayableFolders { get { return externalPlayableFolders; } }

        public List<MenuItem> ContextMenuItems { get { return menuOptions.FindAll(m => (m.Available && m.Supports(MenuType.Item))); } }
        public List<MenuItem> PlayMenuItems { get { return menuOptions.FindAll(m => (m.Available && m.Supports(MenuType.Play))); } }
        public List<MenuItem> DetailMenuItems { get { return menuOptions.FindAll(m => (m.Available && m.Supports(MenuType.Detail))); } }

        public MenuItem AddMenuItem(MenuItem menuItem) {
            menuOptions.Add(menuItem);       
            return menuItem;
        }

        public MenuItem AddMenuItem(MenuItem menuItem, int position)
        {
            Debug.Assert(position <= menuOptions.Count, "cowboy you are trying to insert a menu item in an invalid position!");
            if (position > menuOptions.Count) {
                Logger.ReportWarning("Attempting to insert a menu item in an invalid position, appending to the end instead " + menuItem.Text);
                menuOptions.Add(menuItem);
            } else {
                menuOptions.Insert(position, menuItem);
            }
            return menuItem;
        }

        public void AddExternalPlayableItem(Type aType)
        {
            externalPlayableItems.Add(aType);
        }

        public void AddExternalPlayableFolder(Type aType)
        {
            externalPlayableFolders.Add(aType);
        }

        public T GetLocation<T>(string path) where T : class, IMediaLocation {
            return MediaLocationFactory.Create(path) as T;
        }

        public IMediaLocation GetLocation(string path) {
            return GetLocation<IMediaLocation>(path);
        }

        public LibraryImage GetImage(string path)
        {
            return GetImage(path, false, null);
        }

        public LibraryImage GetImage(string path,bool canBeProcessed, BaseItem item) {
            return LibraryImageFactory.Instance.GetImage(path, canBeProcessed, item);
        }

        public void DeletePlugin(IPlugin plugin) {
            if (!(plugin is Plugin)) {
                Logger.ReportWarning("Attempting to remove a plugin that we have no location for!");
                throw new ApplicationException("Attempting to remove a plugin that we have no location for!");
            }

            (plugin as Plugin).Delete();
            Plugins.Remove(plugin);
        }

        public void InstallPlugin(string path)
        {
            InstallPlugin(path, Path.GetFileName(path), null, null, null);
        }

        public void InstallPlugin(string path,
                MediaBrowser.Library.Network.WebDownload.PluginInstallUpdateCB updateCB,
                MediaBrowser.Library.Network.WebDownload.PluginInstallFinishCB doneCB,
                MediaBrowser.Library.Network.WebDownload.PluginInstallErrorCB errorCB)
        {
            InstallPlugin(path, Path.GetFileName(path), updateCB, doneCB, errorCB);
        }

        public void InstallPlugin(string sourcePath, string targetName,
                MediaBrowser.Library.Network.WebDownload.PluginInstallUpdateCB updateCB,
                MediaBrowser.Library.Network.WebDownload.PluginInstallFinishCB doneCB,
                MediaBrowser.Library.Network.WebDownload.PluginInstallErrorCB errorCB) {
            string target = Path.Combine(ApplicationPaths.AppPluginPath, targetName);

            if (sourcePath.ToLower().StartsWith("http")) {
                // Initialise Async Web Request
                int BUFFER_SIZE = 1024;
                Uri fileURI = new Uri(sourcePath);

                WebRequest request = WebRequest.Create(fileURI);
                Network.WebDownload.State requestState = new Network.WebDownload.State(BUFFER_SIZE, target);
                requestState.request = request;
                requestState.fileURI = fileURI;
                requestState.progCB = updateCB;
                requestState.doneCB = doneCB;
                requestState.errorCB = errorCB;

                IAsyncResult result = (IAsyncResult)request.BeginGetResponse(new AsyncCallback(ResponseCallback), requestState);
            }
            else {
                File.Copy(sourcePath, target, true);
                InitialisePlugin(target);
            }

            // Moved code to InitialisePlugin()
            //Function needs to be called at end of Async dl process as well
        }

        private void InitialisePlugin(string target) {
            var plugin = Plugin.FromFile(target, true);

            try {
                plugin.Init(this);
            } catch (InvalidCastException e) { 
                // this happens if the assembly with the exact same version is loaded 
                // AND the Init process tries to use types defined in its assembly 
                throw new PluginAlreadyLoadedException("Failed to init plugin as its already loaded", e);
            }
            IPlugin pi = Plugins.Find(p => p.Filename == plugin.Filename);
            if (pi != null) Plugins.Remove(pi); //we were updating
            Plugins.Add(plugin);
           

        }

        /// <summary>
        /// Main response callback, invoked once we have first Response packet from
        /// server.  This is where we initiate the actual file transfer, reading from
        /// a stream.
        /// </summary>
        /// <param name="asyncResult"></param>
        private void ResponseCallback(IAsyncResult asyncResult) {
            Network.WebDownload.State requestState = ((Network.WebDownload.State)(asyncResult.AsyncState));

            try {
                WebRequest req = requestState.request;

                // HTTP 
                if (requestState.fileURI.Scheme == Uri.UriSchemeHttp) {
                    HttpWebResponse resp = ((HttpWebResponse)(req.EndGetResponse(asyncResult)));
                    requestState.response = resp;
                    requestState.totalBytes = requestState.response.ContentLength;
                }
                else {
                    throw new ApplicationException("Unexpected URI");
                }

                // Set up a stream, for reading response data into it
                Stream responseStream = requestState.response.GetResponseStream();
                requestState.streamResponse = responseStream;

                // Begin reading contents of the response data
                IAsyncResult ar = responseStream.BeginRead(requestState.bufferRead, 0, requestState.bufferRead.Length, new AsyncCallback(ReadCallback), requestState);

                return;
            }
            catch (WebException ex) {
                //Callback to GUI to report an error has occured.
                if (requestState.errorCB != null) {
                    requestState.errorCB(ex);
                }
            }
        }

        /// <summary>
        /// Main callback invoked in response to the Stream.BeginRead method, when we have some data.
        /// </summary>
        private void ReadCallback(IAsyncResult asyncResult) {
            Network.WebDownload.State requestState = ((Network.WebDownload.State)(asyncResult.AsyncState));

            try {
                Stream responseStream = requestState.streamResponse;

                // Get results of read operation
                int bytesRead = responseStream.EndRead(asyncResult);

                // Got some data, need to read more
                if (bytesRead > 0) {
                    // Save Data
                    requestState.downloadDest.Write(requestState.bufferRead, 0, bytesRead);

                    // Report some progress, including total # bytes read, % complete, and transfer rate
                    requestState.bytesRead += bytesRead;
                    double percentComplete = ((double)requestState.bytesRead / (double)requestState.totalBytes) * 100.0f;

                    //Callback to GUI to update progress
                    if (requestState.progCB != null) {
                        requestState.progCB(percentComplete);
                    }

                    // Kick off another read
                    IAsyncResult ar = responseStream.BeginRead(requestState.bufferRead, 0, requestState.bufferRead.Length, new AsyncCallback(ReadCallback), requestState);
                    return;
                }

                // EndRead returned 0, so no more data to be read
                else {
                    responseStream.Close();
                    requestState.response.Close();
                    requestState.downloadDest.Flush();
                    requestState.downloadDest.Close();

                    // Initialise the Plugin
                    try
                    {
                        InitialisePlugin(requestState.downloadDest.Name);
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Failed to initialize plugin.", e);
                    }

                    //Callback to GUI to report download has completed
                    if (requestState.doneCB != null) {
                        requestState.doneCB();
                    }
                }
            } 
            catch (PluginAlreadyLoadedException) {
                Logger.ReportWarning("Attempting to install a plugin that is already loaded: " + requestState.fileURI);
            } 
            catch (WebException ex) {
                //Callback to GUI to report an error has occured.
                if (requestState.errorCB != null) {
                    requestState.errorCB(ex);
                }
            }
        }

        /// <summary>
        /// Persists a PlaybackStatus object
        /// </summary>
        public void SavePlayState(PlaybackStatus playstate)
        {
            SavePlayState(null, playstate);
        }

        /// <summary>
        /// Persists a PlaybackStatus object
        /// </summary>
        /// <param name="media">The item it belongs to. This can be null, but it's used to notify listeners of PlayStateSaved which item it belongs to.</param>
        public void SavePlayState(BaseItem media, PlaybackStatus playstate)
        {
            Kernel.Instance.ItemRepository.SavePlayState(playstate);
            OnPlayStateSaved(media, playstate);
        }

    }

    [global::System.Serializable]
    public class PluginAlreadyLoadedException : Exception {
        public PluginAlreadyLoadedException() { }
        public PluginAlreadyLoadedException(string message) : base(message) { }
        public PluginAlreadyLoadedException(string message, Exception inner) : base(message, inner) { }
        protected PluginAlreadyLoadedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
