using System.Collections.Generic;
using MediaBrowser.Library.Playables.ExternalPlayer;

namespace MediaBrowser.Library.Playables.VLC2
{
    /// <summary>
    /// Controls editing VLC settings within the configurator
    /// </summary>
    public class VLC2Configurator : PlayableExternalConfigurator
    {
        /// <summary>
        /// Returns a unique name for the external player
        /// </summary>
        public override string ExternalPlayerName
        {
            get { return "VLC 2"; }
        }

        /// <summary>
        /// Gets the default configuration that will be pre-populated into the UI of the configurator.
        /// </summary>
        public override CommonConfigData.ExternalPlayer GetDefaultConfiguration()
        {
            var config = base.GetDefaultConfiguration();

            // http://wiki.videolan.org/VLC_command-line_help

            config.SupportsMultiFileCommandArguments = true;

            // If you enable the full screen interface in VLC, you really don't need the splash screen.
            config.ShowSplashScreen = false;

            return config;
        }

        public override string PlayerTips
        {
            get
            {
                return "Only supports versions from 2.0 to 2.1.0.  Use the generic type for 2.1.1+. For a better transition from MB to VLC, it is recommended to enable View -> Fullscreen interface in VLC.";
            }
        }

        public override IEnumerable<string> GetKnownPlayerPaths()
        {
            return GetProgramFilesPaths("VideoLAN\\VLC\\vlc.exe");
        }

        public override bool ShowIsoDirectLaunchWarning
        {
            get
            {
                return false;
            }
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
