using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Persistance;
using System.Xml;
using System.Diagnostics;
using MediaBrowser.Library.Logging;
using MediaBrowser.LibraryManagement;
using System.IO;

namespace MediaBrowser.Library.Providers.TVDB {


    [RequiresInternet]
    [SupportedType(typeof(Episode), SubclassBehavior.DontInclude)]
    class RemoteEpisodeProvider : BaseMetadataProvider {

        private static readonly string episodeQuery = "http://www.thetvdb.com/api/{0}/series/{1}/default/{2}/{3}/{4}.xml";
        private static readonly string absEpisodeQuery = "http://www.thetvdb.com/api/{0}/series/{1}/absolute/{3}/{4}.xml";

        [Persist]
        string seriesId;

        [Persist]
        DateTime downloadDate = DateTime.MinValue;

        protected const string LOCAL_META_FOLDER_NAME = "metadata";

        Episode Episode { get { return (Episode)Item; } } 

        public override bool NeedsRefresh() {
            bool fetch = false;

            if (Config.Instance.MetadataCheckForUpdateAge == -1 && downloadDate != DateTime.MinValue)
            {
                Logger.ReportInfo("MetadataCheckForUpdateAge = -1 wont clear and check for updated metadata");
                return false;
            }

            if (!HasLocalMeta() && !Helper.DontFetchMeta(Item.Path))
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

                if (seriesId != null)
                {
                    if (FetchEpisodeData()) downloadDate = DateTime.Today;
                }
                else
                {
                    Logger.ReportWarning("Episode provider cannot determine Series Id for " + Item.Path);
                }
            }
            else
            {
                Logger.ReportInfo("Episode provider not fetching because local meta exists or requested to ignore: " + Item.Name);
            }
        }


        private bool FetchEpisodeData() {
            var episode = Item as Episode;

            string name = Item.Name;
            string location = Item.Path;
            Logger.ReportVerbose("TvDbProvider: Fetching episode data: " + name);
            string epNum = TVUtils.EpisodeNumberFromFile(location);

            if (epNum == null)
                return false;
            int episodeNumber = Int32.Parse(epNum);

            episode.EpisodeNumber = episodeNumber.ToString();
            bool UsingAbsoluteData = false;

            if (string.IsNullOrEmpty(seriesId)) return false;

            string seasonNumber = "";
            if (Item.Parent is Season) {
                seasonNumber = (Item.Parent as Season).SeasonNumber;
            }

            if (string.IsNullOrEmpty(seasonNumber))
                seasonNumber = TVUtils.SeasonNumberFromEpisodeFile(location); // try and extract the season number from the file name for S1E1, 1x04 etc.

            if (!string.IsNullOrEmpty(seasonNumber)) {
                seasonNumber = seasonNumber.TrimStart('0');

                if (string.IsNullOrEmpty(seasonNumber)) {
                    seasonNumber = "0"; // Specials
                }

                XmlDocument doc = TVUtils.Fetch(string.Format(episodeQuery, TVUtils.TVDBApiKey, seriesId, seasonNumber, episodeNumber, Config.Instance.PreferredMetaDataLanguage));
                //episode does not exist under this season, try absolute numbering.
                //still assuming it's numbered as 1x01
                //this is basicly just for anime.
                if (doc == null && Int32.Parse(seasonNumber) == 1) {
                    doc = TVUtils.Fetch(string.Format(absEpisodeQuery, TVUtils.TVDBApiKey, seriesId, seasonNumber, episodeNumber, Config.Instance.PreferredMetaDataLanguage));
                    UsingAbsoluteData = true;
                }
                if (doc != null) {

                    var p = doc.SafeGetString("//filename");
                    if (p != null)
                    {
                        if (Kernel.Instance.ConfigData.SaveLocalMeta)
                        {
                            Kernel.IgnoreFileSystemMods = true;
                            if (!Directory.Exists(MetaFolderName)) Directory.CreateDirectory(MetaFolderName);
                            Item.PrimaryImagePath = TVUtils.FetchAndSaveImage(TVUtils.BannerUrl + p, Path.Combine(MetaFolderName, Path.GetFileNameWithoutExtension(p)));
                            Kernel.IgnoreFileSystemMods = false;
                        }
                        else
                        {
                            Item.PrimaryImagePath = TVUtils.BannerUrl + p;
                        }
                    }

                    Item.Overview = doc.SafeGetString("//Overview");
                    if (UsingAbsoluteData)
                        episode.EpisodeNumber = doc.SafeGetString("//absolute_number");
                    if (episode.EpisodeNumber == null)
                        episode.EpisodeNumber = doc.SafeGetString("//EpisodeNumber");

                    episode.Name = episode.EpisodeNumber + " - " + doc.SafeGetString("//EpisodeName");
                    episode.SeasonNumber = doc.SafeGetString("//SeasonNumber");
                    episode.ImdbRating = doc.SafeGetSingle("//Rating", (float)-1, 10);
                    episode.FirstAired = doc.SafeGetString("//FirstAired");
                    DateTime airDate;
                    int y = DateTime.TryParse(episode.FirstAired, out airDate) ? airDate.Year : -1;
                    if (y > 1850) {
                        episode.ProductionYear = y;
                    }


                    string actors = doc.SafeGetString("//GuestStars");
                    if (actors != null) {
                        episode.Actors = new List<Actor>(actors.Trim('|').Split('|')
                            .Select(str => new Actor() { Name = str })
                            );
                    }


                    string directors = doc.SafeGetString("//Director");
                    if (directors != null) {
                        episode.Directors = new List<string>(directors.Trim('|').Split('|'));
                    }


                    string writers = doc.SafeGetString("//Writer");
                    if (writers != null) {
                        episode.Writers = new List<string>(writers.Trim('|').Split('|'));
                    }

                    if (Kernel.Instance.ConfigData.SaveLocalMeta)
                    {
                        try
                        {
                            Kernel.IgnoreFileSystemMods = true;
                            if (!Directory.Exists(MetaFolderName)) Directory.CreateDirectory(MetaFolderName);
                            doc.Save(MetaFileName);
                            Kernel.IgnoreFileSystemMods = false;
                        }
                        catch (Exception e)
                        {
                            Logger.ReportException("Error saving local series meta.", e);
                        }
                    }

                    Logger.ReportVerbose("TvDbProvider: Success");
                    return true;
                }

            }

            return false;
        }



        private string GetSeriesId() {
            string seriesId = null;

            Episode episode = Item as Episode;
            if (episode != null)
            {
                Series series = episode.Series;
                if (series != null)
                {

                    seriesId = series.TVDBSeriesId;
                }
            }
            return seriesId;
        }

        private bool HasLocalMeta()
        {
            return (File.Exists(MetaFileName));
        }

        private string MetaFileName
        {
            get
            {
                return Path.Combine(MetaFolderName, Path.GetFileNameWithoutExtension(Item.Path) + ".xml");
            }
        }

        private string MetaFolderName
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(Item.Path), LOCAL_META_FOLDER_NAME);
            }
        }

    }
}
