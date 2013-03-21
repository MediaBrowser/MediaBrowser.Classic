using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Library.Localization
{
    public class RatingsDefinition
    {
        public RatingsDefinition() 
        {
            Init();
        }

        public RatingsDefinition(string file)
        {
            Logging.Logger.ReportInfo("Loading Certification Ratings from file " + file);
            this.file = file;
            Init();
            this.settings = XmlSettings<RatingsDefinition>.Bind(this, file);
        }

        protected void Init()
        {
            //intitialze based on country
            switch (Kernel.Instance.ConfigData.MetadataCountryCode.ToUpper())
            {
                case "US":
                    RatingsDict = new USRatingsDictionary();
                    break;
                case "GB":
                    RatingsDict = new GBRatingsDictionary();
                    break;
                case "NL":
                    RatingsDict = new NLRatingsDictionary();
                    break;
                case "AU":
                    RatingsDict = new AURatingsDictionary();
                    break;
                default:
                    RatingsDict = new USRatingsDictionary();
                    break;
            }
        }

        [SkipField]
        string file;

        [SkipField]
        XmlSettings<RatingsDefinition> settings;

        public static RatingsDefinition FromFile(string file)
        {
            return new RatingsDefinition(file);  
        }

        public void Save() {
            this.settings.Write();
        }

        public Dictionary<string, int> RatingsDict = new Dictionary<string,int>();

    }
}
