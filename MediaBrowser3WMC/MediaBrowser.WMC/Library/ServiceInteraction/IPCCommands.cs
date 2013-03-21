using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library
{
    /// <summary>
    /// Defines the commands our processes respond to.
    /// Defined as constant strings so they can be passed easily through pipes or whatever
    /// </summary>
    public static class IPCCommands
    {
        public const string Shutdown = "shutdown";
        public const string Restart = "restart";
        public const string ReloadKernel = "reloadkernel";
        public const string ReloadItems = "reloaditems";
        public const string CloseConnection = "closeconnection";
        public const string ReloadConfig = "reloadconfig";
        public const string CancelRefresh = "cancelrefresh";
        public const string ForceRebuild = "forcerebuild";
        public const string Migrate = "migrate";
        public const string Refresh = "refresh";

    }
}
