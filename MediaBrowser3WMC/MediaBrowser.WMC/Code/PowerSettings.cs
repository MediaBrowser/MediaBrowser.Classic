using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MediaBrowser.Library.Logging;

namespace MediaBrowser
{
    public class PowerSettings
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
            // Legacy flag, should not be used.
            // ES_USER_PRESENT = 0x00000004
        }

        public static void PreventMonitorPowerdown()
        {
            Logger.ReportInfo("****Attempting to stop screen saver from activating");
            SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
        }

        public static void AllowMonitorPowerdown()
        {
            Logger.ReportInfo("****Removing screen saver block");
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }
    }
}

