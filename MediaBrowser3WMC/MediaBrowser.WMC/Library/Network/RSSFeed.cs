using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.ServiceModel.Syndication;
using System.Diagnostics;
using MediaBrowser.Library.Entities;
using System.Text.RegularExpressions;
using MediaBrowser.Library.Extensions;
using MediaBrowser.LibraryManagement;
using System.IO;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Logging;
using System.Reflection;
using System.Globalization;
using System.Net;
using System.Xml.Linq;

namespace MediaBrowser.Library.Network {
    public class RSSFeed {

        string url;
        SyndicationFeed feed;
        IEnumerable<BaseItem> children;

        public RSSFeed(string url) {
            this.url = url;
        }

        public void Refresh() {
            lock (this) {
                try {
                    using(WebClient client = new WebClient())
                    using (XmlReader reader = new SyndicationFeedXmlReader(client.OpenRead(url)))
                    {
                        feed = SyndicationFeed.Load(reader);
                        children = GetChildren(feed);
                    }
                } catch (Exception ex) {
                    Debug.Assert(false, "Failed to update podcast");
                    Logger.ReportException("Podcast update failed.", ex);
                    throw;
                }
            }
        }

        public string ImageUrl {
            get {
                if (feed == null || feed.ImageUrl == null) return null;
                return feed.ImageUrl.AbsoluteUri;
            }
        }

        public string Title {
            get {
                if (feed == null) return "";
                return feed.Title.Text;
            }
        }

        public string Description {
            get {
                if (feed == null) return null;
                return feed.Description.Text;
            } 
        } 

        private static IEnumerable<BaseItem> GetChildren(SyndicationFeed feed) {
            var videos = new List<BaseItem>();
            
            if (feed == null) return videos;

            foreach (var item in feed.Items) {
                VodCastVideo video = new VodCastVideo();
                video.DateCreated = item.PublishDate.UtcDateTime;
                video.DateModified = item.PublishDate.UtcDateTime;
                video.Name = item.Title.Text;

                // itunes podcasts sometimes don't have a summary 
                if (item.Summary != null && item.Summary.Text != null) {
                    video.Overview = Regex.Replace(item.Summary.Text, @"<(.|\n)*?>", string.Empty);

                    var match = Regex.Match(item.Summary.Text, @"<img src=[\""\']([^\'\""]+)", RegexOptions.IgnoreCase);
                    if (match != null && match.Groups.Count > 1) {
                        video.PrimaryImagePath = match.Groups[1].Value;
                    }
                }

                foreach (var link in item.Links) {
                    if (link.RelationshipType == "enclosure")
                    {
                        video.Path = (link.Uri.AbsoluteUri);
                    }
                }

                foreach (var extension in item.ElementExtensions.Where(e => e.OuterNamespace == "http://search.yahoo.com/mrss/" && e.OuterName == "thumbnail"))
                {
                    var attr = extension.GetObject<XElement>();
                    if (attr != null)
                    {
                        var url = attr.Attribute("url");
                        if (url != null)
                        {
                            video.PrimaryImagePath = url.Value;
                        }
                    }
                
                }

                if (video.Path != null)
                {
                    video.Id = video.Path.GetMD5();
                    videos.Add(video);
                }
            }

            // TED Talks appends the same damn string on each title, fix it
            if (videos.Count > 5)
            {
                string common = videos[0].Name;

                foreach (var video in videos.Skip(1))
                {
                    while (!video.Name.StartsWith(common))
                    {
                        if (common.Length < 2)
                        {
                            break;
                        }
                        common = common.Substring(0, common.Length - 1);
                    }

                    if (common.Length < 2)
                    {
                        break;
                    }
                }
                
                if (common.Length > 2)
                {
                    foreach (var video in videos)
                    {
                        if (video.Name.Length > common.Length)
                        {
                            video.Name = video.Name.Substring(common.Length);
                        }
                    }
                }

            }

            return videos;
        }

        public IEnumerable<BaseItem> Children {
            get {
                return children;
            }
        }

        // Save a basic .vodcast file that the entity framework understands 
        public void Save(string folder) {
            // find a file name based off title. 
            string name = Helper.RemoveInvalidFileChars(Title); 
            string filename = Path.Combine(folder, name + ".vodcast");

            if (!File.Exists(filename)) {
                VodcastContents generator = new VodcastContents();
                generator.Url = url;
                generator.FilesToRetain = -1;
                generator.DownloadPolicy = DownloadPolicy.Stream;
                File.WriteAllText(filename, generator.Contents);
            } else {
                throw new ApplicationException("Looks like we already have this podcast!");
            }

        }
    }


    /// <summary>
    /// http://stackoverflow.com/questions/210375/problems-reading-rss-with-c-and-net-3-5 workaround datetime issues
    /// </summary>
    public class SyndicationFeedXmlReader : XmlTextReader
    {
        readonly string[] Rss20DateTimeHints = { "pubDate" };
        readonly string[] Atom10DateTimeHints = { "updated", "published", "lastBuildDate" };
        private bool isRss2DateTime = false;
        private bool isAtomDateTime = false;

        public SyndicationFeedXmlReader(Stream stream) : base(stream) { }

        public override bool IsStartElement(string localname, string ns)
        {
            isRss2DateTime = false;
            isAtomDateTime = false;

            if (Rss20DateTimeHints.Contains(localname)) isRss2DateTime = true;
            if (Atom10DateTimeHints.Contains(localname)) isAtomDateTime = true;

            return base.IsStartElement(localname, ns);
        }

        /// <summary>
        /// From Argotic MIT : http://argotic.codeplex.com/releases/view/14436
        /// </summary>
        private static string ReplaceRfc822TimeZoneWithOffset(string value)
        {

            //------------------------------------------------------------
            //	Perform conversion
            //------------------------------------------------------------
            value = value.Trim();
            if (value.EndsWith("UT", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}+0:00", value.TrimEnd("UT".ToCharArray()));
            }
            else if (value.EndsWith("UTC", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}+0:00", value.TrimEnd("UTC".ToCharArray()));
            }
            else if (value.EndsWith("EST", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-05:00", value.TrimEnd("EST".ToCharArray()));
            }
            else if (value.EndsWith("EDT", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-04:00", value.TrimEnd("EDT".ToCharArray()));
            }
            else if (value.EndsWith("CST", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-06:00", value.TrimEnd("CST".ToCharArray()));
            }
            else if (value.EndsWith("CDT", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-05:00", value.TrimEnd("CDT".ToCharArray()));
            }
            else if (value.EndsWith("MST", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-07:00", value.TrimEnd("MST".ToCharArray()));
            }
            else if (value.EndsWith("MDT", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-06:00", value.TrimEnd("MDT".ToCharArray()));
            }
            else if (value.EndsWith("PST", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-08:00", value.TrimEnd("PST".ToCharArray()));
            }
            else if (value.EndsWith("PDT", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-07:00", value.TrimEnd("PDT".ToCharArray()));
            }
            else if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}GMT", value.TrimEnd("Z".ToCharArray()));
            }
            else if (value.EndsWith("A", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-01:00", value.TrimEnd("A".ToCharArray()));
            }
            else if (value.EndsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}-12:00", value.TrimEnd("M".ToCharArray()));
            }
            else if (value.EndsWith("N", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}+01:00", value.TrimEnd("N".ToCharArray()));
            }
            else if (value.EndsWith("Y", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format(null, "{0}+12:00", value.TrimEnd("Y".ToCharArray()));
            }
            else
            {
                return value;
            }
        }

        /// <summary>
        /// From Argotic MIT : http://argotic.codeplex.com/releases/view/14436
        /// </summary>
        public static bool TryParseRfc822DateTime(string value, out DateTime result)
        {
            //------------------------------------------------------------
            //	Local members
            //------------------------------------------------------------
            DateTimeFormatInfo dateTimeFormat = CultureInfo.InvariantCulture.DateTimeFormat;
            string[] formats = new string[3];

            //------------------------------------------------------------
            //	Define valid RFC-822 formats
            //------------------------------------------------------------
            formats[0] = dateTimeFormat.RFC1123Pattern;
            formats[1] = "ddd',' d MMM yyyy HH:mm:ss zzz";
            formats[2] = "ddd',' dd MMM yyyy HH:mm:ss zzz";

            //------------------------------------------------------------
            //	Validate parameter  
            //------------------------------------------------------------
            if (String.IsNullOrEmpty(value))
            {
                result = DateTime.MinValue;
                return false;
            }

            //------------------------------------------------------------
            //	Perform conversion of RFC-822 formatted date-time string
            //------------------------------------------------------------
            return DateTime.TryParseExact(ReplaceRfc822TimeZoneWithOffset(value), formats, dateTimeFormat, DateTimeStyles.None, out result);
        }


        /// <summary>
        /// From Argotic MIT : http://argotic.codeplex.com/releases/view/14436
        /// </summary>
        /// <param name="value"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryParseRfc3339DateTime(string value, out DateTime result)
        {
            //------------------------------------------------------------
            //	Local members
            //------------------------------------------------------------
            DateTimeFormatInfo dateTimeFormat = CultureInfo.InvariantCulture.DateTimeFormat;
            string[] formats = new string[15];

            //------------------------------------------------------------
            //	Define valid RFC-3339 formats
            //------------------------------------------------------------
            formats[0] = dateTimeFormat.SortableDateTimePattern;
            formats[1] = dateTimeFormat.UniversalSortableDateTimePattern;
            formats[2] = "yyyy'-'MM'-'dd'T'HH:mm:ss'Z'";
            formats[3] = "yyyy'-'MM'-'dd'T'HH:mm:ss.f'Z'";
            formats[4] = "yyyy'-'MM'-'dd'T'HH:mm:ss.ff'Z'";
            formats[5] = "yyyy'-'MM'-'dd'T'HH:mm:ss.fff'Z'";
            formats[6] = "yyyy'-'MM'-'dd'T'HH:mm:ss.ffff'Z'";
            formats[7] = "yyyy'-'MM'-'dd'T'HH:mm:ss.fffff'Z'";
            formats[8] = "yyyy'-'MM'-'dd'T'HH:mm:ss.ffffff'Z'";
            formats[9] = "yyyy'-'MM'-'dd'T'HH:mm:sszzz";
            formats[10] = "yyyy'-'MM'-'dd'T'HH:mm:ss.ffzzz";
            formats[11] = "yyyy'-'MM'-'dd'T'HH:mm:ss.fffzzz";
            formats[12] = "yyyy'-'MM'-'dd'T'HH:mm:ss.ffffzzz";
            formats[13] = "yyyy'-'MM'-'dd'T'HH:mm:ss.fffffzzz";
            formats[14] = "yyyy'-'MM'-'dd'T'HH:mm:ss.ffffffzzz";

            //------------------------------------------------------------
            //	Validate parameter  
            //------------------------------------------------------------
            if (String.IsNullOrEmpty(value))
            {
                result = DateTime.MinValue;
                return false;
            }

            //------------------------------------------------------------
            //	Perform conversion of RFC-3339 formatted date-time string
            //------------------------------------------------------------
            return DateTime.TryParseExact(value, formats, dateTimeFormat, DateTimeStyles.AssumeUniversal, out result);
        }

        public override string ReadString()
        {
            string dateVal = base.ReadString();

            try
            {
                if (isRss2DateTime)
                {
                    MethodInfo objMethod = typeof(Rss20FeedFormatter).GetMethod("DateFromString", BindingFlags.NonPublic | BindingFlags.Static);
                    Debug.Assert(objMethod != null);
                    objMethod.Invoke(null, new object[] { dateVal, this });

                }
                if (isAtomDateTime)
                {
                    MethodInfo objMethod = typeof(Atom10FeedFormatter).GetMethod("DateFromString", BindingFlags.NonPublic | BindingFlags.Instance);
                    Debug.Assert(objMethod != null);
                    objMethod.Invoke(new Atom10FeedFormatter(), new object[] { dateVal, this });
                }
            }
            catch (TargetInvocationException)
            {
                DateTime date;
                // Microsofts parser bailed 
                if (!TryParseRfc3339DateTime(dateVal, out date) && !TryParseRfc822DateTime(dateVal, out date))
                {
                    date = DateTime.UtcNow;
                }

                DateTimeFormatInfo dtfi = CultureInfo.InvariantCulture.DateTimeFormat;
                dateVal = date.ToString(dtfi.RFC1123Pattern, dtfi);
            }

            return dateVal;

        }

    }
}
