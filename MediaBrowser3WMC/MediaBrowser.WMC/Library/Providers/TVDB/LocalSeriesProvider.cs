using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Persistance;
using System.IO;
using System.Xml;
using System.Diagnostics;

namespace MediaBrowser.Library.Providers.TVDB {

    [SupportedType(typeof(Series))]
    public class LocalSeriesProvider : BaseMetadataProvider {

        [Persist]
        string metadataFile;
        [Persist]
        DateTime metadataFileDate;

        public override bool NeedsRefresh() {

            bool changed = false;

            string current = XmlLocation;
            changed = (metadataFile != current);
            changed |= current != null && (new FileInfo(current).LastWriteTimeUtc != metadataFileDate);

            return changed;
        }


        private string XmlLocation {
            get {
                string location = Path.Combine(Item.Path, "series.xml");
                if (!File.Exists(location)) {
                    location = null;
                }
                return location;
            }
        }

        public override void Fetch() {
            string location = Item.Path;
            metadataFile = XmlLocation;
            if (location == null || metadataFile == null) return;

            var series = Item as Series;
            metadataFileDate = new FileInfo(metadataFile).LastWriteTimeUtc;

            XmlDocument metadataDoc = new XmlDocument();
            metadataDoc.Load(metadataFile);

            var seriesNode = metadataDoc.SelectSingleNode("//Series");
            if (seriesNode == null) {
                // support for sams metadata scraper 
                seriesNode = metadataDoc.SelectSingleNode("Item");
            }

            // exit if we have no data. 
            if (seriesNode == null) {
                return;
            }

            string id = series.TVDBSeriesId = seriesNode.SafeGetString("id");

            var p = seriesNode.SafeGetString("banner");
            if (p != null) {
                string bannerFile = System.IO.Path.Combine(location, System.IO.Path.GetFileName(p));
                if (File.Exists(bannerFile))
                    Item.BannerImagePath = bannerFile;
                else {
                    // we don't have the banner file!
                }
            }


            Item.Overview = seriesNode.SafeGetString("Overview");
            if (Item.Overview != null)
                Item.Overview = Item.Overview.Replace("\n\n", "\n");

            Item.Name = seriesNode.SafeGetString("SeriesName");

            //used for extended actor information. will fetch actors with roles stored in <Persons> tag
            foreach (XmlNode node in seriesNode.SelectNodes("Persons/Person[Type='Actor']"))
            {
                try
                {
                    if (series.Actors == null)
                        series.Actors = new List<Actor>();

                    var name = node.SelectSingleNode("Name").InnerText;
                    var role = node.SafeGetString("Role", "");
                    var actor = new Actor() { Name = name, Role = role };

                    series.Actors.Add(actor);
                }
                catch
                {
                    // fall through i dont care, one less actor
                }
            }

            //used for backwards compatibility. Will fetch actors stored in the <Actors> tag
            if (series.Actors == null || series.Actors.Count == 0)
            {
                string actors = seriesNode.SafeGetString("Actors");
                if (actors != null)
                {
                    if (series.Actors == null)
                        series.Actors = new List<Actor>();

                    foreach (string n in actors.Trim('|').Split('|'))
                    {
                        series.Actors.Add(new Actor { Name = n });
                    }
                }
            }

            string genres = seriesNode.SafeGetString("Genre");
            if (genres != null)
                series.Genres = new List<string>(genres.Trim('|').Split('|'));

            series.MpaaRating = seriesNode.SafeGetString("ContentRating");

            string runtimeString = seriesNode.SafeGetString("Runtime");
            if (!string.IsNullOrEmpty(runtimeString)) {

                int runtime;
                if (int.TryParse(runtimeString.Split(' ')[0], out runtime))
                    series.RunningTime = runtime;
            }


            // this causes a problem on localized windows version with a comma seperator in regional options
            // http://community.mediabrowser.tv/permalinks/3263/ratings-for-series-aren-t-calculated-properly-with-non-us-regional-settings
            //string ratingString = seriesNode.SafeGetString("Rating",);
            //if (ratingString != null) {
            //    float imdbRating;
            //    if (float.TryParse(ratingString, out imdbRating)) {
            //        series.ImdbRating = imdbRating;
            //    }
            //}

            //SafeGetSingle only works directly from metadataDoc
            //temporary fix, should be handled better
            series.ImdbRating = metadataDoc.SafeGetSingle("Series/Rating", (float)-1, 10);

            series.Status = seriesNode.SafeGetString("Status");
            series.AirDay = seriesNode.SafeGetString("Airs_DayOfWeek");
            series.AirTime = seriesNode.SafeGetString("Airs_Time");

            string studios = seriesNode.SafeGetString("Network");
            if (studios != null) {
                series.Studios = new List<string>(studios.Split('|'));
                //series.Studios = new List<Studio>();
                //foreach (string n in studios.Split('|'))
                //{
                //    series.Studios.Add(new Studio { Name = n });
                //}
            }

            series.CustomRating = seriesNode.SafeGetString("CustomRating");
            series.CustomPIN = seriesNode.SafeGetString("CustomPIN");

            // Some XML files may have incorrect series ids so do not try to set the item, 
            // this would really mess up the internet provid

        }
    }
}
