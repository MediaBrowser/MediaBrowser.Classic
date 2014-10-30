using System;
using MediaBrowser.Library.Playables.ExternalPlayer;

namespace MediaBrowser.Library.Playables.MpcHc
{
    public class PlayableMpcHc : PlayableExternal
    {
        protected override Type PlaybackControllerType
        {
            get
            {
                return typeof(MpcHcPlaybackController);
            }
        }

        public override bool SupportsStreamedContent
        {
            get
            {
                return true;
            }
        }

        public override Type ConfiguratorType
        {
            get
            {
                return typeof(MpcHcConfigurator);
            }
        }
    }
}
