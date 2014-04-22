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
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.Library.Input;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Localization;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.UI;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.System;
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
        private const string versionExtension = "4-22.2";

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

        private static MultiLogger GetDefaultLogger(CommonConfigData config) {
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

        public static void Init(CommonConfigData config) {
           Init(KernelLoadDirective.None, config);
        }


        public static void Init(KernelLoadDirective directives) {
            CommonConfigData config = null;

            config = CommonConfigData.FromFile(ApplicationPaths.CommonConfigFile);
           
            Init(directives, config);
        } 

        public static void Init(KernelLoadDirective directives, CommonConfigData config) {
            lock (sync) {

                // Its critical to have the logger initialized early so initialization 
                //   routines can use the right logger.
                if (Logger.LoggerInstance != null) {
                    Logger.LoggerInstance.Dispose();
                }
                    
                Logger.LoggerInstance = GetDefaultLogger(config);

                AppDomain.CurrentDomain.UnhandledException += CrashHandler;

                // Now try and wake the last server we connected to if set
                if (config.WakeServer && !string.IsNullOrEmpty(config.LastServerMacAddress))
                {
                    Helper.WakeMachine(config.LastServerMacAddress);
                }
                
                var defaultKernel = GetDefaultKernel(config, directives);
                Instance = defaultKernel;

                
            }
        }

        static void CrashHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Logger.ReportException("Unhandled Exception.  Application terminating.", (Exception)args.ExceptionObject);
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
                    if (path != null && path.ToLower().StartsWith("http"))
                    {
                        //We need to re-load IBN general images each time
                        var reAquire = path.IndexOf("mediabrowser/images/general/", StringComparison.OrdinalIgnoreCase) > -1;
                        return new RemoteImage {ReAcquireOnStart = reAquire};
                    }
                    return null;
                },
                (path, canBeProcessed, item) =>
                {
                    if (path != null && path.ToLower().StartsWith("resx:"))
                    {
                        return new ResxImage();
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

 

        public static bool isVista
        {
            get { return false; }
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

        public IEnumerable<RemotePlugin> GetAvailablePlugins()
        {
            foreach (var package in ApiClient.GetPackages())
            {
                foreach (var ver in package.versions.Where(v => new System.Version((string.IsNullOrEmpty(v.requiredVersionStr) ? "3.0" : v.requiredVersionStr)) <= Kernel.Instance.Version
                    && v.classification <= CommonConfigData.PluginUpdateClass))
                {
                    yield return (new RemotePlugin()
                    {
                        Description = package.overview,
                        RichDescURL = package.previewImage,
                        Filename = package.targetFilename,
                        SourceFilename = ver.sourceUrl,
                        Version = ver.version,
                        RequiredMBVersion = new System.Version((string.IsNullOrEmpty(ver.requiredVersionStr) ? "3.0" : ver.requiredVersionStr)),
                        Name = package.name,
                        PluginClass = package.category,
                        UpgradeInfo = ver.description,
                        IsPremium = package.isPremium
                    });
                }
            }
        }

        static IItemRepository GetLocalRepository()
        {
            IItemRepository repository = null;
            if (kernel != null && kernel.MB3ApiRepository != null) kernel.MB3ApiRepository.ShutdownDatabase(); //we need to do this for SQLite
            string sqliteDb = Path.Combine(ApplicationPaths.AppCachePath, "localcache.db");
            string sqliteDll = Path.Combine(ApplicationPaths.AppProgramPath, "system.data.sqlite.dll");
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

        public void RegisterType(string serverTypeName, Type type)
        {
            MB3ApiRepository.AddRegisteredType(serverTypeName, type);
        }

        public static ApiClient ApiClient;
        public static User CurrentUser;
        public static List<UserDto> AvailableUsers; 
        public static bool ServerConnected { get; set; }
        public static SystemInfo ServerInfo { get; set; }
        public static ServerConfiguration ServerConfig { get; set; }
        public static List<PluginInfo> ServerPlugins { get; set; } 
        public string DashboardUrl { get { return ApiClient.ApiUrl + "/dashboard"; } }

        public static bool ConnectToServer(string address, int port, int timeout)
        {
            ApiClient = new ApiClient
            {
                ServerHostName = address,
                ServerApiPort = port,
                DeviceId = ("MBCLASSIC"+Environment.MachineName+Environment.UserName).GetMD5().ToString().Replace("-",""),
                ClientType = "MB-Classic",
                DeviceName = Environment.MachineName+"/"+Environment.UserName,
                Timeout = timeout
            };
            try
            {
                ServerInfo = ApiClient.GetSystemInfo();
                ServerConfig = ApiClient.GetServerConfiguration();
                ServerPlugins = ApiClient.GetServerPlugins().ToList();
            }
            catch (Exception e)
            {
                Logger.ReportException("Unable to connect to server at {0}:{1}", e, address,port);
                return false;
            }
            return (ServerConnected = ServerInfo != null);
        }

        static bool ConnectAutomatically(int timeout)
        {
            var endPoint = new ServerLocator().FindServer();
            return endPoint != null && ConnectToServer(endPoint.Address.ToString(), endPoint.Port, timeout);
        }

        public static bool ConnectToServer(CommonConfigData config)
        {
            var connected = false;

            if (config.FindServerAutomatically)
            {
                connected = ConnectAutomatically(config.HttpTimeout);
            }
            else
            {
                //server specified
                connected = ConnectToServer(config.ServerAddress, config.ServerPort, config.HttpTimeout);
                if (!connected)
                {
                    Logger.ReportWarning("Unable to connect to configured server {0}:{1}. Will try automatic detection", config.ServerAddress, config.ServerPort);
                    connected = ConnectAutomatically(config.HttpTimeout);
                }
            }

            if (connected)
            {
                Logger.ReportInfo("====== Connected to server {0}:{1}", ApiClient.ServerHostName, ApiClient.ServerApiPort);
                AvailableUsers = ApiClient.GetAllUsers().ToList();
                config.LastServerMacAddress = ServerInfo.MacAddress;
                config.ServerPort = ApiClient.ServerApiPort;
                config.Save();
            }

            return connected;
        }

        static Kernel GetDefaultKernel(CommonConfigData config, KernelLoadDirective loadDirective) {

            //Find MB 3 server
            ConnectToServer(config);
            var repository = new MB3ApiRepository();
            var localRepo = GetLocalRepository();

            var kernel = new Kernel()
            {
             PlaybackControllers = new List<BasePlaybackController>(),
             //MetadataProviderFactories = MetadataProviderHelper.DefaultProviders(),
             CommonConfigData = config,
             //ServiceConfigData = ServiceConfigData.FromFile(ApplicationPaths.ServiceConfigFile),
             StringData = LocalizedStrings.Instance,
             ImageResolvers = DefaultImageResolvers(false),
             MB3ApiRepository = repository,
             LocalRepo = localRepo,
             MediaLocationFactory = new MediaLocationFactory(),
             };

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
            kernel.AddConfigPanel(kernel.StringData.GetString("ViewOptionsConfig"), "");
            kernel.AddConfigPanel(kernel.StringData.GetString("ThemesConfig"), "");
            //kernel.AddConfigPanel(kernel.StringData.GetString("ParentalControlConfig"), "");

            //kick off log clean up task if needed
            if (config.LastLogCleanup < DateTime.UtcNow.AddDays(-7))
            {
                Async.Queue("Logfile cleanup", () =>
                {
                    Logger.ReportInfo("Running Logfile clean-up...");
                    var minDateModified = DateTime.UtcNow.AddDays(-(config.LogFileRetentionDays));
                    foreach (var source in new DirectoryInfo(ApplicationPaths.AppLogPath).GetFileSystemInfos("*.log")
                          .Where(f => f.LastWriteTimeUtc < minDateModified))
                    {
                        try
                        {
                            source.Delete();
                        }
                        catch (Exception e)
                        {
                            Logger.ReportException("Error deleting log file {0}",e,source.Name);
                        }                                   
                    }

                    config.LastLogCleanup = DateTime.UtcNow;
                    config.Save();
                });
            }

            return kernel;
        }

        public void LoadUserConfig()
        {
            ConfigData = ConfigData.FromFile(ApplicationPaths.ConfigFile);
        }

        public void LoadPlugins()
        {
            using (new Profiler("Plugin Loading and Init"))
            {
                kernel.Plugins = DefaultPlugins(true);

                // initialize our plugins 
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
        }

        public FavoritesCollectionFolder FavoritesFolder { get; set; }
        public ApiGenreCollectionFolder MovieGenreFolder { get; set; }
        public ApiGenreCollectionFolder MusicGenreFolder { get; set; }
        public ApiAlbumCollectionFolder MusicAlbumFolder { get; set; }
        public Guid FavoriteFolderGuid = new Guid("3D2C3877-4B05-47F4-A231-6C2CF636883F");
        public Guid MovieGenreFolderGuid = new Guid("B01F6DAC-28BA-4EDE-849C-C6716437B2C0");
        public Guid MusicGenreFolderGuid = new Guid("3EBE8C41-289F-40C6-A82C-5621428F9D0F");
        public Guid MusicAlbumFolderGuid = new Guid("6153369B-AC3B-4638-973F-850B625CB8D2");

        public void ReLoadRoot()
        {

            //and re-load the repo
            MB3ApiRepository = new MB3ApiRepository();

            // our root folder needs metadata
            kernel.RootFolder = kernel.MB3ApiRepository.RetrieveRoot();

            //clear image factory cache to free memory
            LibraryImageFactory.Instance.ClearCache();

            if (kernel.RootFolder != null)
            {
                if (ConfigData.ShowFavoritesCollection)
                {
                    //Create Favorites
                    FavoritesFolder = new FavoritesCollectionFolder {Id = FavoriteFolderGuid, DisplayMediaType = "FavoritesFolder"};
                    FavoritesFolder.AddChildren(new List<BaseItem> { new FavoritesTypeFolder(new string[] { "Movie", "Video", "BoxSet", "AdultVideo" }, "Movies"), new FavoritesTypeFolder(new[] { "Series", "Season", "Episode" }, "TV"), new FavoritesTypeFolder(new[] { "Audio", "MusicAlbum", "MusicArtist", "MusicVideo" }, "Music"), new FavoritesTypeFolder(new[] { "Game", "GameConsole" }, "Games"), new FavoritesTypeFolder(new[] { "Book" }, "Books") });
                    kernel.RootFolder.AddVirtualChild(FavoritesFolder);
                }
                
                if (ConfigData.ShowMovieGenreCollection)
                {
                    //Create Genre collection
                    MovieGenreFolder = new ApiGenreCollectionFolder {Id = MovieGenreFolderGuid, Name = ConfigData.MovieGenreFolderName, DisplayMediaType = "MovieGenres", IncludeItemTypes = new[] {"Movie", "BoxSet"}, GenreType = GenreType.Movie};
                    kernel.RootFolder.AddVirtualChild(MovieGenreFolder);
                }
                
                if (ConfigData.ShowMusicGenreCollection)
                {
                    //Create Music Genre collection
                    MusicGenreFolder = new ApiGenreCollectionFolder {Id = MusicGenreFolderGuid, Name = ConfigData.MusicGenreFolderName, DisplayMediaType = "MusicGenres", IncludeItemTypes = ConfigData.GroupAlbumsByArtist ? new[] {"MusicArtist"} : new[] {"MusicAlbum"}, RalIncludeTypes = new[] {"Audio"}, GenreType = GenreType.Music};
                    kernel.RootFolder.AddVirtualChild(MusicGenreFolder);
                }
                
                if (ConfigData.ShowMusicAlbumCollection)
                {
                    //Create Music Album collection
                    MusicAlbumFolder = new ApiAlbumCollectionFolder {Id = MusicAlbumFolderGuid, Name = ConfigData.MusicAlbumFolderName, DisplayMediaType = "MusicAlbums", IncludeItemTypes = new[] {"MusicAlbum"}, RalIncludeTypes = new[] {"Audio"}};
                    kernel.RootFolder.AddVirtualChild(MusicAlbumFolder);
                }
                
            }
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

        /// <summary>
        /// Find an item in the library by Id
        /// If onlyIflLoaded is true, we will only search for items in children that have actually been loaded
        /// Use this parameter when searching for items that have changed on the server so that we don't cause
        /// a complete library load when not necessary.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="onlyIfLoaded"></param>
        /// <returns></returns>
        public BaseItem FindItem(Guid id, bool onlyIfLoaded = true)
        {
            return onlyIfLoaded ? RootFolder.RecursiveLoadedChildren.FirstOrDefault(i => i.Id == id) :
                       RootFolder.RecursiveChildren.FirstOrDefault(i => i.Id == id);
        }

        /// <summary>
        /// Find all occurences of an item in the library by Id
        /// If onlyIflLoaded is true, we will only search for items in children that have actually been loaded
        /// Use this parameter when searching for items that have changed on the server so that we don't cause
        /// a complete library load when not necessary.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="onlyIfLoaded"></param>
        /// <returns></returns>
        public IEnumerable<BaseItem> FindItems(Guid id, bool onlyIfLoaded = true)
        {
            return onlyIfLoaded ? RootFolder.RecursiveLoadedChildren.Where(i => i.Id == id) :
                       RootFolder.RecursiveChildren.Where(i => i.Id == id);
        }

        private static System.Reflection.Assembly _jsonAssembly;
        private static System.Reflection.Assembly _modelAssembly;
        private static System.Reflection.Assembly _websocketAssembly;

        public static System.Reflection.Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            Logger.ReportVerbose("=========System looking for assembly: " + args.Name);
            if (args.Name.StartsWith("MediaBrowser,"))
            {
                Logger.ReportInfo("Plug-in reference to " + args.Name + " is being linked to version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
                return typeof(Kernel).Assembly;
            }
            else
            if (args.Name.StartsWith("MediaBrowser.Model,"))
            {
                Logger.ReportInfo("Resolving " + args.Name + " to " + Path.Combine(ApplicationPaths.AppProgramPath, "MediaBrowser.Model.dll"));
                return _modelAssembly ?? (_modelAssembly = System.Reflection.Assembly.LoadFile(System.IO.Path.Combine(ApplicationPaths.AppProgramPath, "MediaBrowser.Model.dll")));
            }
            else
            if (args.Name.StartsWith("Newtonsoft.Json,"))
            {
                Logger.ReportInfo("Resolving " + args.Name + " to " + Path.Combine(ApplicationPaths.AppProgramPath, "Newtonsoft.Json.dll"));
                return _jsonAssembly ?? (_jsonAssembly = System.Reflection.Assembly.LoadFile(System.IO.Path.Combine(ApplicationPaths.AppProgramPath, "Newtonsoft.Json.dll")));
            }
            else
            if (args.Name.StartsWith("websocket4net,", StringComparison.OrdinalIgnoreCase))
            {
                Logger.ReportInfo("Resolving " + args.Name + " to " + Path.Combine(ApplicationPaths.AppProgramPath, "websocket4net.dll"));
                return _websocketAssembly ?? (_websocketAssembly = System.Reflection.Assembly.LoadFile(System.IO.Path.Combine(ApplicationPaths.AppProgramPath, "websocket4net.dll")));
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
        public CommonConfigData CommonConfigData { get; set; }
        //public ServiceConfigData ServiceConfigData { get; set; }
        public LocalizedStrings StringData { get; set; }
        public MB3ApiRepository MB3ApiRepository { get; set; }
        public IItemRepository ItemRepository { get { return LocalRepo; }}
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
        
        
        //private ParentalControl parentalControls;
        //public ParentalControl ParentalControls
        //{
        //    get
        //    {
        //        if (this.parentalControls == null)
        //            this.parentalControls = new ParentalControl();
        //        return this.parentalControls;
        //    }

        //}
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
            return true;
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
                {"Classic", new ViewTheme()}        
            };

        //method for external entities (plug-ins) to add a new theme - only support replacing detail areas for now...
        public void AddTheme(string name, string pageArea, string detailArea)
        {
            if (AvailableThemes.ContainsKey(name)) AvailableThemes.Remove(name); //clear it if previously was there
            AvailableThemes.Add(name, new ViewTheme(name, pageArea, detailArea));
        }

        public void AddTheme(string name, string pageArea, string detailArea, string msgBox, string progressBox, string yesNoBox)
        {
            if (AvailableThemes.ContainsKey(name)) AvailableThemes.Remove(name); //clear it if previously was there
            AvailableThemes.Add(name, new ViewTheme(name, pageArea, detailArea, msgBox, progressBox, yesNoBox));
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
        public List<MenuItem> UserMenuItems { get { return menuOptions.FindAll(m => (m.Available && m.Supports(MenuType.User))); } }

        public MenuItem AddMenuItem(MenuItem menuItem) {
            menuOptions.Add(menuItem);       
            return menuItem;
        }

        public MenuItem ReplaceMenuItem(MenuItem menuItem)
        {
            var ndx = menuOptions.IndexOf(menuOptions.Find(m => m.Text == menuItem.Text));
            if (ndx < 0)
            {
                menuOptions.Add(menuItem);       
            }
            else
            {
                menuOptions.RemoveAt(ndx);
                menuOptions.Insert(ndx, menuItem);
            }

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

            Logger.ReportInfo("Installing plugin from {0} to {1}", sourcePath, target);

            if (sourcePath.ToLower().StartsWith("http")) {
                // Initialise Async Web Request
                var BUFFER_SIZE = 1024;
                var fileURI = new Uri(sourcePath);

                var request = WebRequest.Create(fileURI);
                var requestState = new Network.WebDownload.State(BUFFER_SIZE, target);
                requestState.request = request;
                requestState.fileURI = fileURI;
                requestState.progCB = updateCB;
                requestState.doneCB = doneCB;
                requestState.errorCB = errorCB;

                var result = (IAsyncResult)request.BeginGetResponse(new AsyncCallback(ResponseCallback), requestState);
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
        /// <param name="media">The item it belongs to. This can be null, but it's used to notify listeners of PlayStateSaved which item it belongs to.</param>
        /// <param name="playstate"></param>
        /// <param name="isPaused"></param>
        public void SavePlayState(BaseItem media, PlaybackStatus playstate, bool isPaused = false)
        {
            Application.CurrentInstance.ReportPlaybackProgress(playstate.Id.ToString(), playstate.PositionTicks, isPaused);
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
