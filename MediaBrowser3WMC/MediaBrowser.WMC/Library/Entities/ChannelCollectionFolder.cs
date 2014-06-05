using System.Linq;

namespace MediaBrowser.Library.Entities
{
    public class ChannelCollectionFolder : LocalIbnSourcedFolder
    {
        protected override bool HideEmptyFolders
        {
            get
            {
                return false;
            }
        }
    }
}