using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Persistance;
using System.Diagnostics;
using System.Xml;
using System.IO;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Providers.TVDB {
    [RequiresInternet]
    [SupportedType(typeof(Season), SubclassBehavior.DontInclude)]
    class RemoteSeasonProvider : BaseMetadataProvider {

        [Persist]
        string seriesId;

        [Persist]
        DateTime downloadDate = DateTime.MinValue;

        Season Season { get { return (Season)Item;  } }

        public override bool NeedsRefresh() {
            bool fetch = false;

            if (Config.Instance.MetadataCheckForUpdateAge == -1 && downloadDate != DateTime.MinValue)
                Logger.ReportInfo("MetadataCheckForUpdateAge = -1 wont clear and check for updated metadata");

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
            if (!HasLocalMeta())
            {
                seriesId = GetSeriesId();

                if (seriesId != null)
                {
                    if (FetchSeasonData()) downloadDate = DateTime.Today;
                }
            }
            else
            {
                Logger.ReportInfo("Season provider not fetching because local meta exists: " + Item.Name);
            }
        }


        private bool FetchSeasonData() {
            Season season = Season;
            string name = Item.Name;

            Logger.ReportVerbose("TvDbProvider: Fetching season data: " + name);
            string seasonNum = TVUtils.SeasonNumberFromFolderName(Item.Path);
            int seasonNumber = Int32.Parse(seasonNum);

            season.SeasonNumber = seasonNumber.ToString();

            if (season.SeasonNumber == "0") {
                season.Name = "Specials";
            }

            if (!string.IsNullOrEmpty(seriesId)) {
                if ((Item.PrimaryImagePath == null) || (Item.BannerImagePath == null) || (Item.BackdropImagePath == null)) {
                    XmlDocument banners = TVUtils.Fetch(string.Format("http://www.thetvdb.com/api/" + TVUtils.TVDBApiKey + "/series/{0}/banners.xml", seriesId));


                    XmlNode n = banners.SelectSingleNode("//Banner[BannerType='season'][BannerType2='season'][Season='" + seasonNumber.ToString() + "']");
                    if (n != null) {
                        n = n.SelectSingleNode("./BannerPath");
                        if (n != null)
                            if (Kernel.Instance.ConfigData.SaveLocalMeta)
                            {
                                season.PrimaryImagePath = TVUtils.FetchAndSaveImage(TVUtils.BannerUrl + n.InnerText, Path.Combine(season.Path, "folder"));
                            }
                            else
                            {
                                season.PrimaryImagePath = TVUtils.BannerUrl + n.InnerText;
                            }
                    }


                    n = banners.SelectSingleNode("//Banner[BannerType='season'][BannerType2='seasonwide'][Season='" + seasonNumber.ToString() + "']");
                    if (n != null) {
                        n = n.SelectSingleNode("./BannerPath");
                        if (n != null)
                            if (Kernel.Instance.ConfigData.SaveLocalMeta)
                            {
                                season.BannerImagePath = TVUtils.FetchAndSaveImage(TVUtils.BannerUrl + n.InnerText, Path.Combine(season.Path, "banner"));
                            }
                            else
                            {
                                season.BannerImagePath = TVUtils.BannerUrl + n.InnerText;
                            }
                    }


                    n = banners.SelectSingleNode("//Banner[BannerType='fanart'][Season='" + seasonNumber.ToString() + "']");
                    if (n != null) {
                        n = n.SelectSingleNode("./BannerPath");
                        if (n != null && Item.BackdropImagePath == null) {
                            if (Kernel.Instance.ConfigData.SaveLocalMeta && Kernel.Instance.ConfigData.SaveSeasonBackdrops)
                            {
                                season.BackdropImagePath = TVUtils.FetchAndSaveImage(TVUtils.BannerUrl + n.InnerText, Path.Combine(Item.Path, "backdrop"));
                            }
                            else
                            {
                                Item.BackdropImagePath = TVUtils.BannerUrl + n.InnerText;
                            }
                        }
                    } else if (!Kernel.Instance.ConfigData.SaveLocalMeta) //if saving local - season will inherit from series
                    {
                        // not necessarily accurate but will give a different bit of art to each season
                        XmlNodeList lst = banners.SelectNodes("//Banner[BannerType='fanart']");
                        if (lst.Count > 0) {
                            int num = seasonNumber % lst.Count;
                            n = lst[num];
                            n = n.SelectSingleNode("./BannerPath");
                            if (n != null && Item.BackdropImagePath == null) {
                                Item.BackdropImagePath = TVUtils.BannerUrl + n.InnerText;
                            }
                        }
                    }

                }
                Logger.ReportVerbose("TvDbProvider: Success");
                return true;
            }

            return false;
        }



        private string GetSeriesId() {
            string seriesId = null;

            // for now do not assert, this can happen in some cases. Just fail out and get no season info
          //  Debug.Assert(Season.Parent is Series);
            var parent = Season.Parent as Series;
            if (parent != null) {
                seriesId = parent.TVDBSeriesId;
            }
            return seriesId;
        }
        
        private bool HasLocalMeta()
        {
            //just folder.jpg/png
            return (File.Exists(System.IO.Path.Combine(Item.Path, "folder.jpg")) ||
                File.Exists(System.IO.Path.Combine(Item.Path, "folder.png")));
        }

    }
}
