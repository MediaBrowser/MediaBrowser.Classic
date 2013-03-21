using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Logging;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Providers
{
    [RequiresInternet]
    [SupportedType(typeof(IMovie))]
    public class MovieDbProvider : BaseMetadataProvider
    {
        private static string search3 = @"http://api.themoviedb.org/3/search/movie?api_key={1}&query={0}&language={2}";
        private static string altTitleSearch = @"http://api.themoviedb.org/3/movie/{0}/alternative_titles?api_key={1}&country={2}";
        private static string getInfo3 = @"http://api.themoviedb.org/3/{3}/{0}?api_key={1}&language={2}";
        private static string castInfo = @"http://api.themoviedb.org/3/movie/{0}/casts?api_key={1}";
        private static string releaseInfo = @"http://api.themoviedb.org/3/movie/{0}/releases?api_key={1}";
        private static string getImages = @"http://api.themoviedb.org/3/{2}/{0}/images?api_key={1}";
        private static readonly string ApiKey = "f6bd687ffa63cd282b6ff2c6877f2669";
        static readonly Regex[] nameMatches = new Regex[] {
            new Regex(@"(?<name>.*)\((?<year>\d{4})\)"), // matches "My Movie (2001)" and gives us the name and the year
            new Regex(@"(?<name>.*)") // last resort matches the whole string as the name
        };

        public const string LOCAL_META_FILE_NAME = "MBMovie.json";
        public const string ALT_META_FILE_NAME = "movie.xml";
        protected bool forceDownload = false;
        protected string itemType = "movie";

        #region IMetadataProvider Members

        [Persist]
        string moviedbId;

        [Persist]
        DateTime downloadDate = DateTime.MinValue;

        public override bool NeedsRefresh()
        {
            if (Config.Instance.MetadataCheckForUpdateAge == -1 && downloadDate != DateTime.MinValue)
            {
                Logger.ReportInfo("MetadataCheckForUpdateAge = -1 wont clear and check for updated metadata");
                return false;
            }

            if (Helper.DontFetchMeta(Item.Path)) return false;
            
            if (DateTime.Today.Subtract(Item.DateCreated).TotalDays > 180 && downloadDate != DateTime.MinValue)
                return false; // don't trigger a refresh data for item that are more than 6 months old and have been refreshed before

            if (DateTime.Today.Subtract(downloadDate).TotalDays < Kernel.Instance.ConfigData.MetadataCheckForUpdateAge) // only refresh every n days
                return false;

            if (HasAltMeta())
                return false; //never refresh if has meta from other source

            forceDownload = true; //tell the provider to re-download even if meta already there
            Logger.ReportVerbose("MovieDbProvider - needs refresh.  Download date: " + downloadDate + " item created date: " + Item.DateCreated + " Check for Update age: " + Kernel.Instance.ConfigData.MetadataCheckForUpdateAge);
            return true;
        }


        public override void Fetch()
        {
            if (HasAltMeta())
            {
                Logger.ReportInfo("MovieDbProvider - Not fetching because 3rd party meta exists for "+Item.Name);
                return;
            }
            if (Helper.DontFetchMeta(Item.Path))
            {
                Logger.ReportInfo("MovieDbProvider - Not fetching because requested to ignore " + Item.Name);
                return;
            }

            if (forceDownload || !Kernel.Instance.ConfigData.SaveLocalMeta || !HasLocalMeta())
            {
                forceDownload = false; //reset
                FetchMovieData();
                downloadDate = DateTime.UtcNow.AddHours(4); // fudge for differing system times
            }
            else
            {
                Logger.ReportVerbose("MovieDBProvider not fetching because local meta exists for " + Item.Name);
                downloadDate = DateTime.UtcNow.AddHours(4);
            }
        }

        private bool HasLocalMeta()
        {
            //need at least the xml and folder.jpg/png or a movie.xml put in by someone else
            return HasAltMeta() || (File.Exists(System.IO.Path.Combine(Item.Path,LOCAL_META_FILE_NAME)));
        }

        protected bool HasLocalImage()
        {
            return File.Exists(System.IO.Path.Combine(Item.Path,"folder.jpg")) ||
                File.Exists(System.IO.Path.Combine(Item.Path,"folder.png"));
        }

        private bool HasAltMeta()
        {
            return File.Exists(System.IO.Path.Combine(Item.Path, ALT_META_FILE_NAME)) ;
        }

        private void FetchMovieData()
        {
            string id = FindId(Item.Name, ((IMovie)Item).ProductionYear);
            if (id != null)
            {
                FetchMovieData(id);
            }
            else
            {
                Logger.ReportWarning("MovieDBProvider could not find " + Item.Name + ". Check name on themoviedb.org.");
            }
        }

        protected void ParseName(string name, out string justName, out int? year) {
            justName = null;
            year = null;
            foreach (Regex re in nameMatches)
            {
                Match m = re.Match(name);
                if (m.Success)
                {
                    justName = m.Groups["name"].Value.Trim();
                    string y = m.Groups["year"] != null ? m.Groups["year"].Value : null;
                    int temp;
                    year = Int32.TryParse(y, out temp) ? temp : (int?)null;
                    break;
                }
            }
        }

        public string FindId(string name, int? productionYear)
        {
            int? year = null;
            ParseName(name, out name, out year);

            if (year == null && productionYear != null) {
                year = productionYear;
            }

            Logger.ReportInfo("MovieDbProvider: Finding id for movie: " + name);
            string language = Kernel.Instance.ConfigData.PreferredMetaDataLanguage.ToLower();

            //if id is specified in the file name return it directly
            string justName = Item.Path != null ? Item.Path.Substring(Item.Path.LastIndexOf("\\")) : "";
            string id = Helper.GetAttributeFromPath(justName, "tmdbid");
            if (id != null)
            {
                Logger.ReportInfo("MovieDbProvider: tMDb ID specified in file path.  Using: " + id);
                return id;
            }

            //if we are a boxset - look at our first child
            BoxSet boxset = Item as BoxSet;
            if (boxset != null)
            {
                if (boxset.Children.Count > 1)
                {
                    var firstChild = boxset.Children[0];
                    Logger.ReportVerbose("MovieDbProvider - Attempting to find boxset ID from: " + firstChild.Name);
                    string childName;
                    int? childYear;
                    ParseName(firstChild.Name, out childName, out childYear);
                    id = GetBoxsetIdFromMovie(childName, childYear, language);
                    if (id != null)
                    {
                        Logger.ReportInfo("MovieDbProvider - Found Boxset ID: " + id);
                        return id;
                    }

                }
            }
            //nope - search for it
            id = AttemptFindId(name, year, language);
            if (id == null)
            {
                //try in english if wasn't before
                if (language != "en")
                {
                    id = AttemptFindId(name, year, "en");
                }
                else
                {
                    if (id == null)
                    {
                        // try with dot and _ turned to space
                        name = name.Replace(",", " ");
                        name = name.Replace(".", " ");
                        name = name.Replace("  ", " ");
                        name = name.Replace("_", " ");
                        name = name.Replace("-", "");
                        id = AttemptFindId(name, year, language);
                        if (id == null && language != "en")
                        {
                            //finally again, in english
                            id = AttemptFindId(name, year, "en");
                        }
                    }
                }
            }
            return id;
        }

        public virtual string AttemptFindId(string name, int? year, string language)
        {
            string id = null;
            string matchedName = null;
            string url3 = string.Format(search3, UrlEncode(name), ApiKey, language);
            var json = Helper.ToJsonDict(Helper.FetchJson(url3));

            List<string> possibleTitles = new List<string>();
            if (json != null)
            {
                System.Collections.ArrayList results = (System.Collections.ArrayList)json["results"];
                if (results == null || results.Count == 0)
                {
                    //try replacing numbers
                    foreach (var pair in ReplaceStartNumbers)
                    {
                        if (name.StartsWith(pair.Key))
                        {
                            name = name.Remove(0, pair.Key.Length);
                            name = pair.Value + name;
                        }
                    }
                    foreach (var pair in ReplaceEndNumbers)
                    {
                        if (name.EndsWith(pair.Key))
                        {
                            name = name.Remove(name.IndexOf(pair.Key), pair.Key.Length);
                            name = name + pair.Value;
                        }
                    }
                    Logger.ReportInfo("MovieDBProvider - No results.  Trying replacement numbers: " + name);
                    url3 = string.Format(search3, UrlEncode(name), ApiKey, language);
                    json = Helper.ToJsonDict(Helper.FetchJson(url3));
                    results = (System.Collections.ArrayList)json["results"];
                }
                if (results != null) {
                    string compName = GetComparableName(name);
                    foreach (Dictionary<string,object> possible in results)
                    {
                        matchedName = null;
                        id = possible["id"].ToString();
                        string n = (string)possible["title"];
                        if (GetComparableName(n) == compName)
                        {
                            matchedName = n;
                        }
                        else
                        {
                            n = (string)possible["original_title"];
                            if (GetComparableName(n) == compName)
                            {
                                matchedName = n;
                            }
                        }

                        Logger.ReportVerbose("MovieDbProvider - " + compName + " didn't match " + n);
                        //if main title matches we don't have to look for alternatives
                        if (matchedName == null)
                        {
                            //that title didn't match - look for alternatives
                            url3 = string.Format(altTitleSearch, id, ApiKey, Kernel.Instance.ConfigData.MetadataCountryCode);
                            string resp = Helper.FetchJson(url3);
                            var response = Helper.ToJsonDict(resp);
                            //Logger.ReportVerbose("Alt Title response: " + resp);
                            if (response != null)
                            {
                                try
                                {
                                    System.Collections.ArrayList altTitles = (System.Collections.ArrayList)response["titles"];
                                    foreach (Dictionary<string,object> title in altTitles)
                                    {
                                        string t = GetComparableName((string)((Dictionary<string, object>)title).GetValueOrDefault("title",""));
                                        if (t == compName)
                                        {
                                            Logger.ReportVerbose("MovieDbProvider - " + compName + " matched " + t);
                                            matchedName = t;
                                            break;
                                        }
                                        else
                                        {
                                            Logger.ReportVerbose("MovieDbProvider - " + compName + " did not match " + t);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Logger.ReportException("MovieDbProvider - Error in alternate title search.",e);
                                }
                            }
                        }

                        if (matchedName != null)
                        {
                            Logger.ReportVerbose("Match " + matchedName + " for " + name);
                            if (year != null)
                            {
                                DateTime r;
                                DateTime.TryParse(possible["release_date"].ToString(), out r);
                                if ((r != null))
                                {
                                    if (Math.Abs(r.Year - year.Value) > 1) // allow a 1 year tolerance on release date
                                    {
                                        Logger.ReportVerbose("Result " + matchedName + " released on " + r + " did not match year " + year);
                                        continue;
                                    }
                                }
                            }
                            //matched name and year
                            return matchedName != null ? id : null;
                        }

                    }
                }
            }
            return null;
        }

        private static string UrlEncode(string name)
        {
            return HttpUtility.UrlEncode(name);
        }

        protected string GetBoxsetIdFromMovie(string name, int? year, string language)
        {
            string id = null;
            string childId = AttemptFindId(name, year, language);
            if (childId != null)
            {
                string url = string.Format(getInfo3, childId, ApiKey, language, itemType);
                string json = Helper.FetchJson(url);
                var jsonDict = Helper.ToJsonDict(json);
                if (jsonDict != null)
                {
                    try
                    {
                        id = ((int)((Dictionary<string, object>)jsonDict["belongs_to_collection"])["id"]).ToString();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Unable to obtain boxset id.", e);
                    }
                }
            }
            return id;
        }

        void FetchMovieData(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                Logger.ReportInfo("MoviedbProvider: Ignoring " + Item.Name + " because ID forced blank.");
                return;
            }
            itemType = Item is BoxSet ? "collection" : "movie";
            string url = string.Format(getInfo3, id, ApiKey, Config.Instance.PreferredMetaDataLanguage, itemType);
            moviedbId = id;
            string json;

            var info = Helper.FetchJson(url);

            Dictionary<string, object> jsonDict = (Dictionary<string,object>)Helper.ToJsonDict(info);

            if (jsonDict.ContainsKey("overview") && (String.IsNullOrEmpty(info) || jsonDict.GetValueOrDefault<string,object>("overview",null) == null))
            {
                if (Kernel.Instance.ConfigData.PreferredMetaDataLanguage.ToLower() != "en") {
                    Logger.ReportInfo("MovieDbProvider couldn't find meta for language "+Kernel.Instance.ConfigData.PreferredMetaDataLanguage+". Trying English...");
                    url = string.Format(getInfo3, id, ApiKey, "en", itemType);
                    info = Helper.FetchJson(url);
                    if (String.IsNullOrEmpty(info) || ((Dictionary<string, object>)Helper.ToJsonDict(info)).GetValueOrDefault<string, object>("overview", null) == null)
                    {
                        Logger.ReportError("MovieDbProvider - Unable to find information for " + Item.Name + " (id:" + id + ")");
                        return;
                    }
                }
            }

            url = string.Format(castInfo, id, ApiKey, itemType);
            var cast = Helper.FetchJson(url) ?? "";
            int castStart = !String.IsNullOrEmpty(cast) ? cast.IndexOf("\"cast\":") : 0;
            int castEnd = !String.IsNullOrEmpty(cast) ? cast.IndexOf("],",castStart)+1 : 0;
            if (castEnd == 0 && !string.IsNullOrEmpty(cast)) castEnd = cast.IndexOf("]", castStart) + 1; //no crew
            int crewStart = !String.IsNullOrEmpty(cast) ? cast.IndexOf("\"crew\":") : 0;
            int crewEnd = !String.IsNullOrEmpty(cast) ? cast.IndexOf("]", crewStart)+1 : 0;

            url = string.Format(releaseInfo, id, ApiKey, itemType);
            var releases = Helper.FetchJson(url) ?? "";
            int releasesStart = !String.IsNullOrEmpty(releases) ? releases.IndexOf("\"countries\":") : 0;
            int releasesEnd = !String.IsNullOrEmpty(releases) ? releases.IndexOf("]",releasesStart)+1 : 0;

            //combine main info, releases and cast info into one json string
            json = info.Substring(0, info.LastIndexOf("}"));
            json += releases != "" ? ("," + releases.Substring(releasesStart, releasesEnd - releasesStart)) : "";
            json += cast != "" ? "," + (cast.Substring(castStart, castEnd - castStart) + ","
                               + cast.Substring(crewStart, crewEnd - crewStart)) : ""; 
            json += "}";

            ProcessMainInfo(json);

            //now the images
            url = string.Format(getImages, id, ApiKey, itemType);
            var images = Helper.FetchJson(url);
            if (images == null && itemType == "collection")
            {
                url = string.Format(getImages, id, ApiKey, "movie");  //until tmdb corrects the api - collection images are found here
                images = Helper.FetchJson(url);
            }
            ProcessImages(images);

            //and save locally
            if (Kernel.Instance.ConfigData.SaveLocalMeta)
            {
                try
                {
                    
                    Kernel.IgnoreFileSystemMods = true;
                    File.WriteAllText(System.IO.Path.Combine(Item.Path, LOCAL_META_FILE_NAME),json);
                    Kernel.IgnoreFileSystemMods = false;
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error saving local meta file " + System.IO.Path.Combine(Item.Path, LOCAL_META_FILE_NAME), e);

                }
            }
        }

        protected virtual void ProcessMainInfo(string json)
        {
            var jsonDict = Helper.ToJsonDict(json);
            IMovie movie = Item as IMovie;
            if (jsonDict != null)
            {

                movie.Name = (string)jsonDict.GetValueOrDefault<string,object>("title",null) ?? (string)jsonDict.GetValueOrDefault<string,object>("name",null);
                movie.Overview = (string)jsonDict.GetValueOrDefault<string,object>("overview","");
                movie.Overview = movie.Overview != null ? movie.Overview.Replace("\n\n", "\n") : null;
                movie.TagLine = (string)jsonDict.GetValueOrDefault<string,object>("tagline","");
                movie.ImdbID = (string)jsonDict.GetValueOrDefault<string,object>("imdb_id","");
                movie.TmdbID = moviedbId;
                float rating;
                string voteAvg = jsonDict.GetValueOrDefault<string, object>("vote_average", "").ToString();
                string cultureStr = Kernel.Instance.ConfigData.PreferredMetaDataLanguage + "-" + Kernel.Instance.ConfigData.MetadataCountryCode;
                System.Globalization.CultureInfo culture;
                try
                {
                    culture = new System.Globalization.CultureInfo(cultureStr);
                }
                catch
                {
                    culture = System.Globalization.CultureInfo.CurrentCulture; //default to windows settings if other was invalid
                }
                Logger.ReportVerbose("Culture for numeric conversion is: " + culture.Name);
                if (float.TryParse(voteAvg, System.Globalization.NumberStyles.AllowDecimalPoint, culture, out rating))
                    movie.ImdbRating = rating;

                //release date and certification are retrieved based on configured country
                System.Collections.ArrayList releases = (System.Collections.ArrayList)jsonDict.GetValueOrDefault<string,object>("countries",null);
                if (releases != null)
                {
                    string usRelease = null, usCert = null;
                    string ourRelease = null, ourCert = null;
                    string ourCountry = Kernel.Instance.ConfigData.MetadataCountryCode;
                    foreach (Dictionary<string, object> release in releases)
                    {
                        string country = (string)release.GetValueOrDefault<string,object>("iso_3166_1",null);
                        //grab the us info so we can default to it if need be
                        if (country == "US")
                        {
                            usRelease = (string)release.GetValueOrDefault<string,object>("release_date","");
                            usCert = (string)release.GetValueOrDefault<string,object>("certification","");
                        }
                        if (ourCountry != "US")
                        {
                            if (country == ourCountry)
                            {
                                ourRelease = (string)release.GetValueOrDefault<string,object>("release_date","");
                                ourCert = (string)release.GetValueOrDefault<string,object>("certification","");
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(ourRelease))
                    {
                        movie.ProductionYear = Int32.Parse(ourRelease.Substring(0, 4));
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(usRelease))
                        {
                            movie.ProductionYear = Int32.Parse(usRelease.Substring(0, 4));
                        }
                    }
                    if (!string.IsNullOrEmpty(ourCert))
                    {
                        movie.MpaaRating = ourCountry+"-"+ourCert;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(usCert))
                        {
                            movie.MpaaRating = usCert;
                        }
                    }
                }

                //if that still didn't find a rating and we are a boxset, use the one from our first child
                if (movie.MpaaRating == null && movie is BoxSet)
                {
                    var boxset = movie as BoxSet;
                    if (boxset != null)
                    {
                        Logger.ReportInfo("MovieDbProvider - Using rating of first child of boxset...");
                        boxset.MpaaRating = boxset.Children.Count > 0 ? boxset.Children[0].OfficialRating : null;
                    }
                }

                //mediainfo should override this metadata
                if (movie.MediaInfo != null && movie.MediaInfo.RunTime > 0)
                {
                    movie.RunningTime = movie.MediaInfo.RunTime;
                }
                else
                {
                    int runtime;
                    if (Int32.TryParse(jsonDict.GetValueOrDefault<string,object>("runtime","").ToString(), out runtime))
                        movie.RunningTime = runtime;
                }
                
                //studios
                System.Collections.ArrayList studios = (System.Collections.ArrayList)jsonDict.GetValueOrDefault<string,object>("production_companies",null);
                if (studios != null)
                {
                    //always clear so they don't double up
                    movie.Studios = new List<string>();
                    foreach (Dictionary<string, object> studio in studios)
                    {
                        string name = (string)studio.GetValueOrDefault<string,object>("name","");
                        if (name != null) movie.Studios.Add(name);
                    }
                }

                //genres
                System.Collections.ArrayList genres = (System.Collections.ArrayList)jsonDict.GetValueOrDefault<string,object>("genres",null);
                if (genres != null)
                {
                    //always clear so they don't double up
                    movie.Genres = new List<string>();
                    foreach (Dictionary<string, object> genre in genres)
                    {
                        string name = (string)genre.GetValueOrDefault<string,object>("name","");
                        if (name != null) movie.Genres.Add(name);
                    }
                }

                //we will need this if we save people images
                string tmdbImageUrl = Kernel.Instance.ConfigData.TmdbImageUrl + Kernel.Instance.ConfigData.FetchedProfileSize;

                //actors
                System.Collections.ArrayList cast = (System.Collections.ArrayList)jsonDict.GetValueOrDefault<string,object>("cast",null);
                SortedList<int, Actor> sortedActors = new SortedList<int,Actor>();
                if (cast != null)
                {
                    // always clear so they don't double up
                    movie.Actors = new List<Actor>();
                    foreach (Dictionary<string, object> person in cast)
                    {
                        string name = (string)person.GetValueOrDefault<string,object>("name","");
                        string role = (string)person.GetValueOrDefault<string,object>("character","");
                        if (name != null)
                        {
                            try
                            {
                                sortedActors.Add(Convert.ToInt32(person["order"].ToString()), new Actor() { Name = name, Role = role });
                            }
                            catch (ArgumentException e)
                            {
                                Logger.ReportException("Actor " + name + " has duplicate order of " + person["order"].ToString() + " in tmdb data.", e);
                            }
                            if (Kernel.Instance.ConfigData.DownloadPeopleImages && person["profile_path"] != null && !File.Exists(Path.Combine(ApplicationPaths.AppIBNPath, "People/"+name)+"/folder.jpg"))
                            {
                                try 
                                {
                                    string dir = Path.Combine(ApplicationPaths.AppIBNPath, "People/"+name);
                                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                                    DownloadAndSaveImage(tmdbImageUrl+(string)person["profile_path"], dir, "folder");
                                } 
                                catch (Exception e) 
                                {
                                    Logger.ReportException("Error attempting to download/save actor image",e);
                                }
                            }
                        }
                    }
                    //now add them to movie in proper order
                    movie.Actors.AddRange(sortedActors.Values);
                        
                }

                //directors and writers are both in "crew"
                System.Collections.ArrayList crew = (System.Collections.ArrayList)jsonDict.GetValueOrDefault<string,object>("crew",null);
                if (crew != null)
                {
                    //always clear these so they don't double up
                    movie.Directors = new List<string>();
                    movie.Writers = new List<string>();
                    foreach (Dictionary<string, object> person in crew)
                    {
                        string name = (string)person["name"];
                        string job = (string)person["job"];
                        if (name != null)
                        {
                            switch(job) 
                            {
                                case "Director":
                                    movie.Directors.Add(name);
                                    break;
                                case "Screenplay":
                                    movie.Writers.Add(name);
                                    break;
                            }
                        }
                    }
                }

            }

        }

        protected virtual void ProcessImages(string json)
        {
            Dictionary<string, object> jsonDict = Helper.ToJsonDict(json);

            if (jsonDict != null)
            {
                //poster
                System.Collections.ArrayList posters = (System.Collections.ArrayList)jsonDict["posters"];
                if (posters != null && posters.Count > 0 && (Kernel.Instance.ConfigData.RefreshItemImages || !HasLocalImage()))
                {
                    string tmdbImageUrl = Kernel.Instance.ConfigData.TmdbImageUrl + Kernel.Instance.ConfigData.FetchedPosterSize;
                    //posters should be in order of rating.  get first one for our language
                    foreach (Dictionary<string, object> poster in posters)
                    {
                        if ((string)poster["iso_639_1"] == Kernel.Instance.ConfigData.PreferredMetaDataLanguage)
                        {
                            Logger.ReportVerbose("MovieDbProvider - using poster for language " + Kernel.Instance.ConfigData.PreferredMetaDataLanguage);
                            Item.PrimaryImagePath = ProcessImage(tmdbImageUrl + poster["file_path"].ToString(), "folder");
                            break;
                        }
                    }

                    if (Item.PrimaryImagePath == null && (Kernel.Instance.ConfigData.RefreshItemImages || !HasLocalImage()))
                    {
                        //couldn't find one for our specific country - just take the first one with null country
                        Logger.ReportVerbose("MovieDbProvider - no specific language poster using highest rated English one.");
                        string posterPath = "";
                        try 
                        {
                            posterPath = ((Dictionary<string, object>)posters.ToArray().Where(p => (string)((Dictionary<string,object>)p)["iso_639_1"] == "en").First())["file_path"].ToString();
                        } catch
                        {
                            //fall back to first one
                            try
                            {
                                posterPath = ((Dictionary<string, object>)posters[0])["file_path"].ToString();
                            }
                            catch (Exception e)
                            {
                                //give up
                                Logger.ReportException("Unable to find poster.", e);
                            }
                        }
                        if (!String.IsNullOrEmpty(posterPath)) Item.PrimaryImagePath = ProcessImage(tmdbImageUrl + posterPath, "folder");
                    }
                }

                //backdrops
                System.Collections.ArrayList backdrops = (System.Collections.ArrayList)jsonDict["backdrops"];
                if (backdrops != null && backdrops.Count > 0)
                {
                    if (Item.BackdropImagePaths == null) Item.BackdropImagePaths = new List<string>();
                    string tmdbImageUrl = Kernel.Instance.ConfigData.TmdbImageUrl + Kernel.Instance.ConfigData.FetchedBackdropSize;
                    //posters should be in order of rating.  get first n ones
                    int numToFetch = Math.Min(Kernel.Instance.ConfigData.MaxBackdrops, backdrops.Count);
                    for (int i = 0; i < numToFetch; i++)
                    {
                        string bdNum = i == 0 ? "" : i.ToString();
                        Item.BackdropImagePaths.Add(ProcessImage(tmdbImageUrl + ((Dictionary<string, object>)backdrops[i])["file_path"].ToString(), "backdrop" + bdNum));
                    }
                }
            }
            else
            {
                Logger.ReportInfo("MovieDbProvider - No images defined for " + Item.Name);
            }
        }

        protected virtual string ProcessImage(string tmdbPath, string targetName)
        {
            
            if (tmdbPath != null)
            {
                if (Kernel.Instance.ConfigData.SaveLocalMeta)
                {
                    //download and save locally
                    return DownloadAndSaveImage(tmdbPath, Item.Path, targetName);
                }
                else
                {
                    return tmdbPath;
                }
            }
            return null;
        }

        protected virtual string DownloadAndSaveImage(string source, string targetPath, string targetName)
        {
            //download and save locally
            RemoteImage img = new RemoteImage() { Path = source };
            string ext = Path.GetExtension(source).ToLower();
            string fn = (Path.Combine(targetPath, targetName + ext));
            try
            {
                Kernel.IgnoreFileSystemMods = true;
                img.DownloadImage().Save(fn, ext == ".png" ? System.Drawing.Imaging.ImageFormat.Png : System.Drawing.Imaging.ImageFormat.Jpeg);
                Kernel.IgnoreFileSystemMods = false;
                return fn;
            }
            catch (Exception e)
            {
                Logger.ReportException("Error downloading and saving image " + fn, e);
                return null;
            }

        }

        #endregion

        private static readonly Dictionary<string, string> genreMap = CreateGenreMap();

        private static Dictionary<string, string> CreateGenreMap()
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            // some of the genres in the moviedb may be deemed too specific/detailed
            // they certainly don't align to those of other sources 
            // this collection will let us map them to alternative names or "" to ignore them
            /* these are the imdb genres that should probably be our common targets
                Action
                Adventure
                Animation
                Biography
                Comedy
                Crime
                Documentary
                Drama
                Family Fantasy
                Film-Noir
                Game-Show 
                History
                Horror
                Music
                Musical 
                Mystery
                News
                Reality-TV
                Romance 
                Sci-Fi
                Short
                Sport
                Talk-Show 
                Thriller
                War
                Western
             */
            ret.Add("Action Film", "Action");
            ret.Add("Adventure Film", "Adventure");
            ret.Add("Animation Film", "Animation");
            ret.Add("Comedy Film", "Comedy");
            ret.Add("Crime Film", "Crime");
            ret.Add("Children's Film", "Children");
            ret.Add("Disaster Film", "Disaster");
            ret.Add("Documentary Film", "Documentary");
            ret.Add("Drama Film", "Drama");
            ret.Add("Eastern", "Eastern");
            ret.Add("Environmental", "Environmental");
            ret.Add("Erotic Film", "Erotic");
            ret.Add("Family Film", "Family");
            ret.Add("Fantasy Film", "Fantasy");
            ret.Add("Historical Film", "History");
            ret.Add("Horror Film", "Horror");
            ret.Add("Musical Film", "Musical");
            ret.Add("Mystery", "Mystery");
            ret.Add("Mystery Film", "Mystery");
            ret.Add("Romance Film", "Romance");
            ret.Add("Road Movie", "Road Movie");
            ret.Add("Science Fiction Film", "Sci-Fi");
            ret.Add("Science Fiction", "Sci-Fi");
            ret.Add("Thriller", "Thriller");
            ret.Add("Thriller Film", "Thriller");
            ret.Add("Western", "Western");
            ret.Add("Music", "Music");
            ret.Add("Sport", "Sport");
            ret.Add("War", "War");
            ret.Add("Short", "Short");
            ret.Add("Biography", "Biography");
            ret.Add("Film-Noir", "Film-Noir");
            ret.Add("Game-Show", "Game-Show");

            return ret;
        }

        private string MapGenre(string g)
        {
            if (genreMap.ContainsValue(g)) return g; //the new api has cleaned up most of these

            if (genreMap.ContainsKey(g))
                return genreMap[g];
            else
            {
                Logger.ReportWarning("Tmdb category not mapped to genre: " + g);
                return "";
            }
        }

        static string remove = "\"'!`?";
        // "Face/Off" support.
        static string spacers = "/,.:;\\(){}[]+-_=–*";  // (there are not actually two - in the they are different char codes)
        static Dictionary<string, string> ReplaceStartNumbers = new Dictionary<string, string>() {
            {"1 ","one "},
            {"2 ","two "},
            {"3 ","three "},
            {"4 ","four "},
            {"5 ","five "},
            {"6 ","six "},
            {"7 ","seven "},
            {"8 ","eight "},
            {"9 ","nine "},
            {"10 ","ten "},
            {"11 ","eleven "},
            {"12 ","twelve "},
            {"13 ","thirteen "},
            {"100 ","one hundred "},
            {"101 ","one hundred one "}
        };

        static Dictionary<string, string> ReplaceEndNumbers = new Dictionary<string, string>() {
            {" 1"," i"},
            {" 2"," ii"},
            {" 3"," iii"},
            {" 4"," iv"},
            {" 5"," v"},
            {" 6"," vi"},
            {" 7"," vii"},
            {" 8"," viii"},
            {" 9"," ix"},
            {" 10"," x"}
        };

        internal static string GetComparableName(string name)
        {
            name = name.ToLower();
            name = name.Replace("á", "a");
            name = name.Replace("é", "e");
            name = name.Replace("í", "i");
            name = name.Replace("ó", "o");
            name = name.Replace("ú", "u");
            name = name.Replace("ü", "u");
            name = name.Replace("ñ", "n");
            foreach (var pair in ReplaceStartNumbers)
            {
                if (name.StartsWith(pair.Key))
                {
                    name = name.Remove(0, pair.Key.Length);
                    name = pair.Value + name;
                    Logger.ReportInfo("MovieDbProvider - Replaced Start Numbers: " + name);
                }
            }
            foreach (var pair in ReplaceEndNumbers)
            {
                if (name.EndsWith(pair.Key))
                {
                    name = name.Remove(name.IndexOf(pair.Key), pair.Key.Length);
                    name = name + pair.Value;
                    Logger.ReportInfo("MovieDbProvider - Replaced End Numbers: " + name);
                }
            }
            name = name.Normalize(NormalizationForm.FormKD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in name)
            {
                if ((int)c >= 0x2B0 && (int)c <= 0x0333)
                {
                    // skip char modifier and diacritics 
                }
                else if (remove.IndexOf(c) > -1)
                {
                    // skip chars we are removing
                }
                else if (spacers.IndexOf(c) > -1)
                {
                    sb.Append(" ");
                }
                else if (c == '&')
                {
                    sb.Append(" and ");
                }
                else
                {
                    sb.Append(c);
                }
            }
            name = sb.ToString();
            name = name.Replace(", the", "");
            name = name.Replace("the ", " ");
            name = name.Replace(" the ", " ");

            string prev_name;
            do
            {
                prev_name = name;
                name = name.Replace("  ", " ");
            } while (name.Length != prev_name.Length);

            return name.Trim();
        }

    }
}
