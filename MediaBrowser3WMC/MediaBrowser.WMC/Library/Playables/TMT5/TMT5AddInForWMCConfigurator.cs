using System.Collections.Generic;

namespace MediaBrowser.Library.Playables.TMT5
{
    public class TMT5AddInForWMCConfigurator : TMT5Configurator
    {
        /// <summary>
        /// Returns a unique name for the external player
        /// </summary>
        public override string ExternalPlayerName
        {
            get { return "TotalMedia Theatre 5 WMC Add-On"; }
        }

        /// <summary>
        /// Gets the default configuration that will be pre-populated into the UI of the configurator.
        /// </summary>
        public override ConfigData.ExternalPlayer GetDefaultConfiguration()
        {
            ConfigData.ExternalPlayer config = base.GetDefaultConfiguration();

            config.LaunchType = ConfigData.ExternalPlayerLaunchType.WMCNavigate;

            return config;
        }

        public override string PlayerTips
        {
            get
            {
                return "You will need to enable \"auto-fullscreen\". There is no resume support at this time. There is no multi-part movie or folder-based playback support at this time.";
            }
        }

        public override string CommandFieldTooltip
        {
            get
            {
                return "The path to PlayerLoader.htm within the TMT installation directory.";
            }
        }

        public override IEnumerable<string> GetKnownPlayerPaths()
        {
            return GetProgramFilesPaths("ArcSoft\\TotalMedia Theatre 5\\PlayerLoader.htm");
        }
    }
}
