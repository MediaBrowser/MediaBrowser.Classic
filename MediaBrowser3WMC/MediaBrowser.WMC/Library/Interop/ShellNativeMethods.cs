using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace MediaBrowser.Interop {
    static class ShellNativeMethods {
       [DllImport("shfolder.dll", CharSet = CharSet.Auto)]
        internal static extern int SHGetFolderPath(IntPtr hwndOwner, int nFolder, IntPtr hToken, int dwFlags, StringBuilder lpszPath);

       [DllImport("User32.dll")]
       public static extern bool
               GetLastInputInfo(ref LASTINPUTINFO plii);

       public struct LASTINPUTINFO
       {
           public uint cbSize;
           public uint dwTime;
       }
    }
}
