using System;

namespace MediaBrowser.Library.Playables.TMT5
{
    /// <summary>
    /// Represents an external player that uses the WMC add-in
    /// </summary>
    public class PlayableTMT5AddInForWMC : PlayableTMT5
    {
        protected override Type PlaybackControllerType
        {
            get
            {
                return typeof(TMT5AddInPlaybackController);
            }
        }

        public override Type ConfiguratorType
        {
            get
            {
                return typeof(TMT5AddInForWMCConfigurator);
            }
        }

        protected override void StopAllApplicationPlayback()
        {
            base.StopAllApplicationPlayback();

            // The TMT plugin seems to need some extra time after stopping playback.
            // The internal player needs to completely wind down or TMT will have issues
            System.Threading.Thread.Sleep(1000);
        }
    }
}
