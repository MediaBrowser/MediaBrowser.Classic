using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Persistance;
using System.Xml;
using System.Web;
using System.IO;
using System.Diagnostics;
using MediaBrowser.Library.Logging;
using MediaBrowser.LibraryManagement;

namespace MediaBrowser.Library.Providers.TVDB {
    [RequiresInternet]
    [SupportedType(typeof(Series), SubclassBehavior.DontInclude)]
    class RemoteSeriesProvider : BaseMetadataProvider {

        private static readonly string rootUrl = "http://www.thetvdb.com/api/";
        private static readonly string seriesQuery = "GetSeries.php?seriesname={0}";
        private static readonly string seriesGet = "http://www.thetvdb.com/api/{0}/series/{1}/{2}.xml";
        private static readonly string getActors = "http://www.thetvdb.com/api/{0}/series/{1}/actors.xml";

        protected const string LOCAL_META_FILE_NAME = "Series.xml";

        [Persist]
        string seriesId;

        [Persist]
        DateTime downloadDate = DateTime.MinValue;

        Series Series { get { return (Series)Item; } }


        public override bool NeedsRefresh() {
            bool fetch = false;

            if (Config.Instance.MetadataCheckForUpdateAge == -1 && downloadDate != DateTime.MinValue)
            {
                Logger.ReportInfo("MetadataCheckForUpdateAge = -1 wont clear and check for updated metadata");
                return false;
            }

            if (Helper.DontFetchMeta(Item.Path)) return false;

            if (!HasLocalMeta())
            {
                fetch = seriesId != GetSeriesId();
                fetch |= (
                    Config.Instance.MetadataCheckForUpdateAge != -1 &&
                    seriesId != null &&
                    DateTime.Today.Subtract(downloadDate).TotalDays > Config.Instance.MetadataCheckForUpdateAge &&
                    DateTime.Today.Subtract(Item.DateCreated).TotalDays < 180
                    );
            }
            return fetch;
        }


        public override void Fetch() {
            if (!HasLocalMeta() && !Helper.DontFetchMeta(Item.Path))
            {
                seriesId = GetSeriesId();

                if (!string.IsNullOrEmpty(seriesId))
                {
                    if (!HasCompleteMetadata() && FetchSeriesData(seriesId))
                    {
                        downloadDate = DateTime.Today;
                        Series.TVDBSeriesId = seriesId;
                    }
                    else
                    {
                        if (!HasCompleteMetadata())
                        {
                            seriesId = null;
                        }
                    }
                }
            }
            else
            {
                Logger.ReportInfo("Series provider not fetching because local meta exists or requested to ignore: " + Item.Name);
            }

        }

        private bool FetchSeriesData(string seriesId) {
            bool success = false;
            Series series = Item as Series;

            string name = Item.Name;
            Logger.ReportVerbose("TvDbProvider: Fetching series data: " + name);

            if (string.IsNullOrEmpty(seriesId)) {
                Logger.ReportInfo("TvDbProvider: Ignoring series: " + name + " because id is forced blank.");
                return false;
            }

            if (!string.IsNullOrEmpty(seriesId)) {
      
                string url = string.Format(seriesGet, TVUtils.TVDBApiKey, seriesId, Config.Instance.PreferredMetaDataLanguage);
                XmlDocument doc = TVUtils.Fetch(url);
                if (doc != null)
                {

                    success = true;

                    series.Name = doc.SafeGetString("//SeriesName");
                    series.Overview = doc.SafeGetString("//Overview");
                    series.ImdbRating = doc.SafeGetSingle("//Rating", 0, 10);
                    series.AirDay = doc.SafeGetString("//Airs_DayOfWeek");
                    series.AirTime = doc.SafeGetString("//Airs_Time");

                    string n = doc.SafeGetString("//banner");
                    if ((n != null) && (n.Length > 0))
                        series.BannerImagePath = TVUtils.BannerUrl + n;

                    string s = doc.SafeGetString("//Network");
                    if ((s != null) && (s.Length > 0))
                        series.Studios = new List<string>(s.Trim().Split('|'));

                    string urlActors = string.Format(getActors, TVUtils.TVDBApiKey, seriesId);
                    XmlDocument docActors = TVUtils.Fetch(urlActors);
                    if (docActors != null)
                    {
                        series.Actors = null;
                        XmlNode actorsNode = null;
                        if (Kernel.Instance.ConfigData.SaveLocalMeta)
                        {
                            //add to the main doc for saving
                            var seriesNode = doc.SelectSingleNode("//Series");
                            if (seriesNode != null)
                            {
                                actorsNode = doc.CreateNode(XmlNodeType.Element, "Actors", null);
                                seriesNode.AppendChild(actorsNode);
                            }
                        }
                        foreach (XmlNode p in docActors.SelectNodes("Actors/Actor"))
                        {
                            if (series.Actors == null)
                                series.Actors = new List<Actor>();
                            string actorName = p.SafeGetString("Name");
                            string actorRole = p.SafeGetString("Role");
                            if (!string.IsNullOrEmpty(name))
                                series.Actors.Add(new Actor { Name = actorName, Role = actorRole });

                            if (Kernel.Instance.ConfigData.SaveLocalMeta && actorsNode != null)
                            {
                                //add to main doc for saving
                                actorsNode.AppendChild(doc.ImportNode(p, true));
                            }
                        }
                    }

                    //string actors = doc.SafeGetString("//Actors");
                    //if (actors != null) {
                    //    string[] a = actors.Trim('|').Split('|');
                    //    if (a.Length > 0) {
                    //        series.Actors = new List<Actor>();
                    //        series.Actors.AddRange(
                    //            a.Select(actor => new Actor { Name = actor }));
                    //    }
                    //}

                    series.MpaaRating = doc.SafeGetString("//ContentRating");

                    string g = doc.SafeGetString("//Genre");

                    if (g != null)
                    {
                        string[] genres = g.Trim('|').Split('|');
                        if (g.Length > 0)
                        {
                            series.Genres = new List<string>();
                            series.Genres.AddRange(genres);
                        }
                    }

                    if (Kernel.Instance.ConfigData.SaveLocalMeta)
                    {
                        try
                        {
                            Kernel.IgnoreFileSystemMods = true;
                            doc.Save(System.IO.Path.Combine(Item.Path, LOCAL_META_FILE_NAME));
                            Kernel.IgnoreFileSystemMods = false;
                        }
                        catch (Exception e)
                        {
                            Logger.ReportException("Error saving local series meta.", e);
                        }
                    }
                }
            }
            if ((!string.IsNullOrEmpty(seriesId)) && ((series.PrimaryImagePath == null) || (series.BackdropImagePath == null))) {
                XmlDocument banners = TVUtils.Fetch(string.Format("http://www.thetvdb.com/api/" + TVUtils.TVDBApiKey + "/series/{0}/banners.xml", seriesId));
                if (banners != null) {

                    XmlNode n = banners.SelectSingleNode("//Banner[BannerType='poster']");
                    if (n != null) {
                        n = n.SelectSingleNode("./BannerPath");
                        if (n != null)
                        {
                            if (Kernel.Instance.ConfigData.SaveLocalMeta)
                            {
                                Kernel.IgnoreFileSystemMods = true;
                                series.PrimaryImagePath = TVUtils.FetchAndSaveImage(TVUtils.BannerUrl + n.InnerText, Path.Combine(Item.Path, "folder"));
                                Kernel.IgnoreFileSystemMods = false;
                            }
                            else
                            {
                                series.PrimaryImagePath = TVUtils.BannerUrl + n.InnerText;
                            }
                        }
                    }

                    n = banners.SelectSingleNode("//Banner[BannerType='series']");
                    if (n != null) {
                        n = n.SelectSingleNode("./BannerPath");
                        if (n != null)
                        {
                            if (Kernel.Instance.ConfigData.SaveLocalMeta)
                            {
                                Kernel.IgnoreFileSystemMods = true;
                                series.BannerImagePath = TVUtils.FetchAndSaveImage(TVUtils.BannerUrl + n.InnerText, Path.Combine(Item.Path, "banner"));
                                Kernel.IgnoreFileSystemMods = false;
                            }
                            else
                            {
                                series.BannerImagePath = TVUtils.BannerUrl + n.InnerText;
                            }
                        }
                    }

                    int bdNo = 0;
                    foreach (XmlNode b in banners.SelectNodes("//Banner[BannerType='fanart']"))
                    {
                        series.BackdropImagePaths = new List<string>();
                        var p = b.SelectSingleNode("./BannerPath");
                        if (p != null)
                        {
                            if (Kernel.Instance.ConfigData.SaveLocalMeta)
                            {
                                Kernel.IgnoreFileSystemMods = true;
                                series.BackdropImagePaths.Add(TVUtils.FetchAndSaveImage(TVUtils.BannerUrl + p.InnerText, Path.Combine(Item.Path, "backdrop" + (bdNo > 0 ? bdNo.ToString() : ""))));
                                Kernel.IgnoreFileSystemMods = false;
                                bdNo++;
                                if (bdNo >= Kernel.Instance.ConfigData.MaxBackdrops) break;
                            }
                            else
                            {
                                series.BackdropImagePaths.Add(TVUtils.BannerUrl + p.InnerText);
                            }

                        }
                    }
                }
            }


            return success;
        }

        private bool HasCompleteMetadata() {
            return (Series.BannerImagePath != null) && (Series.ImdbRating != null)
                                && (Series.Overview != null) && (Series.Name != null) && (Series.Actors != null)
                                && (Series.Genres != null) && (Series.MpaaRating != null) && (Series.TVDBSeriesId != null);
        }

        private bool HasLocalMeta()
        {
            //need at least the xml and folder.jpg/png
            return File.Exists(System.IO.Path.Combine(Item.Path, LOCAL_META_FILE_NAME)) && (File.Exists(System.IO.Path.Combine(Item.Path, "folder.jpg")) ||
                File.Exists(System.IO.Path.Combine(Item.Path, "folder.png")));
        }

        private string GetSeriesId() {
            string seriesId = Series.TVDBSeriesId;
            if (string.IsNullOrEmpty(seriesId)) {
                seriesId = FindSeries(Series.Name);
            }
            return seriesId;
        }


        public string FindSeries(string name) {
            //if id is specified in the file name return it directly
            string id = Helper.GetAttributeFromPath(Item.Path, "tvdbid");
            if (id != null)
            {
                Logger.ReportInfo("TVDbProvider: TVDb ID specified in file path.  Using: " + id);
                return id;
            }

            //nope - search for it
            string url = string.Format(rootUrl + seriesQuery, HttpUtility.UrlEncode(name));
            XmlDocument doc = TVUtils.Fetch(url);
            XmlNodeList nodes = doc.SelectNodes("//Series");
            string comparableName = GetComparableName(name);
            foreach (XmlNode node in nodes) {
                XmlNode n = node.SelectSingleNode("./SeriesName");
                if (GetComparableName(n.InnerText) == comparableName)
                {
                    n = node.SelectSingleNode("./seriesid");
                    if (n != null)
                        return n.InnerText;
                }
                else
                {
                    Logger.ReportInfo("TVDb Provider - " + n.InnerText + " did not match " + comparableName);
                }
            }
            Logger.ReportInfo("TVDb Provider - Could not find " + name + ". Check name on Thetvdb.org.");
            return null;
        }

        static string remove = "\"'!`?";
        static string spacers = "/,.:;\\(){}[]+-_=–*";  // (there are not actually two - in the they are different char codes)

        internal static string GetComparableName(string name) {
            name = name.ToLower();
            name = name.Normalize(NormalizationForm.FormKD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in name) {
                if ((int)c >= 0x2B0 && (int)c <= 0x0333) {
                    // skip char modifier and diacritics 
                } else if (remove.IndexOf(c) > -1) {
                    // skip chars we are removing
                } else if (spacers.IndexOf(c) > -1) {
                    sb.Append(" ");
                } else if (c == '&') {
                    sb.Append(" and ");
                } else {
                    sb.Append(c);
                }
            }
            name = sb.ToString();
            name = name.Replace(", the", "");
            name = name.Replace("the ", " ");
            name = name.Replace(" the ", " ");

            string prev_name;
            do {
                prev_name = name;
                name = name.Replace("  ", " ");
            } while (name.Length != prev_name.Length);

            return name.Trim();
        }

      

    }
}
