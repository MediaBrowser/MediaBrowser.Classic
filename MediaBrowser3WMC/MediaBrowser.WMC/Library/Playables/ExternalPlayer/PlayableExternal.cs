using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Entities;
using MediaBrowser.LibraryManagement;

namespace MediaBrowser.Library.Playables.ExternalPlayer
{
    /// <summary>
    /// Represents an abstract base class for all externally playable items
    /// </summary>
    public class PlayableExternal : PlayableItem
    {
        #region CanPlay
        public override bool CanPlay(IEnumerable<string> files)
        {
            return ConfigData.CanPlay(ExternalPlayerConfiguration, files);
        }

        public override bool CanPlay(IEnumerable<Media> mediaList)
        {
            return ConfigData.CanPlay(ExternalPlayerConfiguration, mediaList);
        }

        public override bool CanPlay(Media media)
        {
            return CanPlay(new Media[] { media });
        }

        public override bool CanPlay(string path)
        {
            return CanPlay(new string[] { path });
        }
        #endregion
        
        /// <summary>
        /// Gets the ExternalPlayer configuration for this instance
        /// </summary>
        public ConfigData.ExternalPlayer ExternalPlayerConfiguration
        {
            get;
            set;
        }

        protected override Type PlaybackControllerType
        {
            get
            {
                return typeof(ConfigurableExternalPlaybackController);
            }
        }

        protected override void Prepare()
        {
            base.Prepare();

            (PlaybackController as ConfigurableExternalPlaybackController).ExternalPlayerConfiguration = ExternalPlayerConfiguration;
        }

        protected override bool StopAllPlaybackBeforePlaying
        {
            get
            {
                return true;
            }
        }

        public virtual Type ConfiguratorType
        {
            get
            {
                return typeof(PlayableExternalConfigurator);
            }
        }
    }
}
