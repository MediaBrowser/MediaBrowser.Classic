using System.Linq;

namespace MediaBrowser.Library.Entities
{
    public class Channel : Folder
    {
        protected override System.Collections.Generic.List<BaseItem> GetCachedChildren()
        {
            return Kernel.Instance.MB3ApiRepository.RetrieveChannelChildren(ApiId).ToList();
        }

        //until there is an API for this we don't have recent items
        public override Folder QuickList
        {
            get
            {
                return new Folder();
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