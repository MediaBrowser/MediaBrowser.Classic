using System.Net;
using System.Threading;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using System;
using System.IO;
using System.Text;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Class AsyncHttpClient
    /// </summary>
    public class MbHttpClient : IHttpClient
    {
        /// <summary>
        /// Gets or sets the HTTP client.
        /// </summary>
        /// <value>The HTTP client.</value>
        private WebClient HttpClient { get; set; }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        private ILogger Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public MbHttpClient(ILogger logger)
        {
            Logger = logger;
            HttpClient = new WebClient();
        }

        /// <summary>
        /// Gets the stream.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>Stream</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public Stream Get(string url)
        {

            MediaBrowser.Library.Logging.Logger.ReportInfo("Sending Http Get to {0}", url);

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                var ms = new MemoryStream();
                req.Timeout = 10000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    var r = resp.GetResponseStream();
                    int read = 1;
                    var buffer = new byte[10000];
                    while (read > 0)
                    {
                        read = r.Read(buffer, 0, buffer.Length);
                        ms.Write(buffer, 0, read);
                    }
                    ms.Flush();
                    ms.Seek(0, SeekOrigin.Begin);

                    return ms;
                }

            }
            catch (WebException ex)
            {
                Library.Logging.Logger.ReportException("Error getting response from " + url, ex);
                return null;

            }
            catch (Exception ex)
            {
                Library.Logging.Logger.ReportException("Error requesting {0}", ex, url);
                return null;
                //throw;
            }
        }

        /// <summary>
        /// Posts
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="postContent">Content of the post.</param>
        /// <returns>string (the response)</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public string Post(string url, string contentType, string postContent)
        {
            Logger.Info("Sending Http Post to {0}", url);

            HttpClient.Encoding = Encoding.UTF8;
            HttpClient.Headers["Content-type"] = contentType;

            try
            {
                return HttpClient.UploadString(url, postContent);
            }
            catch (WebException ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                return "";
                throw new HttpException(ex.Message, ex);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error posting {0}", ex, url);

                return "";
                throw;
            }
        }

        /// <summary>
        /// Deletes.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public void Delete(string url)
        {
            Logger.Debug("Sending Http Delete to {0}", url);

            try
            {
                HttpClient.UploadString(url, "DELETE", "");
            }
            catch (WebException ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error requesting {0}", ex, url);

                throw;
            }
        }

        

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                HttpClient.Dispose();
            }
        }

        /// <summary>
        /// Sets the authorization header that should be supplied on every request
        /// </summary>
        /// <param name="header">The header.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void SetAuthorizationHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                HttpClient.Headers.Remove("Authorization");
            }
            else
            {
                HttpClient.Headers["Authorization"] =  "MediaBrowser " + header;
            }
        }
    }
}
