using System.Text;
using MediaBrowser.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.MBSystem;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Weather;
using MediaBrowser.Model.Web;
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
        public string[] GetIntros(string itemId, Guid userId)
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
                return DeserializeFromStream<string[]>(stream);
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
        /// Gets DisplayPrefs
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="prefsId">The prefs id</param>
        /// <returns>DisplayPreferences.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public DisplayPreferences GetDisplayPrefs(Guid userId, string prefsId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/DisplayPreferences/" + prefsId);

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
            var url = GetApiUrl("Users");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<UserDto[]>(stream);
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
        /// Gets all People
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public ItemsResult GetAllPeople(ItemsByNameQuery query)
        {
            if (query.UserId == Guid.Empty)
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

            dict.AddIfNotNull("personTypes", query.PersonTypes);

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

            var url = GetApiUrl("Genres/" + name);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
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
        public Stream GetPluginConfigurationFile(Guid pluginId)
        {
            if (pluginId == Guid.Empty)
            {
                throw new ArgumentNullException("pluginId");
            }

            var url = GetApiUrl("Plugins/" + pluginId + "/ConfigurationFile");

            return HttpClient.Get(url);
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

        /// <summary>
        /// Gets weather information for the default location as set in configuration
        /// </summary>
        /// <returns>Task{WeatherInfo}.</returns>
        public WeatherInfo GetWeatherInfo()
        {
            var url = GetApiUrl("Weather");

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<WeatherInfo>(stream);
            }
        }

        /// <summary>
        /// Gets weather information for a specific location
        /// Location can be a US zipcode, or "city,state", "city,state,country", "city,country"
        /// It can also be an ip address, or "latitude,longitude"
        /// </summary>
        /// <param name="location">The location.</param>
        /// <returns>Task{WeatherInfo}.</returns>
        /// <exception cref="System.ArgumentNullException">location</exception>
        public WeatherInfo GetWeatherInfo(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                throw new ArgumentNullException("location");
            }

            var dict = new QueryStringDictionary();

            dict.Add("location", location);

            var url = GetApiUrl("Weather", dict);

            using (var stream = GetSerializedStream(url))
            {
                return DeserializeFromStream<WeatherInfo>(stream);
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
        /// Reports to the server that the user has begun playing an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public void ReportPlaybackStart(string itemId, Guid userId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/PlayingItems/" + itemId);

            Post<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Reports playback progress to the server
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="positionTicks">The position ticks.</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public void ReportPlaybackProgress(string itemId, Guid userId, long? positionTicks)
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

            var url = GetApiUrl("Users/" + userId + "/PlayingItems/" + itemId + "/Progress", dict);

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
        public void ReportPlaybackStopped(string itemId, Guid userId, long? positionTicks)
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

            var url = GetApiUrl("Users/" + userId + "/PlayingItems/" + itemId, dict);

            HttpClient.Delete(url);
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
        public void AuthenticateUser(Guid userId, byte[] sha1Hash)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var password = BitConverter.ToString(sha1Hash).Replace("-", string.Empty);
            var url = GetApiUrl("Users/" + userId + "/Authenticate");

            var args = new Dictionary<string, string>();

            args["password"] = password;

            Post<EmptyRequestResult>(url, args);
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
        public void UpdateDisplayPreferences(Guid userId, string prefsId, DisplayPreferences displayPreferences)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            if (string.IsNullOrEmpty(prefsId))
            {
                throw new ArgumentNullException("prefsId");
            }

            if (displayPreferences == null)
            {
                throw new ArgumentNullException("displayPreferences");
            }

            var url = GetApiUrl("Users/" + userId + "/DisplayPreferences/" + prefsId);

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
    }
}
