using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Diagnostics;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Providers.Attributes;

namespace MediaBrowser.Library.Providers
{
    [SupportedType(typeof(IMovie))]
    public class MovieProviderFromXml : BaseMetadataProvider
    {

        [Persist]
        DateTime lastWriteTime = DateTime.MinValue;

        [Persist]
        string myMovieFile;


        
        #region IMetadataProvider Members

        public override bool NeedsRefresh()
        {
            string lastFile = myMovieFile;

            string mfile = XmlLocation();
            if (!File.Exists(mfile))
                mfile = null;
            if (lastFile != mfile)
                return true;
            if ((mfile == null) && (lastFile == null))
                return false;

          
            DateTime modTime = new FileInfo(mfile).LastWriteTimeUtc;
            DateTime lastTime = lastWriteTime;
            if (modTime <= lastTime)
               return false;
            
            return true;
        }

        protected virtual string XmlLocation()
        {
            string location = Item.Path;
            return Path.Combine(location, "movie.xml");
        }

        public override void Fetch()
        {
            var movie = Item as IMovie;
            Debug.Assert(movie != null);

            string mfile = XmlLocation();
            string location = Path.GetDirectoryName(mfile);
            if (File.Exists(mfile))
            {

                DateTime modTime = new FileInfo(mfile).LastWriteTimeUtc;
                lastWriteTime = modTime;
                myMovieFile = mfile;
                XmlDocument doc = new XmlDocument();
                doc.Load(mfile);

                string s = doc.SafeGetString("Title/LocalTitle");
                if ((s == null) || (s == ""))
                    s = doc.SafeGetString("Title/OriginalTitle");
                movie.Name = s;
                movie.SortName = doc.SafeGetString("Title/SortTitle");

                movie.Overview = doc.SafeGetString("Title/Description");
                if (movie.Overview != null)
                    movie.Overview = movie.Overview.Replace("\n\n", "\n");

                movie.TagLine = doc.SafeGetString("Title/TagLine");
                movie.Plot = doc.SafeGetString("Title/Plot");

                //if added date is in xml override the file/folder date - this isn't gonna work cuz it's already filled in...
                DateTime added = DateTime.MinValue;
                DateTime.TryParse(doc.SafeGetString("Title/Added"), out added);
                if (added > DateTime.MinValue) movie.DateCreated = added;


                string front = doc.SafeGetString("Title/Covers/Front");
                if ((front != null) && (front.Length > 0))
                {
                    front = Path.Combine(location, front);
                    if (File.Exists(front))
                        Item.PrimaryImagePath = front;
                }

                if (string.IsNullOrEmpty(movie.DisplayMediaType))
                {
                    movie.DisplayMediaType = doc.SafeGetString("Title/Type", "");
                    switch (movie.DisplayMediaType.ToLower())
                    {
                        case "blu-ray":
                            movie.DisplayMediaType = MediaType.BluRay.ToString();
                            break;
                        case "dvd":
                            movie.DisplayMediaType = MediaType.DVD.ToString();
                            break;
                        case "hd dvd":
                            movie.DisplayMediaType = MediaType.HDDVD.ToString();
                            break;
                        case "":
                            movie.DisplayMediaType = null;
                            break;
                    }
                }
                if (movie.ProductionYear == null)
                {
                    int y = doc.SafeGetInt32("Title/ProductionYear", 0);
                    if (y > 1850)
                        movie.ProductionYear = y;
                }
                if (movie.ImdbRating == null)
                {
                    float i = doc.SafeGetSingle("Title/IMDBrating", (float)-1, (float)10);
                    if (i >= 0)
                        movie.ImdbRating = i;
                }
                if (movie.ImdbID == null)
                {
                    if (!string.IsNullOrEmpty(doc.SafeGetString("Title/IMDB")))
                    {
                        movie.ImdbID = doc.SafeGetString("Title/IMDB");
                    }
                    else
                    {
                        movie.ImdbID = doc.SafeGetString("Title/IMDbId");
                    }
                }
                if (movie.TmdbID == null)
                {
                    movie.TmdbID = doc.SafeGetString("Title/TMDbId");
                }

                foreach (XmlNode node in doc.SelectNodes("Title/Persons/Person[Type='Actor']"))
                {
                    try
                    {
                        if (movie.Actors == null)
                            movie.Actors = new List<Actor>();

                        var name = node.SelectSingleNode("Name").InnerText;
                        var role = node.SafeGetString("Role", "");
                        var actor = new Actor() { Name = name, Role = role };

                        movie.Actors.Add(actor);
                    }
                    catch
                    {
                        // fall through i dont care, one less actor
                    }
                }


                foreach (XmlNode node in doc.SelectNodes("Title/Persons/Person[Type='Director']"))
                {
                    try
                    {
                        if (movie.Directors == null)
                            movie.Directors = new List<string>();
                        movie.Directors.Add(node.SelectSingleNode("Name").InnerText);
                    }
                    catch
                    {
                        // fall through i dont care, one less director
                    }
                }


                foreach (XmlNode node in doc.SelectNodes("Title/Genres/Genre"))
                {
                    try
                    {
                        if (movie.Genres == null)
                            movie.Genres = new List<string>();
                        movie.Genres.Add(node.InnerText);
                    }
                    catch
                    {
                        // fall through i dont care, one less genre
                    }
                }


                foreach (XmlNode node in doc.SelectNodes("Title/Studios/Studio"))
                {
                    try
                    {
                        if (movie.Studios == null)
                            movie.Studios = new List<string>();
                        movie.Studios.Add(node.InnerText);
                        //movie.Studios.Add(new Studio { Name = node.InnerText });                        
                    }
                    catch
                    {
                        // fall through i dont care, one less actor
                    }
                }

                if (movie.TrailerPath == null)
                    movie.TrailerPath = doc.SafeGetString("Title/LocalTrailer/URL");

                if (movie.MpaaRating == null)
                    movie.MpaaRating = doc.SafeGetString("Title/MPAARating");

                if (movie.MpaaRating == null)
                {
                    int i = doc.SafeGetInt32("Title/ParentalRating/Value", (int)7);
                    switch (i)
                    {
                        case -1:
                            movie.MpaaRating = "NR";
                            break;
                        case 0:
                            movie.MpaaRating = "UR";
                            break;
                        case 1:
                            movie.MpaaRating = "G";
                            break;
                        case 3:
                            movie.MpaaRating = "PG";
                            break;
                        case 4:
                            movie.MpaaRating = "PG-13";
                            break;
                        case 5:
                            movie.MpaaRating = "NC-17";
                            break;
                        case 6:
                            movie.MpaaRating = "R";
                            break;
                        default:
                            movie.MpaaRating = null;
                            break;
                    }
                }
                //if there is a custom rating - use it (if not rating will be filled with MPAARating)
                if (movie.CustomRating == null)
                    movie.CustomRating = doc.SafeGetString("Title/CustomRating");

                if (movie.CustomPIN == null)
                    movie.CustomPIN = doc.SafeGetString("Title/CustomPIN");

                if (movie.AspectRatio == null)
                    movie.AspectRatio = doc.SafeGetString("Title/AspectRatio");

                //MetaBrowser Custom MediaInfo Support
                if (movie.MediaInfo == null) movie.MediaInfo = new MediaInfoData();
                //we need to decode metabrowser strings to format and profile
                string audio = doc.SafeGetString("Title/MediaInfo/Audio/Codec", "");
                if (audio != "")
                {
                    switch (audio.ToLower())
                    {
                        case "dts-es":
                        case "dts-es matrix":
                        case "dts-es discrete":
                            movie.MediaInfo.OverrideData.AudioFormat = "DTS";
                            movie.MediaInfo.OverrideData.AudioProfile = "ES";
                            break;
                        case "dts-hd hra":
                        case "dts-hd high resolution":
                            movie.MediaInfo.OverrideData.AudioFormat = "DTS";
                            movie.MediaInfo.OverrideData.AudioProfile = "HRA";
                            break;
                        case "dts-hd ma":
                        case "dts-hd master":
                            movie.MediaInfo.OverrideData.AudioFormat = "DTS";
                            movie.MediaInfo.OverrideData.AudioProfile = "MA";
                            break;
                        case "dolby digital":
                        case "dolby digital surround ex":
                        case "dolby surround":
                            movie.MediaInfo.OverrideData.AudioFormat = "AC-3";
                            break;
                        case "dolby digital plus":
                            movie.MediaInfo.OverrideData.AudioFormat = "E-AC-3";
                            break;
                        case "dolby truehd":
                            movie.MediaInfo.OverrideData.AudioFormat = "AC-3";
                            movie.MediaInfo.OverrideData.AudioProfile = "TrueHD";
                            break;
                        case "mp2":
                            movie.MediaInfo.OverrideData.AudioFormat = "MPEG Audio";
                            movie.MediaInfo.OverrideData.AudioProfile = "Layer 2";
                            break;
                        case "other":
                            break;
                        default:
                            movie.MediaInfo.OverrideData.AudioFormat = audio;
                            break;
                    }
                }
                movie.MediaInfo.OverrideData.AudioStreamCount = doc.SelectNodes("Title/MediaInfo/Audio/Codec[text() != '']").Count;
                movie.MediaInfo.OverrideData.AudioChannelCount = doc.SafeGetString("Title/MediaInfo/Audio/Channels", "");
                movie.MediaInfo.OverrideData.AudioBitRate = doc.SafeGetInt32("Title/MediaInfo/Audio/BitRate");
                string video = doc.SafeGetString("Title/MediaInfo/Video/Codec", "");
                if (video != "")
                {
                    switch (video.ToLower())
                    {
                        case "sorenson h.263":
                            movie.MediaInfo.OverrideData.VideoCodec = "Sorenson H263";
                            break;
                        case "h.262":
                            movie.MediaInfo.OverrideData.VideoCodec = "MPEG-2 Video";
                            break;
                        case "h.264":
                            movie.MediaInfo.OverrideData.VideoCodec = "AVC";
                            break;
                        default:
                            movie.MediaInfo.OverrideData.VideoCodec = video;
                            break;
                    }
                }
                movie.MediaInfo.OverrideData.VideoBitRate = doc.SafeGetInt32("Title/MediaInfo/Video/BitRate");
                movie.MediaInfo.OverrideData.Height = doc.SafeGetInt32("Title/MediaInfo/Video/Height");
                movie.MediaInfo.OverrideData.Width = doc.SafeGetInt32("Title/MediaInfo/Video/Width");
                movie.MediaInfo.OverrideData.ScanType = doc.SafeGetString("Title/MediaInfo/Video/ScanType", "");
                movie.MediaInfo.OverrideData.VideoFPS = doc.SafeGetString("Title/MediaInfo/Video/FrameRate", "");
                int rt = doc.SafeGetInt32("Title/MediaInfo/Video/Duration", 0);
                if (rt > 0)
                    movie.MediaInfo.OverrideData.RunTime = rt;
                else
                    movie.MediaInfo.OverrideData.RunTime = doc.SafeGetInt32("Title/RunningTime", 0);
                if (movie.MediaInfo.RunTime > 0) movie.RunningTime = movie.MediaInfo.RunTime;

                XmlNodeList nodes = doc.SelectNodes("Title/MediaInfo/Audio/Language");
                List<string> Langs = new List<string>();
                foreach (XmlNode node in nodes)
                {
                    string m = node.InnerText;
                    if (!string.IsNullOrEmpty(m))
                        Langs.Add(m);
                }
                if (Langs.Count > 1)
                {
                    movie.MediaInfo.OverrideData.AudioLanguages = String.Join(" / ", Langs.ToArray());
                }
                else
                {
                    movie.MediaInfo.OverrideData.AudioLanguages = doc.SafeGetString("Title/MediaInfo/Audio/Language", "");
                }
                nodes = doc.SelectNodes("Title/MediaInfo/Subtitle/Language");
                List<string> Subs = new List<string>();
                foreach (XmlNode node in nodes)
                {
                    string n = node.InnerText;
                    if (!string.IsNullOrEmpty(n))
                        Subs.Add(n);
                }
                if (Subs.Count > 1)
                {
                    movie.MediaInfo.OverrideData.Subtitles = String.Join(" / ", Subs.ToArray());
                }
                else
                {
                    movie.MediaInfo.OverrideData.Subtitles = doc.SafeGetString("Title/MediaInfo/Subtitle/Language", "");
                }
            }
        }



        #endregion
    }
}
