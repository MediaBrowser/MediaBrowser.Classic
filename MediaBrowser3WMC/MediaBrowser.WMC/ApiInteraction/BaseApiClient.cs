using System.Web;
using MediaBrowser.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Provides api methods that are usable on all platforms
    /// </summary>
    public abstract class BaseApiClient : IDisposable
    {
        /// <summary>
        /// Gets the logger.
        /// </summary>
        /// <value>The logger.</value>
        protected ILogger Logger { get; private set; }

        /// <summary>
        /// Gets the json serializer.
        /// </summary>
        /// <value>The json serializer.</value>
        public IJsonSerializer JsonSerializer { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseApiClient" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <exception cref="System.ArgumentNullException">logger</exception>
        protected BaseApiClient(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            JsonSerializer = new NewtonsoftJsonSerializer();
            Logger = logger;
            SerializationFormat = SerializationFormats.Json;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseApiClient" /> class.
        /// </summary>
        protected BaseApiClient()
            : this(new NullLogger())
        {
        }
        
        /// <summary>
        /// Gets or sets the server host name (myserver or 192.168.x.x)
        /// </summary>
        /// <value>The name of the server host.</value>
        public string ServerHostName { get; set; }

        /// <summary>
        /// Gets or sets the port number used by the API
        /// </summary>
        /// <value>The server API port.</value>
        public int ServerApiPort { get; set; }

        /// <summary>
        /// Gets or sets the type of the client.
        /// </summary>
        /// <value>The type of the client.</value>
        public string ClientType { get; set; }

        /// <summary>
        /// Gets or sets the name of the device.
        /// </summary>
        /// <value>The name of the device.</value>
        public string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the auth token.
        /// </summary>
        /// <value>The token.</value>
        public string AuthToken { get; set; }

        /// <summary>
        /// Gets or sets the device id.
        /// </summary>
        /// <value>The device id.</value>
        public string DeviceId { get; set; }
        
        private Guid? _currentUserId;

        /// <summary>
        /// Gets or sets the current user id.
        /// </summary>
        /// <value>The current user id.</value>
        public virtual Guid? CurrentUserId
        {
            get { return _currentUserId; }
            set
            {
                _currentUserId = value;
                ResetAuthorizationHeader();
            }
        }

        /// <summary>
        /// Gets the current api url based on hostname and port.
        /// </summary>
        /// <value>The API URL.</value>
        public string ApiUrl
        {
            get
            {
                return string.Format("http://{0}:{1}/mediabrowser", ServerHostName, ServerApiPort);
            }
        }

        /// <summary>
        /// Gets the current url to the dashboard.
        /// </summary>
        /// <value>The API URL.</value>
        public string DashboardUrl
        {
            get
            {
                return string.Format("http://{0}:{1}/mediabrowser/dashboard/dashboard.html", ServerHostName, ServerApiPort);
            }
        }

        public int GetMaxBitRate()
        {
            return ServerHostName.StartsWith("192.168") || ServerHostName.StartsWith("127.0")
                || ServerHostName.StartsWith("172.16") || ServerHostName.StartsWith("10.0")
                || ServerHostName.StartsWith("169.254") || !ServerHostName.Contains('.') 
                ? Config.Instance.LocalMaxBitrate * 1000000 : Config.Instance.RemoteMaxBitrate * 1000000;
        }

        /// <summary>
        /// Gets the default data format to request from the server
        /// </summary>
        /// <value>The serialization format.</value>
        public SerializationFormats SerializationFormat { get; set; }

        /// <summary>
        /// Resets the authorization header.
        /// </summary>
        public void ResetAuthorizationHeader()
        {
            var header = CurrentUserId.HasValue ? string.Format("UserId=\"{0}\", Client=\"{1}\", Version=\"{2}\"", CurrentUserId.Value, ClientType, System.Reflection.Assembly.GetExecutingAssembly().GetName().Version)
                             : string.Format("Client=\"{0}\", Version=\"{1}\"", ClientType, System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

            header += string.Format(", DeviceId=\"{0}\"", DeviceId);
            
            if (!string.IsNullOrEmpty(DeviceName))
            {
                header += string.Format(", Device=\"{0}\"", DeviceName);
            }

            SetAuthorizationHeader(header);
        }

        /// <summary>
        /// Sets the authorization header.
        /// </summary>
        /// <param name="header">The header.</param>
        protected abstract void SetAuthorizationHeader(string header);

        /// <summary>
        /// Gets the API URL.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">handler</exception>
        public string GetApiUrl(string handler)
        {
            return GetApiUrl(handler, new QueryStringDictionary());
        }

        /// <summary>
        /// Gets the API URL.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="queryString">The query string.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">handler</exception>
        public string GetApiUrl(string handler, QueryStringDictionary queryString)
        {
            if (string.IsNullOrEmpty(handler))
            {
                throw new ArgumentNullException("handler");
            }

            if (queryString == null)
            {
                throw new ArgumentNullException("queryString");
            }

            return queryString.GetUrl(ApiUrl + "/" + handler);
        }

        /// <summary>
        /// Gets the name of the slug.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        protected string GetSlugName(string name)
        {
            return name.Replace('/', '-').Replace('?', '-');
        }

        /// <summary>
        /// Creates a url to return a list of genres
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="listType">The type of list to retrieve.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        protected string GetGenreListUrl(ItemQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var dict = new QueryStringDictionary { };

            dict.AddIfNotNullOrEmpty("ParentId", query.ParentId);

            dict.AddIfNotNull("startindex", query.StartIndex);

            dict.AddIfNotNull("limit", query.Limit);

            dict.AddIfNotNull("sortBy", query.SortBy);

            dict.AddIfNotNull("IsPlayed", query.IsPlayed);

            if (query.SortOrder.HasValue)
            {
                dict["sortOrder"] = query.SortOrder.ToString();
            }

            if (query.Fields != null)
            {
                dict.Add("fields", query.Fields.Select(f => f.ToString()));
            }
            if (query.Filters != null)
            {
                dict.Add("Filters", query.Filters.Select(f => f.ToString()));
            }

            if (query.UserId != null)
            {
                dict.Add("UserId", query.UserId);
            }

            dict.Add("recursive", query.Recursive);

            dict.AddIfNotNull("ExcludeItemTypes", query.ExcludeItemTypes);
            dict.AddIfNotNull("IncludeItemTypes", query.IncludeItemTypes);


            return GetApiUrl("Genres/", dict);
        }

        /// <summary>
        /// Creates a url to return a list of music genres
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="listType">The type of list to retrieve.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        protected string GetMusicGenreListUrl(ItemQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var dict = new QueryStringDictionary { };

            dict.AddIfNotNullOrEmpty("ParentId", query.ParentId);

            dict.AddIfNotNull("startindex", query.StartIndex);

            dict.AddIfNotNull("limit", query.Limit);

            dict.AddIfNotNull("sortBy", query.SortBy);

            dict.AddIfNotNull("IsPlayed", query.IsPlayed);

            if (query.SortOrder.HasValue)
            {
                dict["sortOrder"] = query.SortOrder.ToString();
            }

            if (query.Fields != null)
            {
                dict.Add("fields", query.Fields.Select(f => f.ToString()));
            }
            if (query.Filters != null)
            {
                dict.Add("Filters", query.Filters.Select(f => f.ToString()));
            }

            if (query.UserId != null)
            {
                dict.Add("UserId", query.UserId);
            }

            dict.Add("recursive", query.Recursive);

            dict.AddIfNotNull("ExcludeItemTypes", query.ExcludeItemTypes);
            dict.AddIfNotNull("IncludeItemTypes", query.IncludeItemTypes);


            return GetApiUrl("MusicGenres/", dict);
        }

        /// <summary>
        /// Creates a url to return a list of items
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="listType">The type of list to retrieve.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        protected string GetItemListUrl(ItemQuery query, string listType = null)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var dict = new QueryStringDictionary { };

            dict.AddIfNotNullOrEmpty("listtype", listType);
            dict.AddIfNotNullOrEmpty("ParentId", query.ParentId);

            dict.AddIfNotNull("startindex", query.StartIndex);

            dict.AddIfNotNull("limit", query.Limit);

            dict.AddIfNotNull("sortBy", query.SortBy);

            dict.AddIfNotNull("IsPlayed", query.IsPlayed);
            dict.AddIfNotNull("CollapseBoxSetItems", query.CollapseBoxSetItems);

            if (query.SortOrder.HasValue)
            {
                dict["sortOrder"] = query.SortOrder.ToString();
            }

            if (query.Fields != null)
            {
                dict.Add("fields", query.Fields.Select(f => f.ToString()));
            }
            if (query.Filters != null)
            {
                dict.Add("Filters", query.Filters.Select(f => f.ToString()));
            }
            if (query.ImageTypes != null)
            {
                dict.Add("ImageTypes", query.ImageTypes.Select(f => f.ToString()));
            }

            dict.Add("recursive", query.Recursive);

            dict.AddIfNotNull("genres", query.Genres);
            dict.AddIfNotNull("Ids", query.Ids);
            dict.AddIfNotNull("studios", query.Studios);
            dict.AddIfNotNull("ExcludeItemTypes", query.ExcludeItemTypes);
            dict.AddIfNotNull("IncludeItemTypes", query.IncludeItemTypes);
            dict.AddIfNotNull("ExcludeLocationTypes", query.ExcludeLocationTypes.Select(t => t.ToString()));

            dict.AddIfNotNullOrEmpty("person", query.Person);
            dict.AddIfNotNull("personTypes", query.PersonTypes);
            if (!Config.Instance.ShowMissingItems) dict.AddIfNotNull("IsMissing", false);
            if (!Config.Instance.ShowUnairedItems) dict.AddIfNotNull("IsVirtualUnaired", false);

            dict.AddIfNotNull("years", query.Years);

            dict.AddIfNotNullOrEmpty("SearchTerm", query.SearchTerm);
            dict.AddIfNotNullOrEmpty("MaxOfficialRating", query.MaxOfficialRating);
            dict.AddIfNotNullOrEmpty("MinOfficialRating", query.MinOfficialRating);

            return GetApiUrl("Users/" + query.UserId + "/Items", dict);
        }

        /// <summary>
        /// Gets the similar item list URL.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="type">The type.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// query
        /// or
        /// type
        /// </exception>
        protected string GetSimilarItemListUrl(SimilarItemsQuery query, string type)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentNullException("type");
            }

            var dict = new QueryStringDictionary { };

            dict.Add("Id", query.Id);

            dict.AddIfNotNull("Limit", query.Limit);
            dict.AddIfNotNullOrEmpty("UserId", query.UserId);
            if (type == "Movies" && Config.Instance.ExcludeRemoteContentInSearch)
            {
                dict.Add("IncludeTrailers", false);
            }

            if (query.Fields != null)
            {
                dict.Add("fields", query.Fields.Select(f => f.ToString()));
            }

            return GetApiUrl(type + "/" + query.Id + "/Similar", dict);
        }

        /// <summary>
        /// Gets the item by name list URL.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="query">The query.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        protected string GetItemByNameListUrl(string type, ItemsByNameQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var dict = new QueryStringDictionary { };

            dict.AddIfNotNullOrEmpty("ParentId", query.ParentId);

            dict.Add("UserId", query.UserId);
            dict.AddIfNotNull("StartIndex", query.StartIndex);

            dict.AddIfNotNull("Limit", query.Limit);

            dict.AddIfNotNullOrEmpty("NameStartsWith", query.NameStartsWith);
            dict.AddIfNotNullOrEmpty("NameStartsWithOrGreater", query.NameStartsWithOrGreater);
            dict.AddIfNotNullOrEmpty("NameLessThan", query.NameLessThan);

            dict.AddIfNotNull("SortBy", query.SortBy);

            dict.AddIfNotNull("IsPlayed", query.IsPlayed);

            if (query.SortOrder.HasValue)
            {
                dict["sortOrder"] = query.SortOrder.ToString();
            }

            if (query.Fields != null)
            {
                dict.Add("fields", query.Fields.Select(f => f.ToString()));
            }

            if (query.Filters != null)
            {
                dict.Add("Filters", query.Filters.Select(f => f.ToString()));
            }

            if (query.ImageTypes != null)
            {
                dict.Add("ImageTypes", query.ImageTypes.Select(f => f.ToString()));
            }

            var personQuery = query as PersonsQuery;

            if (personQuery != null && personQuery.PersonTypes != null)
            {
                dict.Add("PersonTypes", personQuery.PersonTypes.Select(f => f.ToString()));
            }

            dict.Add("recursive", query.Recursive);

            dict.AddIfNotNull("MediaTypes", query.MediaTypes);
            dict.AddIfNotNull("ExcludeItemTypes", query.ExcludeItemTypes);
            dict.AddIfNotNull("IncludeItemTypes", query.IncludeItemTypes);

            return GetApiUrl(type, dict);
        }

        /// <summary>
        /// Gets the image URL.
        /// </summary>
        /// <param name="baseUrl">The base URL.</param>
        /// <param name="options">The options.</param>
        /// <param name="queryParams">The query params.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">options</exception>
        public string GetImageUrl(string baseUrl, ImageOptions options, QueryStringDictionary queryParams)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (queryParams == null)
            {
                throw new ArgumentNullException("queryParams");
            }

            if (options.ImageIndex.HasValue)
            {
                baseUrl += "/" + options.ImageIndex.Value;
            }

            queryParams.AddIfNotNull("width", options.Width);
            queryParams.AddIfNotNull("height", options.Height);
            queryParams.AddIfNotNull("maxWidth", options.MaxWidth);
            queryParams.AddIfNotNull("maxHeight", options.MaxHeight);
            queryParams.AddIfNotNull("Quality", options.Quality);

            queryParams.AddIfNotNullOrEmpty("tag", options.Tag);

            return GetApiUrl(baseUrl, queryParams);
        }

        /// <summary>
        /// Gets the image URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        public string GetImageUrl(BaseItemDto item, ImageOptions options)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            var index = options.ImageIndex ?? 0;

            if (options.ImageType == ImageType.Backdrop)
            {
                options.Tag = item.BackdropImageTags[index];
            }
            else if (options.ImageType == ImageType.Chapter)
            {
                options.Tag = item.Chapters[index].ImageTag;
            }
            else
            {
                options.Tag = item.ImageTags[options.ImageType];
            }

            return GetImageUrl(item.Id, options);
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="itemId">The Id of the item</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public string GetImageUrl(string itemId, ImageOptions options)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            var url = "Items/" + itemId + "/Images/" + options.ImageType;

            return GetImageUrl(url, options, new QueryStringDictionary());
        }

        /// <summary>
        /// Gets the user image URL.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        public string GetUserImageUrl(UserDto user, ImageOptions options)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            options.Tag = user.PrimaryImageTag;

            return GetUserImageUrl(user.Id, options);
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="userId">The Id of the user</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public string GetUserImageUrl(string userId, ImageOptions options)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = "Users/" + userId + "/Images/" + options.ImageType;

            return GetImageUrl(url, options, new QueryStringDictionary());
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="theme"></param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public string GetMediaInfoImageUrl(string name, string theme, ImageOptions options)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }
            if (string.IsNullOrEmpty(theme))
            {
                throw new ArgumentNullException("theme");
            }

            var url = "Images/MediaInfo/" + HttpUtility.UrlEncode(theme + "/" + name);

            return GetImageUrl(url, options, new QueryStringDictionary());
        }

        /// <summary>
        /// This is a helper to get a list of backdrop url's from a given ApiBaseItemWrapper. If the actual item does not have any backdrops it will return backdrops from the first parent that does.
        /// </summary>
        /// <param name="item">A given item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String[][].</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        public string[] GetBackdropImageUrls(BaseItemDto item, ImageOptions options)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            options.ImageType = ImageType.Backdrop;

            string backdropItemId;
            List<string> backdropImageTags;

            if (item.BackdropCount == 0)
            {
                backdropItemId = item.ParentBackdropItemId;
                backdropImageTags = item.ParentBackdropImageTags;
            }
            else
            {
                backdropItemId = item.Id;
                backdropImageTags = item.BackdropImageTags;
            }

            if (string.IsNullOrEmpty(backdropItemId))
            {
                return new string[] { };
            }

            var files = new string[backdropImageTags.Count];

            for (var i = 0; i < backdropImageTags.Count; i++)
            {
                options.ImageIndex = i;
                options.Tag = backdropImageTags[i];

                files[i] = GetImageUrl(backdropItemId, options);
            }

            return files;
        }

        /// <summary>
        /// This is a helper to get the logo image url from a given ApiBaseItemWrapper. If the actual item does not have a logo, it will return the logo from the first parent that does, or null.
        /// </summary>
        /// <param name="item">A given item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        public string GetLogoImageUrl(BaseItemDto item, ImageOptions options)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            options.ImageType = ImageType.Logo;

            var logoItemId = item.HasLogo ? item.Id : item.ParentLogoItemId;
            var imageTag = item.HasLogo ? item.ImageTags[ImageType.Logo] : item.ParentLogoImageTag;

            if (!string.IsNullOrEmpty(logoItemId))
            {
                options.Tag = imageTag;

                return GetImageUrl(logoItemId, options);
            }

            return null;
        }

        /// <summary>
        /// Gets the url needed to stream a video file
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">options</exception>
        public string GetVideoStreamUrl(VideoStreamOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            var handler = "Videos/" + options.ItemId + "/stream";

            if (!string.IsNullOrEmpty(options.OutputFileExtension))
            {
                handler += "." + options.OutputFileExtension.TrimStart('.');
            }

            return GetVideoStreamUrl(handler, options);
        }

        /// <summary>
        /// Gets the video stream URL.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        private string GetVideoStreamUrl(string handler, VideoStreamOptions options)
        {
            var queryParams = new QueryStringDictionary();

            queryParams.AddIfNotNullOrEmpty("VideoCodec", options.VideoCodec);
            queryParams.AddIfNotNull("VideoBitRate", options.VideoBitRate);
            queryParams.AddIfNotNull("Width", options.Width);
            queryParams.AddIfNotNull("Height", options.Height);
            queryParams.AddIfNotNull("MaxWidth", options.MaxWidth);
            queryParams.AddIfNotNull("MaxHeight", options.MaxHeight);
            queryParams.AddIfNotNull("FrameRate", options.FrameRate);
            queryParams.AddIfNotNull("AudioStreamIndex", options.AudioStreamIndex);
            queryParams.AddIfNotNull("VideoStreamIndex", options.VideoStreamIndex);
            queryParams.AddIfNotNull("SubtitleStreamIndex", options.SubtitleStreamIndex);

            queryParams.AddIfNotNullOrEmpty("Profile", options.Profile);
            queryParams.AddIfNotNullOrEmpty("Level", options.Level);

            return GetMediaStreamUrl(handler, options, queryParams);
        }

        /// <summary>
        /// Gets the media stream URL.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="options">The options.</param>
        /// <param name="queryParams">The query params.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">handler</exception>
        private string GetMediaStreamUrl(string handler, StreamOptions options, QueryStringDictionary queryParams)
        {
            if (string.IsNullOrEmpty(handler))
            {
                throw new ArgumentNullException("handler");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (queryParams == null)
            {
                throw new ArgumentNullException("queryParams");
            }

            queryParams.AddIfNotNullOrEmpty("audiocodec", options.AudioCodec);
            queryParams.AddIfNotNull("audiochannels", options.MaxAudioChannels);
            queryParams.AddIfNotNull("audiosamplerate", options.MaxAudioSampleRate);
            queryParams.AddIfNotNull("AudioBitRate", options.AudioBitRate);
            queryParams.AddIfNotNull("StartTimeTicks", options.StartTimeTicks);
            queryParams.AddIfNotNull("Static", options.Static);

            return GetApiUrl(handler, queryParams);
        }

        /// <summary>
        /// Deserializes from stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns>``0.</returns>
        public T DeserializeFromStream<T>(Stream stream)
            where T : class
        {
            return stream != null ? (T)DeserializeFromStream(stream, typeof(T), SerializationFormat) : null;
        }

        /// <summary>
        /// Deserializes from stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="type">The type.</param>
        /// <param name="format">The format.</param>
        /// <returns>System.Object.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        protected object DeserializeFromStream(Stream stream, Type type, SerializationFormats format)
        {
            if (format == SerializationFormats.Json)
            {
                return JsonSerializer.DeserializeFromStream(stream, type);
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Serializers to json.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns>System.String.</returns>
        protected string SerializeToJson(object obj)
        {
            return JsonSerializer.SerializeToString(obj);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {

        }
    }
}
