using System.Collections.Generic;
using MediaBrowser.Library.Playables.ExternalPlayer;

namespace MediaBrowser.Library.Playables.TMT5
{
    /// <summary>
    /// Controls editing TMT settings within the configurator
    /// </summary>
    public class TMT5Configurator : PlayableExternalConfigurator
    {
        /// <summary>
        /// Returns a unique name for the external player
        /// </summary>
        public override string ExternalPlayerName
        {
            get { return "TotalMedia Theatre 5"; }
        }

        /// <summary>
        /// Gets the default configuration that will be pre-populated into the UI of the configurator.
        /// </summary>
        public override ConfigData.ExternalPlayer GetDefaultConfiguration()
        {
            ConfigData.ExternalPlayer config = base.GetDefaultConfiguration();

            config.SupportsPlaylists = false;
            config.SupportsMultiFileCommandArguments = false;

            return config;
        }

        public override string PlayerTips
        {
            get
            {
                return "You will need to enable \"always on top\" and \"auto-fullscreen\". There is no resume support at this time. There is no multi-part movie or folder-based playback support at this time.";
            }
        }

        public override string CommandFieldTooltip
        {
            get
            {
                return "The path to uTotalMediaTheatre5.exe within the TMT installation directory.";
            }
        }

        public override IEnumerable<string> GetKnownPlayerPaths()
        {
            return GetProgramFilesPaths("ArcSoft\\TotalMedia Theatre 5\\uTotalMediaTheatre5.exe");
        }

        public override bool AllowArgumentsEditing
        {
            get
            {
                return false;
            }
        }
    }

}
