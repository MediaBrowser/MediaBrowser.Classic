using System.Collections.Generic;

namespace MediaBrowser.Library.Playables.ExternalPlayer
{
    /// <summary>
    /// Represents a PlaybackController that is driven off of External player configuration
    /// </summary>
    public class ConfigurableExternalPlaybackController : ExternalPlaybackController
    {
        /// <summary>
        /// Gets the ExternalPlayer configuration for this instance
        /// </summary>
        public ConfigData.ExternalPlayer ExternalPlayerConfiguration
        {
            get;
            set;
        }

        public override string ControllerName
        {
            get { return ExternalPlayerConfiguration.ExternalPlayerName; }
        }

        /// <summary>
        /// Gets the launch method to use
        /// </summary>
        protected override ConfigData.ExternalPlayerLaunchType LaunchType
        {
            get
            {
                return ExternalPlayerConfiguration.LaunchType;
            }
        }

        /// <summary>
        /// Determines if the windows taskbar should be hidden during playback
        /// </summary>
        protected override bool HideTaskbar
        {
            get
            {
                return ExternalPlayerConfiguration.HideTaskbar;
            }
        }

        /// <summary>
        /// Determines if the MB splash screen should be displayed before playback
        /// </summary>
        protected override bool ShowSplashScreen
        {
            get
            {
                return ExternalPlayerConfiguration.ShowSplashScreen;
            }
        }

        /// <summary>
        /// Determines if Media Center should be minimized before playback
        /// </summary>
        protected override bool MinimizeMCE
        {
            get
            {
                return ExternalPlayerConfiguration.MinimizeMCE;
            }
        }

        /// <summary>
        /// Determines if the player can play a list of files directly from the command line without having to generate a PLS file
        /// </summary>
        protected override bool SupportsMultiFileCommandArguments
        {
            get
            {
                return ExternalPlayerConfiguration.SupportsMultiFileCommandArguments;
            }
        }

        /// <summary>
        /// Determines if PLS playlist files are supported
        /// </summary>
        protected override bool SupportsPlaylists
        {
            get
            {
                return ExternalPlayerConfiguration.SupportsPlaylists;
            }
        }

        /// <summary>
        /// Gets the path to the player's executable file
        /// </summary>
        protected override string GetCommandPath(PlayableItem playable)
        {
            return ExternalPlayerConfiguration.Command;
        }

        /// <summary>
        /// Gets list of arguments to send to the player
        /// </summary>
        protected override List<string> GetCommandArgumentsList(PlayableItem playable)
        {
            List<string> args = new List<string>();

            if (!string.IsNullOrEmpty(ExternalPlayerConfiguration.Args))
            {
                args.Add(ExternalPlayerConfiguration.Args);
            }

            return args;
        }

        protected override void StopInternal()
        {

        }

        public override void GoToFullScreen()
        {

        }

        public override bool CanPause
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override void DisplayMessage(string header, string message, int timeout)
        {
            
        }
    }
}
