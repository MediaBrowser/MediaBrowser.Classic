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

        public override string[] RalIncludeTypes
        {
            get
            {
                return new string[] { "ChannelVideoItem", "ChannelAudioItem" };
            }
            set
            {
                base.RalIncludeTypes = value;
            }
        }

    }
}