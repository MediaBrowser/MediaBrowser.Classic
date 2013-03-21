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

namespace MediaBrowser.Library.Providers.TVDB
{

    [SupportedType(typeof(Season))]
    public class LocalSeasonProvider : BaseMetadataProvider
    {

        [Persist]
        DateTime folderDate;

        public override bool NeedsRefresh()
        {

            bool changed = false;

            changed = (new FileInfo(Item.Path).LastWriteTimeUtc != folderDate);

            return changed;
        }


        public override void Fetch()
        {
            folderDate = new FileInfo(Item.Path).LastWriteTimeUtc;
            //all we need to do here is fill in season number
            Season season = Item as Season;

            if (season != null)
            {
                if (season.SeasonNumber == null)
                {
                    string seasonNum = TVUtils.SeasonNumberFromFolderName(Item.Path);
                    int seasonNumber = Int32.Parse(seasonNum);

                    season.SeasonNumber = seasonNumber.ToString();

                    if (season.SeasonNumber == "0")
                    {
                        season.Name = "Specials";
                    }
                }
            }
        }
    }
}
