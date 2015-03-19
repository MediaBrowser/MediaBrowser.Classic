﻿using System.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using System;
using System.IO;
using System.Text;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.Hosting;
using System.Diagnostics;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Class AsyncHttpClient
    /// </summary>
    public class MbHttpClient : IHttpClient
    {
        private string AuthHeader;
        private string AuthToken;

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        private ILogger Logger { get; set; }

        public void SetAuthorizationToken(string token)
        {
            AuthToken = token;
        }

        public int Timeout { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public MbHttpClient(ILogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Gets the stream.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>Stream</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public Stream Get(string url)
        {

            Library.Logging.Logger.ReportInfo("Sending Http Get to {0}", url);
#if DEBUG
            Stopwatch sw = new Stopwatch();
            sw.Start();
#endif   
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Headers.Add(HttpRequestHeader.Authorization, AuthHeader);
                req.Headers.Add("X-MediaBrowser-Token", AuthToken);
                var ms = new MemoryStream();
                req.Timeout = Timeout > 0 ? Timeout : 30000;
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
                if (ex.Status == WebExceptionStatus.ProtocolError && ((HttpWebResponse) ex.Response).StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (Application.CurrentInstance != null)
                    {
                        var header = ex.Response.Headers["X-Application-Error-Code"];
                        if (header == "ParentalControl")
                        {
                            Application.MediaCenterEnvironment.Dialog("User Access is Restricted at This Time", "Parental Control.", DialogButtons.Ok, 100, true);
                        }
                        Library.Logging.Logger.ReportVerbose("Unauthorized Request received from server - logging out");
                        Application.CurrentInstance.Logout(true);
                    }
                }

                Library.Logging.Logger.ReportException("Error getting response from " + url, ex);
                return new MemoryStream();

            }
            catch (Exception ex)
            {
                Library.Logging.Logger.ReportException("Error requesting {0}", ex, url);
                return new MemoryStream();
                //throw;
            }
            finally
            {
#if DEBUG
                Library.Logging.Logger.ReportInfo("Done Http Get to {0} in {1}ms", url, sw.ElapsedMilliseconds);
#endif
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
            Library.Logging.Logger.ReportVerbose("Sending Http Post to {0}", url);

            using (var httpClient = new WebClient {Encoding = Encoding.UTF8})
            {
                httpClient.Headers["Content-type"] = contentType;
                httpClient.Headers["Authorization"] = AuthHeader;
                httpClient.Headers.Add("X-MediaBrowser-Token", AuthToken);

                try
                {
                    return httpClient.UploadString(url, postContent);
                }
                catch (WebException ex)
                {
                    Library.Logging.Logger.ReportException("Error getting response from " + url, ex);

                    throw new HttpException(ex.Message, ex);
                }
                catch (Exception ex)
                {
                    Library.Logging.Logger.ReportException("Error posting {0}", ex, url);

                    return "";
                }
            }
        }

        /// <summary>
        /// Deletes.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public void Delete(string url)
        {
            Library.Logging.Logger.ReportVerbose("Sending Http Delete to {0}", url);

            try
            {
                using (var httpClient = new WebClient {Encoding = Encoding.UTF8})
                {
                    httpClient.Headers["Content-type"] = "application/x-www-form-urlencoded";
                    httpClient.Headers["Authorization"] = AuthHeader;
                    httpClient.Headers.Add("X-MediaBrowser-Token", AuthToken);
                    httpClient.UploadData(url, "DELETE", new byte[] { });
                }
            }
            catch (WebException ex)
            {
                Library.Logging.Logger.ReportException("Error getting response from " + url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (Exception ex)
            {
                Library.Logging.Logger.ReportException("Error requesting {0}", ex, url);

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
                AuthHeader = "";
            }
            else
            {
                AuthHeader = "MediaBrowser " + header;
            }
        }
    }
}
