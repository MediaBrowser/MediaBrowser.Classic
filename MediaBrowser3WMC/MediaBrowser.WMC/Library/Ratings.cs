using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Text;
using System.IO;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Localization;
using MediaBrowser.Library.Configuration;

namespace MediaBrowser.Library
{
    public class Ratings
    {
        private static RatingsDefinition ratingsDef;
        private static Dictionary<string, int> ratings;
        private static Dictionary<int, string> ratingsStrings = new Dictionary<int, string>();
        private static ConfigData config;
        private static ConfigData Config
        {
            get
            {
                if (config == null)
                {
                    config = Kernel.Instance.ConfigData;
                }
                return config;
            }
        }

        public Ratings(bool blockUnrated)
        {
            this.Initialize(blockUnrated);
        }

        public Ratings()
        {
            this.Initialize(false);
        }

        /// <summary>
        /// Use this constructor when calling before kernel is initialized
        /// </summary>
        /// <param name="cfg"></param>
        public Ratings(ConfigData cfg)
        {
            config = cfg;
            this.Initialize(false);
        }

        public void Initialize(bool blockUnrated)
        {
            //build our ratings dictionary from the combined local one and us one
            ratingsDef = RatingsDefinition.FromFile(Path.Combine(ApplicationPaths.AppLocalizationPath, "Ratings-" + Config.MetadataCountryCode+".xml"));
            ratings = new Dictionary<string, int>();
            //global value of None
            ratings.Add("None", -1);
            foreach (var pair in ratingsDef.RatingsDict)
                try
                {
                    ratings.Add(pair.Key, pair.Value);
                }
                catch (Exception e)
                {
                    Logging.Logger.ReportException("Error adding " + pair.Key + " to ratings", e);
                }
            if (Config.MetadataCountryCode.ToUpper() != "US")
                foreach (var pair in new USRatingsDictionary()) 
                    try 
                    {
                        ratings.Add(pair.Key, pair.Value);
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.ReportException("Error adding " + pair.Key + " to ratings", e);
                    }
            //global values of CS
            try
            {
                ratings.Add("CS", 1000);
            }
            catch (Exception e)
            {
                Logging.Logger.ReportException("Error adding CS to ratings", e);
            }

            try
            {
                if (blockUnrated)
                {
                    ratings.Add("", 1000);
                }
                else
                {
                    ratings.Add("", 0);
                }
            }
            catch (Exception e)
            {
                Logging.Logger.ReportException("Error adding blank value to ratings", e);
            }
            //and rating reverse lookup dictionary (non-redundant ones)
            ratingsStrings.Clear();
            int lastLevel = -10;
            ratingsStrings.Add(-1,LocalizedStrings.Instance.GetString("Any"));
            foreach (var pair in ratingsDef.RatingsDict.OrderBy(p => p.Value))
            {
                if (pair.Value > lastLevel)
                {
                    lastLevel = pair.Value;
                    try
                    {
                        ratingsStrings.Add(pair.Value, pair.Key);
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.ReportException("Error adding "+pair.Value+" to ratings strings", e);
                    }
                }
            }
            try
            {
                ratingsStrings.Add(999, "CS"); //this is different because we want Custom to be protected, not allowed
            }
            catch (Exception e)
            {
                Logging.Logger.ReportException("Error adding CS to ratings strings", e);
            }

            return;
        }

        public void SwitchUnrated(bool block)
        {
            ratings.Remove("");
            if (block)
            {
                ratings.Add("", 1000);
            }
            else
            {
                ratings.Add("", 0);
            }
        }
        public static int Level(string ratingStr)
        {
            if (ratingStr != null && ratings.ContainsKey(ratingStr))
                return ratings[ratingStr];
            else
            {
                string stripped = stripCountry(ratingStr);
                if (ratingStr != null && ratings.ContainsKey(stripped))
                    return ratings[stripped];
                else
                    return ratings[""]; //return "unknown" level
            }
        }

        private static string stripCountry(string rating)
        {
            int start = rating.IndexOf('-');
            return start > 0 ? rating.Substring(start + 1) : rating;
        }

        public static string ToString(int level)
        {
            //return the closest one
            while (level > 0) 
            {
                if (ratingsStrings.ContainsKey(level))
                    return ratingsStrings[level];
                else 
                    level--;
            }
            return ratingsStrings.Values.FirstOrDefault(); //default to first one
        }
        public List<string> ToStrings()
        {
            //return the whole list of ratings strings
            return ratingsStrings.Values.ToList();
        }

        public List<int> ToValues()
        {
            //return the whole list of ratings values
            return ratingsStrings.Keys.ToList();
        }

        public Microsoft.MediaCenter.UI.Image RatingImage(string rating)
        {
            return Helper.GetMediaInfoImage("Rated_" + rating);
        }


    }
}
