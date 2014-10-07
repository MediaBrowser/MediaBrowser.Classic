using System.Collections.Generic;
using MediaBrowser.Library.Localization;

namespace MediaBrowser.Library.Entities
{
    public class PlaylistsFolder : Folder
    {
        protected override bool CollapseBoxSets
        {
            get { return false; }
        }
    }
}