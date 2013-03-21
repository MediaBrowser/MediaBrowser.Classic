using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Entities;


namespace MediaBrowser.Library.Providers
{
    [SupportedType(typeof(Movie))]
    public class LegacyMovieProviderFromXml : MovieProviderFromXml
    {
        protected override string XmlLocation()
        {
            string location = Item.Path;
            return Path.Combine(location, "mymovies.xml");
        }

        protected bool HasNewMeta
        {
            get
            {
                try
                {
                    return File.Exists(base.XmlLocation());
                }
                catch
                {
                    return false;
                }
            }
        }

        public override bool NeedsRefresh()
        {
            if (!HasNewMeta)
                return base.NeedsRefresh();
            else
                return false;
        }

        public override void Fetch()
        {
            //only try if we don't have the new format and do have this one
            if (!HasNewMeta && File.Exists(XmlLocation()))
            {
                Logging.Logger.ReportVerbose("Getting meta from legacy xml file...");
                base.Fetch();
            }
        }
    }
}
