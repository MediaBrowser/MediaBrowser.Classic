using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Playables.ExternalPlayer;
using MediaBrowser.LibraryManagement;
using Microsoft.Win32;

namespace MediaBrowser.Library.Playables.MpcHc
{
    /// <summary>
    /// Controls editing MPC-HC settings within the configurator
    /// </summary>
    public class MpcHcConfigurator : PlayableExternalConfigurator
    {
        /// <summary>
        /// Returns a unique name for the external player
        /// </summary>
        public override string ExternalPlayerName
        {
            get { return "MPC-HC"; }
        }

        public override ConfigData.ExternalPlayer GetDefaultConfiguration()
        {
            ConfigData.ExternalPlayer config = base.GetDefaultConfiguration();

            config.SupportsMultiFileCommandArguments = true;

            return config;
        }

        public override bool SupportsConfiguringUserSettings
        {
            get
            {
                return true;
            }
        }

        public override string PlayerTips
        {
            get
            {
                return "A number of settings need to be configured. Please click configure my player for more information.";
            }
        }

        public override IEnumerable<string> GetKnownPlayerPaths()
        {
            List<string> paths = new List<string>();

            paths.AddRange(GetProgramFilesPaths("Media Player Classic - Home Cinema\\mpc-hc.exe"));
            paths.AddRange(GetProgramFilesPaths("Media Player Classic - Home Cinema\\mpc-hc64.exe"));
            paths.AddRange(GetProgramFilesPaths("MPC-HC\\mpc-hc.exe"));
            paths.AddRange(GetProgramFilesPaths("MPC-HC\\mpc-hc64.exe"));

            return paths;
        }

        public override string ConfigureUserSettingsConfirmationMessage
        {
            get
            {
                string msg = "The following MPC-HC settings will be configured for you:\n";

                msg += "\n-Disable: Remember file position";
                msg += "\n-Disable: Remember DVD position";
                msg += "\n-Enable: Web interface on port " + MpcHcPlaybackController.HttpPort;
                msg += "\n-Enable: Use global media keys";
                msg += "\n-Enable: Don't use 'search in folder' on commands 'Skip back/forward' when only one item in playlist";
                msg += "\n-Set medium jump size to 30 seconds (for rewind/ff buttons)";
                msg += "\n-Configure basic media center remote buttons";

                msg += "\n\nWould you like to continue?";

                return msg;
            }
        }

        public override void ConfigureUserSettings(ConfigData.ExternalPlayer currentConfiguration)
        {
            string iniPath = GetIniFilePath(currentConfiguration);

            if (string.IsNullOrEmpty(iniPath))
            {
                ConfigureUserSettingsIntoRegistry();
            }
            else
            {
                ConfigureUserSettingsIntoINIFile(iniPath);
            }
        }

        /// <summary>
        /// This will be used to configure mpc settings when "store settings to ini file" is enabled
        /// </summary>
        private void ConfigureUserSettingsIntoINIFile(string iniPath)
        {
            AddCommandModSettingsToIniFile(iniPath, 19);

            Dictionary<string, object> values = new Dictionary<string, object>();

            /** NOTE: These have to be kept in sync with the embedded reg file **/

            values["AllowMultipleInstances"] = 0;
            values["KeepHistory"] = 1;
            values["RememberPlaylistItems"] = 1;
            values["Remember DVD Pos"] = 0;
            values["Remember File Pos"] = 0;
            values["SearchInDirAfterPlayBack"] = 0;
            values["DontUseSearchInFolder"] = 1;
            values["UseGlobalMedia"] = 1;
            values["EnableWebServer"] = 1;
            values["WebServerPort"] = int.Parse(MpcHcPlaybackController.HttpPort);

            // Set medium jump to 30 seconds
            values["JumpDistM"] = 30000;

            // Set large jump to 5 minutes
            values["JumpDistL"] = 300000;

            // These are unreadable, but they setup basic functions such as play, pause, stop, back, next, ff, rw, etc
            values["CommandMod0"] = "816 13 58 \"\" 5 0 13 0";
            values["CommandMod1"] = "890 3 be \"\" 5 0 0 0";
            values["CommandMod2"] = "902 b 0 \"\" 5 0 49 0";
            values["CommandMod3"] = "901 b 0 \"\" 5 0 50 0";
            values["CommandMod4"] = "904 b 27 \"\" 5 0 11 0";
            values["CommandMod5"] = "903 b 25 \"\" 5 0 12 0";
            values["CommandMod6"] = "920 b 22 \"\" 5 0 51 0";
            values["CommandMod7"] = "919 b 21 \"\" 5 0 52 0";
            values["CommandMod8"] = "907 1 0 \"\" 5 16 10 16";
            values["CommandMod9"] = "908 1 0 \"\" 5 17 9 17";
            values["CommandMod10"] = "929 1 25 \"\" 5 0 0 0";
            values["CommandMod11"] = "930 1 27 \"\" 5 0 0 0";
            values["CommandMod12"] = "931 1 26 \"\" 5 0 0 0";
            values["CommandMod13"] = "932 1 28 \"\" 5 0 0 0";
            values["CommandMod14"] = "933 1 d \"\" 5 0 0 0";
            values["CommandMod15"] = "934 1 8 \"\" 5 0 1 0";
            values["CommandMod16"] = "32778 b 49 \"\" 5 0 66057 0";
            values["CommandMod17"] = "32780 3 59 \"\" 5 0 0 0";
            values["CommandMod18"] = "32781 3 55 \"\" 5 0 0 0";
           
            Helper.SetIniFileValues(iniPath, values);
        }

        /// <summary>
        /// This will be used to configure mpc settings when "store settings to ini file" is disabled
        /// </summary>
        private void ConfigureUserSettingsIntoRegistry()
        {
            string regPath = WriteEmbeddedRegFileToTempFile();
            
            ProcessStartInfo processInfo = new ProcessStartInfo();

            processInfo.FileName = "regedit";
            processInfo.Arguments = string.Format("/s \"{0}\"", regPath);

            using (Process process = Process.Start(processInfo))
            {
                process.WaitForExit();
            }
        }

        /// <summary>
        /// Takes the embedded mpc reg patch and writes it to a temp file
        /// </summary>
        private string WriteEmbeddedRegFileToTempFile()
        {
            string resourceName = "MediaBrowser.Library.Playables.MpcHc.MpcHcSettings.reg";

            using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(resourceStream))
                {
                    string content = reader.ReadToEnd();

                    string path = Path.GetTempFileName();

                    File.WriteAllText(path, content);

                    return path;
                }
            }
        }

        /// <summary>
        /// The CommandMod ini values may not actually exist in the file.
        /// If that's the case this will add them.
        /// </summary>
        private static void AddCommandModSettingsToIniFile(string iniPath, int numMods)
        {
            List<string> lines = File.ReadAllLines(iniPath).ToList();

            NameValueCollection values = Helper.ParseIniFile(iniPath);

            string sectionName = "[Commands2]";

            if (!lines.Contains(sectionName))
            {
                lines.Add(sectionName);
            }

            int commandsIndex = lines.IndexOf(sectionName);

            for (int i = 0; i < numMods; i++)
            {
                string key = "CommandMod" + i;

                if (!values.AllKeys.Contains(key))
                {
                    string line = key + "=";

                    lines.Insert(commandsIndex + 1, line);
                }
                commandsIndex++;
            }

            File.WriteAllLines(iniPath, lines.ToArray());
        }

        /// <summary>
        /// Sets values within a registry key
        /// </summary>
        public static void SetRegistryKeyValues(RegistryKey key, Dictionary<string, object> values)
        {
            try
            {
                foreach (string valueName in values.Keys)
                {
                    key.SetValue(valueName, values[valueName]);
                }
            }
            catch (Exception ex)
            {
                Logger.ReportException("MpcHcConfigurator.SetRegistryKeyValues", ex);
            }
            
            key.Close();
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

        private static string GetIniFilePath(ConfigData.ExternalPlayer currentConfiguration)
        {
            string directory = Path.GetDirectoryName(currentConfiguration.Command);

            string path = Path.Combine(directory, "mpc-hc.ini");

            if (File.Exists(path))
            {
                return path;
            }

            path = Path.Combine(directory, "mpc-hc64.ini");

            if (File.Exists(path))
            {
                return path;
            }

            return string.Empty;
        }
    }

}