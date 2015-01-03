using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Connect;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.ApiInteraction
{
    public class ConnectApiClient
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

        protected ConnectHttpClient HttpClient { get; set; }

        public ConnectApiClient(ILogger logger)
        {
            HttpClient = new ConnectHttpClient(logger);
            JsonSerializer = new NewtonsoftJsonSerializer();
        }

        public void SetUserToken(string token)
        {
            HttpClient.SetAuthorizationToken(token);
        }

        /// <summary>
        /// Gets the current api url based on hostname and port.
        /// </summary>
        /// <value>The API URL.</value>
        public string ApiUrl
        {
            get
            {
                return "https://connect.mediabrowser.tv/service";
            }
        }

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
        /// Deserializes from stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns>``0.</returns>
        public T DeserializeFromStream<T>(Stream stream)
            where T : class
        {
            return stream != null ? (T)DeserializeFromStream(stream, typeof(T), SerializationFormats.Json) : null;
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
        /// Posts a set of data to a url, and deserializes the return stream into T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="args">The args.</param>
        /// <returns>Task{``0}.</returns>
        public T Post<T>(string url, Dictionary<string, string> args)
            where T : class
        {
            // Create the post body
            var strings = args.Keys.Select(key => string.Format("{0}={1}", key, args[key]));
            var postContent = string.Join("&", strings.ToArray());

            const string contentType = "application/x-www-form-urlencoded";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(HttpClient.Post(url, contentType, postContent))))
            {
                return DeserializeFromStream<T>(stream);
            }
        }

        public Stream GetSerializedStream(string url)
        {
            return HttpClient.Get(url);
        }

        public IEnumerable<ServerInfo> GetAvailableServers(string connectId)
        {
            var dict = new QueryStringDictionary {{"userId", connectId}};
            var url = GetApiUrl("servers", dict);

            using (var stream = GetSerializedStream(url))
            {
                var result = DeserializeFromStream<ConnectUserServer[]>(stream);
                if (result != null)
                {
                    return result.Select(s => new ServerInfo {AccessToken = s.AccessKey, Id = s.SystemId, Name = s.Name, LocalAddress = s.LocalAddress, ExchangeToken = s.AccessKey, UserLinkType = s.UserType == "Linked" ? UserLinkType.LinkedUser : UserLinkType.Guest, RemoteAddress = s.Url});
                }
                else
                {
                    return null;
                }
            }

        }

        public PinCreationResult CreatePin(string deviceId)
        {
            var dict = new QueryStringDictionary {{"deviceId", deviceId}};
            var url = GetApiUrl("pin");

            return Post<PinCreationResult>(url, dict);
        }

        public PinExchangeResult ExchangePin(string pin, string deviceId)
        {
            var dict = new QueryStringDictionary {{"pin", pin}, {"deviceId", deviceId}};
            var url = GetApiUrl("pin/authenticate");

            return Post<PinExchangeResult>(url, dict);
        }

    }
}
