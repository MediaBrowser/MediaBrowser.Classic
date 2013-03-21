using System;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Playables.ExternalPlayer;

namespace MediaBrowser.Library.Playables.VLC2
{
    public class PlayableVLC2 : PlayableExternal
    {
        protected override Type PlaybackControllerType
        {
            get
            {
                return typeof(VLC2PlaybackController);
            }
        }

        public override Type ConfiguratorType
        {
            get
            {
                return typeof(VLC2Configurator);
            }
        }

        protected override bool IsPlaylistCapable(Media media)
        {
            // VLC seems to handle everything in a playlist
            return true;
        }
    }
}
