using System.Net;
using System.Text;
using System.Web;
using MediaBrowser.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Updates;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DisplayPreferences = MediaBrowser.Model.Entities.DisplayPreferences;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Provides api methods centered around an HttpClient
    /// </summary>
    public class ApiClient : BaseApiClient
    {
        /// <summary>
        /// Gets the HTTP client.
        /// </summary>
        /// <value>The HTTP client.</value>
        protected IHttpClient HttpClient { get; private set; }

        public SessionInfoDto CurrentSessionInfo { get; set; } 

        public int Timeout
        {
            set { HttpClient.Timeout = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClient">The HTTP client.</param>
        /// <exception cref="System.ArgumentNullException">httpClient</exception>
        public ApiClient(ILogger logger, IHttpClient httpClient)
            : base(logger)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            HttpClient = httpClient;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public ApiClient(ILogger logger)
            : this(logger, new MbHttpClient(logger))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient" /> class.
        /// </summary>
        public ApiClient()
            : this(new NullLogger(), new MbHttpClient(new NullLogger()))
        {
        }

        /// <summary>
        /// Sets the authorization header.
        /// </summary>
        /// <param name="header">The header.</param>
        protected override void SetAuthorizationHeader(string header)
        {
            HttpClient.SetAuthorizationHeader(header);
        }

        /// <summary>
        /// Gets an image stream based on a url
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>Task{Stream}.</returns>
        /// <exception cref="System.ArgumentNullException">url</exception>
        public Stream GetImageStream(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException("url");
            }

            return HttpClient.Get(url);
        }

        /// <summary>
        /// Gets a BaseItem
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public BaseItemDto GetItem(string id)
        {
            return this.GetItem(id, Kernel.CurrentUser.Id);
        }

        /// <summary>
        /// Gets a BaseItem
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public BaseItemDto GetItem(string id, Guid userId)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + id);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets the intros async.
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{System.String[]}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public ItemsResult GetIntros(string itemId, Guid userId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/Intros");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets a BaseItem
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public BaseItemDto GetRootFolder(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/Root");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets Item Counts
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public ItemCounts GetItemCounts(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary {{"userId", userId.ToString()}};
            var url = GetApiUrl("Items/Counts/", dict);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemCounts>(stream);
            }
        }

        /// <summary>
        /// Gets DisplayPrefs
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="prefsId">The prefs id</param>
        /// <returns>DisplayPreferences.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public DisplayPreferences GetDisplayPrefs(string prefsId)
        {
            if (string.IsNullOrEmpty(prefsId))
            {
                throw new ArgumentNullException("prefsId");
            }

            var dict = new QueryStringDictionary();

            dict.AddIfNotNullOrEmpty("userId", Kernel.CurrentUser.ApiId);
            dict.Add("client", "MBC");

            var url = GetApiUrl("DisplayPreferences/" + prefsId, dict);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<DisplayPreferences>(stream);
            }
        }

        /// <summary>
        /// Gets all Users
        /// </summary>
        /// <returns>Task{UserDto[]}.</returns>
        public UserDto[] GetAllUsers()
        {
            var dict = new QueryStringDictionary();

            dict.AddIfNotNull("isDisabled", false);

            var url = GetApiUrl("Users/Public", dict);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<UserDto[]>(stream);
            }
        }

        /// <summary>
        /// Gets all available MediaInfo Images
        /// </summary>
        /// <returns>Task{UserDto[]}.</returns>
        public ImageByNameInfo[] GetMediaInfoImages()
        {

            var url = GetApiUrl("Images/MediaInfo");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ImageByNameInfo[]>(stream);
            }
        }

        /// <summary>
        /// Gets all available General IBN Images
        /// </summary>
        /// <returns>Task{UserDto[]}.</returns>
        public ImageByNameInfo[] GetGeneralIbnImages()
        {

            var url = GetApiUrl("Images/General");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ImageByNameInfo[]>(stream);
            }
        }

        /// <summary>
        /// Queries for items
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetItems(ItemQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetItemListUrl(query);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Queries for channels
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetChannels(string userId)
        {
            if (userId == null)
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("/Channels", new QueryStringDictionary {{"userid",userId}});

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Queries for channel items
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="channelId"></param>
        /// <param name="folderId"></param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetChannelItems(string userId, string channelId, string folderId)
        {
            if (userId == null)
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary {{"userid", userId}};
            dict.AddIfNotNullOrEmpty("folderid", folderId);

            var url = GetApiUrl("/Channels/"+channelId+"/Items", dict);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Queries for additional parts
        /// </summary>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetAdditionalParts(string userId, string itemId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            var parms = new QueryStringDictionary {{"userId", userId}};
            var url = GetApiUrl("Videos/" + itemId + "/AdditionalParts", parms);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Queries for genres
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetGenres(ItemQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetGenreListUrl(query);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Queries for people
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetPersons(PersonsQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetItemByNameListUrl("Persons", query);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Queries for music genres
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetMusicGenres(ItemQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetMusicGenreListUrl(query);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Queries for IBN Items
        /// </summary>
        /// <param name="ibnName"></param>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetIbnItems(string ibnName, ItemsByNameQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetItemByNameListUrl(ibnName, query);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }


        /// <summary>
        /// Queries for music artists
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetArtists(ItemsByNameQuery query)
        {
            return GetIbnItems("Artists", query);
        }

        /// <summary>
        /// Gets all People
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public ItemsResult GetAllPeople(ItemsByNameQuery query)
        {
            if (string.IsNullOrEmpty(query.UserId))
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary();

            dict.AddIfNotNull("startIndex", query.StartIndex);
            dict.AddIfNotNull("limit", query.Limit);

            dict.Add("recursive", query.Recursive);

            if (query.SortOrder.HasValue)
            {
                dict["sortOrder"] = query.SortOrder.Value.ToString();
            }
            if (query.Fields != null)
            {
                dict.Add("fields", query.Fields.Select(f => f.ToString()));
            }

            var url = string.IsNullOrEmpty(query.ParentId) ? "Users/" + query.UserId + "/Items/Root/Persons" : "Users/" + query.UserId + "/Items/" + query.ParentId + "/Persons";
            url = GetApiUrl(url, dict);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets a studio
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public BaseItemDto GetStudio(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("Studios/" + name);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets a genre
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public BaseItemDto GetGenre(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("Genres/" + HttpUtility.UrlEncode(name));

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets a music genre
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public BaseItemDto GetMusicGenre(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("MusicGenres/" + HttpUtility.UrlEncode(name));

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets the similar movies async.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public ItemsResult GetSimilarItems(SimilarItemsQuery query, string type)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetSimilarItemListUrl(query, type);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Restarts the kernel or the entire server if necessary
        /// If the server application is restarting this request will fail to return, even if
        /// the operation is successful.
        /// </summary>
        /// <returns>Task.</returns>
        public void PerformPendingRestart()
        {
            var url = GetApiUrl("System/Restart");

            Post<EmptyRequestResult>(url, new QueryStringDictionary());
        }

        /// <summary>
        /// Start a library scan on the server
        /// </summary>
        /// <returns>Task.</returns>
        public void StartLibraryScan()
        {
            var url = GetApiUrl("Library/Refresh");

            Post<EmptyRequestResult>(url, new QueryStringDictionary());
        }

        /// <summary>
        /// Report our remote control Capabilities
        /// </summary>
        /// <returns>Task.</returns>
        public void ReportRemoteCapabilities()
        {
            var url = GetApiUrl("Sessions/Capabilities");
            var parms = new QueryStringDictionary();
            parms.Add("PlayableMediaTypes","Video,Audio,Photo");
            parms.Add("SupportedCommands", "GoHome,Back,DisplayContent,DisplayMessage,GotoSettings,Mute,UnMute,ToggleMute");

            Post<EmptyRequestResult>(url, parms);
        }

        /// <summary>
        /// Refresh an item on the server
        /// </summary>
        /// <returns>Task.</returns>
        public void RefreshItem(string id, bool forced = true, bool recursive = true)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            var options = new QueryStringDictionary();
            options.AddIfNotNull("Forced", forced);
            options.AddIfNotNull("Recursive", recursive);

            var url = GetApiUrl("Items/"+id+"/Refresh");

            Post<EmptyRequestResult>(url, options);
        }

        /// <summary>
        /// Gets the system status async.
        /// </summary>
        /// <returns>Task{SystemInfo}.</returns>
        public SystemInfo GetSystemInfo()
        {
            var url = GetApiUrl("System/Info");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<SystemInfo>(stream);
            }
        }

        /// <summary>
        /// Gets the system configuration file.
        /// </summary>
        /// <returns>Task{SystemInfo}.</returns>
        public ServerConfiguration GetServerConfiguration()
        {
            var url = GetApiUrl("System/Configuration");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<ServerConfiguration>(stream);
            }
        }

        /// <summary>
        /// Gets the system configuration file.
        /// </summary>
        /// <returns>Task{SystemInfo}.</returns>
        public PluginInfo[] GetServerPlugins()
        {
            var url = GetApiUrl("Plugins");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<PluginInfo[]>(stream);
            }
        }

        /// <summary>
        /// Gets information on the given package
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public PackageInfo GetPackageInfo(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("Packages/" + HttpUtility.UrlEncode(name));

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<PackageInfo>(stream);
            }

        }

        /// <summary>
        /// Gets a list of available packages meeting the supplied criteria
        /// </summary>
        /// <param name="name"></param>
        /// <param name="packageType"></param>
        /// <param name="targetSystem"></param>
        /// <param name="isPremium"></param>
        /// <returns></returns>
        public List<PackageInfo> GetPackages(string packageType = "UserInstalled", string targetSystem = "MBClassic", bool? isPremium = null)
        {
            var dict = new QueryStringDictionary()
                           {
                               {"packagetype", packageType},
                               {"targetsystems", targetSystem},

                           };

            dict.AddIfNotNull("ispremium",isPremium);
            
            var url = GetApiUrl("Packages", dict);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<List<PackageInfo>>(stream);
            }

        }

        /// <summary>
        /// Test a particular url for an error (like if it exists)
        /// </summary>
        /// <param name="url"></param>
        /// <returns>true if no error false otherwise</returns>
        public bool TestUrl(string url)
        {
            try
            {
                HttpClient.Get(url);
                Logger.Debug("Tested url {0}", url);
                return true;
            }
            catch (WebException)
            {
                Logger.Debug("Url Test failed {0}", url);
                return false;
            }
        }

        /// <summary>
        /// Gets a person
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public BaseItemDto GetPerson(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("Persons/" + name);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets a music artist
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public BaseItemDto GetArtist(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("Artists/" + name);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets a year
        /// </summary>
        /// <param name="year">The year.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public BaseItemDto GetYear(int year)
        {
            var url = GetApiUrl("Years/" + year);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets the scheduled tasks.
        /// </summary>
        /// <returns>Task{TaskInfo[]}.</returns>
        public TaskInfo[] GetScheduledTasks()
        {
            var url = GetApiUrl("ScheduledTasks");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<TaskInfo[]>(stream);
            }
        }

        /// <summary>
        /// Gets the scheduled task async.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>Task{TaskInfo}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public TaskInfo GetScheduledTask(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            var url = GetApiUrl("ScheduledTasks/" + id);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<TaskInfo>(stream);
            }
        }

        /// <summary>
        /// Gets the plugin configuration file in plain text.
        /// </summary>
        /// <param name="pluginId">The plugin id.</param>
        /// <returns>Task{Stream}.</returns>
        /// <exception cref="System.ArgumentNullException">assemblyFileName</exception>
        public T GetPluginConfiguration<T>(Guid pluginId) where T : class 
        {
            if (pluginId == Guid.Empty)
            {
                throw new ArgumentNullException("pluginId");
            }

            var url = GetApiUrl("Plugins/" + pluginId + "/Configuration");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<T>(stream);
            }
        }

        /// <summary>
        /// Gets a user by id
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>Task{UserDto}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public UserDto GetUser(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            var url = GetApiUrl("Users/" + id);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<UserDto>(stream);
            }
        }

        /// <summary>
        /// Gets the parental ratings async.
        /// </summary>
        /// <returns>Task{List{ParentalRating}}.</returns>
        public List<ParentalRating> GetParentalRatings()
        {
            var url = GetApiUrl("Localization/ParentalRatings");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<List<ParentalRating>>(stream);
            }
        }

        ///// <summary>
        ///// Gets weather information for the default location as set in configuration
        ///// </summary>
        ///// <returns>Task{WeatherInfo}.</returns>
        //public WeatherInfo GetWeatherInfo()
        //{
        //    var url = GetApiUrl("Weather");

        //    using (var stream = GetSerializedStream(url))
        //    {
        //        return DeserializeFromStream<WeatherInfo>(stream);
        //    }
        //}

        ///// <summary>
        ///// Gets weather information for a specific location
        ///// Location can be a US zipcode, or "city,state", "city,state,country", "city,country"
        ///// It can also be an ip address, or "latitude,longitude"
        ///// </summary>
        ///// <param name="location">The location.</param>
        ///// <returns>Task{WeatherInfo}.</returns>
        ///// <exception cref="System.ArgumentNullException">location</exception>
        //public WeatherInfo GetWeatherInfo(string location)
        //{
        //    if (string.IsNullOrEmpty(location))
        //    {
        //        throw new ArgumentNullException("location");
        //    }

        //    var dict = new QueryStringDictionary();

        //    dict.Add("location", location);

        //    var url = GetApiUrl("Weather", dict);

        //    using (var stream = GetSerializedStream(url))
        //    {
        //        return DeserializeFromStream<WeatherInfo>(stream);
        //    }
        //}

        /// <summary>
        /// Gets registration status for a specific feature
        /// with an MB2 equivalent
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="Mb2Equivalent"></param>
        /// <returns>MBRegistrationRecord.</returns>
        /// <exception cref="System.ArgumentNullException">feature</exception>
        public MBRegistrationRecord GetRegistrationStatus(string feature, string Mb2Equivalent = null)
        {
            if (string.IsNullOrEmpty(feature))
            {
                throw new ArgumentNullException("feature");
            }

            var dict = new QueryStringDictionary();

            if (Mb2Equivalent != null) dict.Add("Mb2Equivalent", Mb2Equivalent);

            var url = GetApiUrl("Plugins/RegistrationRecords/"+ HttpUtility.UrlEncode(feature), dict);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<MBRegistrationRecord>(stream);
            }
        }

        /// <summary>
        /// Gets local trailers for an item
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="itemId">The item id.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public BaseItemDto[] GetLocalTrailers(Guid userId, string itemId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/LocalTrailers");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto[]>(stream);
            }
        }

        /// <summary>
        /// Gets special features for an item
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="itemId">The item id.</param>
        /// <returns>Task{BaseItemDto[]}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public BaseItemDto[] GetSpecialFeatures(Guid userId, string itemId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/SpecialFeatures");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto[]>(stream);
            }
        }

        /// <summary>
        /// Gets the cultures async.
        /// </summary>
        /// <returns>Task{CultureDto[]}.</returns>
        public CultureDto[] GetCultures()
        {
            var url = GetApiUrl("Localization/Cultures");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<CultureDto[]>(stream);
            }
        }

        /// <summary>
        /// Gets the countries async.
        /// </summary>
        /// <returns>Task{CountryInfo[]}.</returns>
        public CountryInfo[] GetCountries()
        {
            var url = GetApiUrl("Localization/Countries");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<CountryInfo[]>(stream);
            }
        }

        /// <summary>
        /// Marks an item as played or unplayed.
        /// This should not be used to update playstate following playback.
        /// There are separate playstate check-in methods for that. This should be used for a
        /// separate option to reset playstate.
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="wasPlayed">if set to <c>true</c> [was played].</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public void UpdatePlayedStatus(string itemId, Guid userId, bool wasPlayed)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/PlayedItems/" + itemId);

            if (wasPlayed)
            {
                Post<EmptyRequestResult>(url, new Dictionary<string, string>());
            }
            else
            {
                HttpClient.Delete(url);
                
            }
        }

        /// <summary>
        /// Updates the favorite status async.
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="isFavorite">if set to <c>true</c> [is favorite].</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public void UpdateFavoriteStatus(string itemId, Guid userId, bool isFavorite)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/FavoriteItems/" + itemId);

            if (isFavorite)
            {
                Post<EmptyRequestResult>(url, new Dictionary<string, string>());
            }
            else
            {
                HttpClient.Delete(url);
                
            }
        }

        /// <summary>
        /// Sends a delete command to the specified url
        /// </summary>
        /// <param name="url"></param>
        public void Delete(string url)
        {
            HttpClient.Delete(url);
        }

        /// <summary>
        /// Reports to the server that the user has begun playing an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="playMethod"></param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public void ReportPlaybackStart(string itemId, Guid userId, string playMethod = "DirectPlay")
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary();
            dict.Add("CanSeek", true);
            dict.Add("IsPaused", false);
            dict.Add("IsMuted", false);
            dict.Add("ItemId", itemId);
            dict.Add("QueueableMediaTypes", "");
            dict.Add("PlayMethod", playMethod);

            var url = GetApiUrl("Sessions/Playing", dict);

            Post<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Submit a rating for a package
        /// </summary>
        public void RatePackage(int packageId, int rating, bool recommend)
        {

            var dict = new QueryStringDictionary();
            dict.Add("Rating", rating);
            dict.Add("Recommend", recommend);

            var url = GetApiUrl("PackageReviews/" + packageId, dict);

            Post<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Reports playback progress to the server
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="positionTicks">The position ticks.</param>
        /// <param name="isPaused"></param>
        /// <param name="isMuted"></param>
        /// <param name="playMethod"></param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public void ReportPlaybackProgress(string itemId, Guid userId, long? positionTicks, bool isPaused, bool isMuted, string playMethod = "DirectPlay")
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary();
            dict.AddIfNotNull("positionTicks", positionTicks);
            dict.AddIfNotNull("isPaused", isPaused);
            dict.AddIfNotNull("isMuted", isMuted);
            dict.AddIfNotNullOrEmpty("ItemId", itemId);
            dict.AddIfNotNullOrEmpty("PlayMethod", playMethod);

            var url = GetApiUrl("Sessions/Playing/Progress", dict);

            Post<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Reports to the server that the user has stopped playing an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="positionTicks">The position ticks.</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public Library.Entities.PlaybackStatus ReportPlaybackStopped(string itemId, Guid userId, long? positionTicks)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary();
            dict.AddIfNotNull("positionTicks", positionTicks);
            dict.AddIfNotNullOrEmpty("ItemId", itemId);

            var url = GetApiUrl("Sessions/Playing/Stopped", dict);

            Post<EmptyRequestResult>(url, new Dictionary<string, string>());

            //Now we have to get the updated playstate from the server.  The only way to do this now is re-retrieve the whole item and grab the playstate
            var updated = Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(itemId)) as Library.Entities.Media;
            return updated != null ? updated.PlaybackStatus : null;
        }

        /// <summary>
        /// Clears a user's rating for an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public void ClearUserItemRating(string itemId, Guid userId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/Rating");

            HttpClient.Delete(url);
        }

        /// <summary>
        /// Delete an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public void DeleteItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            var url = GetApiUrl("Items/" + itemId);

            HttpClient.Delete(url);
        }

        /// <summary>
        /// Updates a user's rating for an item, based on likes or dislikes
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="likes">if set to <c>true</c> [likes].</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public UserItemDataDto UpdateUserItemRating(string itemId, Guid userId, bool likes)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary { };

            dict.Add("likes", likes);

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/Rating", dict);

            return Post<UserItemDataDto>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Authenticates a user and returns the result
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="sha1Hash">The sha1 hash.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public void AuthenticateUser(string userId, byte[] sha1Hash)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var password = BitConverter.ToString(sha1Hash).Replace("-", string.Empty);
            var url = GetApiUrl("Users/" + userId + "/Authenticate");

            var args = new Dictionary<string, string>();

            args["password"] = password;

            var result = Post<AuthenticationResult>(url, args);
            CurrentSessionInfo = result.SessionInfo;
        }

        /// <summary>
        /// Add user to the current session
        /// </summary>
        /// <param name="userId">The user id.</param>
        public void AddUserToSession(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Sessions/" + CurrentSessionInfo.Id + "/Users/" + userId);

            Post<EmptyRequestResult>(url, new Dictionary<string, string>());
            
        }

        /// <summary>
        /// Add user to the current session
        /// </summary>
        /// <param name="userId">The user id.</param>
        public void RemoveUserFromSession(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Sessions/" + CurrentSessionInfo.Id + "/Users/" + userId);

            HttpClient.Delete(url);
            
        }

        /// <summary>
        /// Authenticates a user and returns the result
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="sha1Hash">The sha1 hash.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public void AuthenticateUserWithHash(Guid userId, string sha1Hash)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var password = sha1Hash.Replace("-", string.Empty);
            var url = GetApiUrl("Users/" + userId + "/Authenticate");

            var args = new Dictionary<string, string>();

            args["password"] = password;

            var result = Post<AuthenticationResult>(url, args);
            CurrentSessionInfo = result.SessionInfo;
        }

        /// <summary>
        /// Uploads the user image async.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="imageType">Type of the image.</param>
        /// <param name="image">The image.</param>
        /// <returns>Task{RequestResult}.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public void UploadUserImage(Guid userId, ImageType imageType, Stream image)
        {
            // Implement when needed
            throw new NotImplementedException();
        }

        /// <summary>
        /// Updates the scheduled task triggers.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="triggers">The triggers.</param>
        /// <returns>Task{RequestResult}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public void UpdateScheduledTaskTriggers(Guid id, TaskTriggerInfo[] triggers)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            if (triggers == null)
            {
                throw new ArgumentNullException("triggers");
            }

            var url = GetApiUrl("ScheduledTasks/" + id + "/Triggers");

            Post<TaskTriggerInfo[], EmptyRequestResult>(url, triggers);
        }

        /// <summary>
        /// Updates display preferences for a user
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="prefsId">The item id.</param>
        /// <param name="displayPreferences">The display preferences.</param>
        /// <returns>Task{DisplayPreferences}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public void UpdateDisplayPreferences(string prefsId, DisplayPreferences displayPreferences)
        {
            if (string.IsNullOrEmpty(prefsId))
            {
                throw new ArgumentNullException("prefsId");
            }

            if (displayPreferences == null)
            {
                throw new ArgumentNullException("displayPreferences");
            }

            var dict = new QueryStringDictionary();

            dict.AddIfNotNullOrEmpty("userId", Kernel.CurrentUser.ApiId);
            dict.Add("client", "MBC");

            var url = GetApiUrl("DisplayPreferences/" + prefsId, dict);

            Post<DisplayPreferences, EmptyRequestResult>(url, displayPreferences);
        }

        /// <summary>
        /// Posts a set of data to a url, and deserializes the return stream into T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="args">The args.</param>
        /// <returns>Task{``0}.</returns>
        public T Post<T>(string url, Dictionary<string, string> args)
            where T : class
        {
            return Post<T>(url, args, SerializationFormat);
        }

        /// <summary>
        /// Posts a set of data to a url, and deserializes the return stream into T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="args">The args.</param>
        /// <param name="serializationFormat">The serialization format.</param>
        /// <returns>Task{``0}.</returns>
        public T Post<T>(string url, Dictionary<string, string> args, SerializationFormats serializationFormat)
            where T : class
        {
            url = AddDataFormat(url, serializationFormat);

            // Create the post body
            var strings = args.Keys.Select(key => string.Format("{0}={1}", key, args[key]));
            var postContent = string.Join("&", strings.ToArray());

            const string contentType = "application/x-www-form-urlencoded";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(HttpClient.Post(url, contentType, postContent))))
            {
                return DeserializeFromStream<T>(stream);
            }
        }

        /// <summary>
        /// Posts an object of type TInputType to a given url, and deserializes the response into an object of type TOutputType
        /// </summary>
        /// <typeparam name="TInputType">The type of the T input type.</typeparam>
        /// <typeparam name="TOutputType">The type of the T output type.</typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="obj">The obj.</param>
        /// <returns>Task{``1}.</returns>
        private TOutputType Post<TInputType, TOutputType>(string url, TInputType obj)
            where TOutputType : class
        {
            return Post<TInputType, TOutputType>(url, obj, SerializationFormat);
        }

        /// <summary>
        /// Posts an object of type TInputType to a given url, and deserializes the response into an object of type TOutputType
        /// </summary>
        /// <typeparam name="TInputType">The type of the T input type.</typeparam>
        /// <typeparam name="TOutputType">The type of the T output type.</typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="obj">The obj.</param>
        /// <param name="serializationFormat">The serialization format.</param>
        /// <returns>Task{``1}.</returns>
        private TOutputType Post<TInputType, TOutputType>(string url, TInputType obj, SerializationFormats serializationFormat)
            where TOutputType : class
        {
            url = AddDataFormat(url, serializationFormat);

            const string contentType = "application/json";

            var postContent = SerializeToJson(obj);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(HttpClient.Post(url, contentType, postContent))))
            {
                return DeserializeFromStream<TOutputType>(stream);
            }
        }

        /// <summary>
        /// This is a helper around getting a stream from the server that contains serialized data
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>Task{Stream}.</returns>
        public Stream GetSerializedStream(string url)
        {
            return GetSerializedStream(url, SerializationFormat);
        }

        /// <summary>
        /// This is a helper around getting a stream from the server that contains serialized data
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="serializationFormat">The serialization format.</param>
        /// <returns>Task{Stream}.</returns>
        public Stream GetSerializedStream(string url, SerializationFormats serializationFormat)
        {
            url = AddDataFormat(url, serializationFormat);

            return HttpClient.Get(url);
        }

        /// <summary>
        /// Adds the data format.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="serializationFormat">The serialization format.</param>
        /// <returns>System.String.</returns>
        private string AddDataFormat(string url, SerializationFormats serializationFormat)
        {
            var format = serializationFormat == SerializationFormats.Protobuf ? "x-protobuf" : serializationFormat.ToString();

            if (url.IndexOf('?') == -1)
            {
                url += "?format=" + format;
            }
            else
            {
                url += "&format=" + format;
            }

            return url;
        }

        /// <summary>
        /// Gets the person image URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        public string GetPersonImageUrl(BaseItemDto item, ImageOptions options)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            options.Tag = item.ImageTags[ImageType.Primary];

            return GetPersonImageUrl(item.Name, options);
        }

        /// <summary>
        /// Gets the year image URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        public string GetYearImageUrl(BaseItemDto item, ImageOptions options)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            options.Tag = item.ImageTags[ImageType.Primary];

            return GetYearImageUrl(int.Parse(item.Name), options);
        }

        /// <summary>
        /// Gets the genre image URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        public string GetGenreImageUrl(BaseItemDto item, ImageOptions options)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            options.Tag = item.ImageTags[ImageType.Primary];

            return GetGenreImageUrl(item.Name, options);
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public string GetMusicGenreImageUrl(string name, ImageOptions options)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = "MusicGenres/" + name + "/Images/" + options.ImageType;
            url = GetImageUrl(url, options, new QueryStringDictionary());
            return TestUrl(url) ? url : null;
        }

        /// <summary>
        /// Gets the studio image URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        public string GetStudioImageUrl(BaseItemDto item, ImageOptions options)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            options.Tag = item.ImageTags[ImageType.Primary];

            return GetStudioImageUrl(item.Name, options);
        }

        private HashSet<string> _generalIbnImages;
        protected HashSet<string> GeneralIbnImages
        {
            get { return _generalIbnImages ?? (_generalIbnImages = GetAvailableGeneralIbnImages()); }
        }

        private HashSet<string> GetAvailableGeneralIbnImages()
        {
            var hash = new HashSet<string>();
            foreach (var image in GetGeneralIbnImages())
            {
                hash.Add((image.Context ?? "").ToLower() + image.Name.ToLower());
            }
            return hash;
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// Return null if there is no such image on the server
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public string GetGeneralIbnImageUrl(string name, ImageOptions options)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var filename = "folder";

            switch (options.ImageType)
            {
                case ImageType.Primary:
                    options.MaxWidth = Kernel.Instance.CommonConfigData.MaxPrimaryWidth;
                    break;
                case ImageType.Backdrop:
                    options.MaxWidth = Kernel.Instance.CommonConfigData.MaxBackgroundWidth;
                    filename = "backdrop";
                    break;
                case ImageType.Logo:
                    options.MaxWidth = Kernel.Instance.CommonConfigData.MaxLogoWidth;
                    filename = "logo";
                    break;
                case ImageType.Thumb:
                    filename = "thumb";
                    break;
            }

            if (!GeneralIbnImages.Contains(name.ToLower() + filename)) return null;

            options.Quality = Kernel.Instance.CommonConfigData.JpgImageQuality;

            var url = "Images/General/" + HttpUtility.UrlEncode(name) + "/" + options.ImageType;

            url = GetImageUrl(url, options, new QueryStringDictionary());
            return TestUrl(url) ? url : null;
        }

        /// <summary>
        /// Gets the person image URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        public string GetPersonImageUrl(BaseItemPerson item, ImageOptions options)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            options.Tag = item.PrimaryImageTag;

            return GetPersonImageUrl(item.Name, options);
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="name">The name of the person</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public string GetPersonImageUrl(string name, ImageOptions options)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = "Persons/" + name + "/Images/" + options.ImageType;
            url = GetImageUrl(url, options, new QueryStringDictionary());
            return TestUrl(url) ? url : null;
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="year">The year.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        public string GetYearImageUrl(int year, ImageOptions options)
        {
            var url = "Years/" + year + "/Images/" + options.ImageType;

            return GetImageUrl(url, options, new QueryStringDictionary());
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public string GetGenreImageUrl(string name, ImageOptions options)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = "Genres/" + name + "/Images/" + options.ImageType;

            return GetImageUrl(url, options, new QueryStringDictionary());
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public string GetStudioImageUrl(string name, ImageOptions options)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = "Studios/" + HttpUtility.UrlEncode(name) + "/Images/" + options.ImageType;

            url = GetImageUrl(url, options, new QueryStringDictionary());
            return TestUrl(url) ? url : null;
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="theme"></param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public string GetRatingsImageUrl(string name, string theme, ImageOptions options)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }
            if (string.IsNullOrEmpty(theme))
            {
                throw new ArgumentNullException("theme");
            }

            var url = "Images/Ratings/" + HttpUtility.UrlEncode(theme + "/" + name);

            url = GetImageUrl(url, options, new QueryStringDictionary());
            return TestUrl(url) ? url : null;
        }
    }
}
