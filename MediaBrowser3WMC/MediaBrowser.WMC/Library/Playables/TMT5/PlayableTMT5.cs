using System;
using MediaBrowser.Library.Playables.ExternalPlayer;

namespace MediaBrowser.Library.Playables.TMT5
{
    /// <summary>
    /// Represents an external player that uses the standalone TMT application
    /// </summary>
    public class PlayableTMT5 : PlayableExternal
    {
        protected override Type PlaybackControllerType
        {
            get
            {
                return typeof(TMT5PlaybackController);
            }
        }

        public override Type ConfiguratorType
        {
            get
            {
                return typeof(TMT5Configurator);
            }
        }
    }
}
