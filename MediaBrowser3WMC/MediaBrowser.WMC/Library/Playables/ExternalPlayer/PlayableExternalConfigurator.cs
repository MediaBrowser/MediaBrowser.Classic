using System;
using System.Collections.Generic;
using System.IO;

namespace MediaBrowser.Library.Playables.ExternalPlayer
{
    public class PlayableExternalConfigurator
    {
        /// <summary>
        /// Returns a unique name for the external player
        /// </summary>
        public virtual string ExternalPlayerName
        {
            get { return "Generic"; }
        }

        /// <summary>
        /// Gets the default configuration that will be pre-populated into the UI of the configurator.
        /// </summary>
        public virtual ConfigData.ExternalPlayer GetDefaultConfiguration()
        {
            ConfigData.ExternalPlayer config = new ConfigData.ExternalPlayer();

            config.ExternalPlayerName = ExternalPlayerName;
            config.LaunchType = ConfigData.ExternalPlayerLaunchType.CommandLine;
            config.SupportsPlaylists = true;
            config.SupportsMultiFileCommandArguments = false;
            config.ShowSplashScreen = true;
            config.MinimizeMCE = true;
            config.Args = "{0}";

            return config;
        }

        public virtual bool SupportsConfiguringUserSettings
        {
            get
            {
                return false;
            }
        }

        public virtual string PlayerTips
        {
            get
            {
                return "If your player has settings for \"always on top\", \"auto-fullscreen\", and \"exit after stopping\", it is recommended to enable them.";
            }
        }

        public virtual string CommandFieldTooltip
        {
            get
            {
                return "The path to the player's executable file.";
            }
        }

        public virtual IEnumerable<string> GetKnownPlayerPaths()
        {
            return new List<string>();
        }

        protected IEnumerable<string> GetProgramFilesPaths(string pathSuffix)
        {
            string path1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pathSuffix);
            string path2 = Path.Combine(GetProgramFilesx86Path(), pathSuffix);

            return new string[] { path1, path2 };
        }

        private static string GetProgramFilesx86Path()
        {
            if (8 == IntPtr.Size || (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
            {
                return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            }

            return Environment.GetEnvironmentVariable("ProgramFiles");
        }

        public virtual string ConfigureUserSettingsConfirmationMessage
        {
            get
            {
                return string.Empty;
            }
        }

        public virtual bool ShowIsoDirectLaunchWarning
        {
            get
            {
                return true;
            }
        }

        public virtual string IsoDirectLaunchWarning
        {
            get
            {
                return "Selecting ISO as a media type will allow ISO's to be passed directly to the player without having to mount them. Be sure your player supports this. As of this release, MPC-HC and VLC support this, but TMT does not. Are you sure you wish to continue?";
            }
        }

        public virtual bool AllowArgumentsEditing
        {
            get
            {
                return true;
            }
        }

        public virtual void ConfigureUserSettings(ConfigData.ExternalPlayer currentConfiguration)
        {
        }
    }

}
