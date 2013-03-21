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
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Providers
{
    [SupportedType(typeof(IMovie))]
    public class MBMovieProviderFromJson : MovieDbProvider
    {

        [Persist]
        DateTime lastWriteTime = DateTime.MinValue;

       
        public override bool NeedsRefresh()
        {

            if (File.Exists(ALT_META_FILE_NAME)) //never read if we have manual metadata
                return false;

            string mfile = MetaLocation();
            if (!File.Exists(mfile))
                return false;

          
            DateTime modTime = new FileInfo(mfile).LastWriteTimeUtc;
            if (modTime <= lastWriteTime)
               return false;

            Logger.ReportVerbose("Metadata changed for " + Item.Name + " mod time: " + modTime + " last update time: " + lastWriteTime);
            return true;
        }

        protected virtual string MetaLocation()
        {
            return Path.Combine(Item.Path, LOCAL_META_FILE_NAME);
        }

        public override void Fetch()
        {
            string metaFile = MetaLocation();
            if (!File.Exists(ALT_META_FILE_NAME) && File.Exists(metaFile))
            {
                string json = File.ReadAllText(metaFile);
                Logger.ReportVerbose("Processing MovieDB info from local json...");
                ProcessMainInfo(json);
                lastWriteTime = new FileInfo(metaFile).LastWriteTimeUtc.AddHours(4); //fudge to account for differing system times
            }
        }

    }
}
