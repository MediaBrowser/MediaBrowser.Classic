using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser.Library.Extensions {
    static class FileFormatHelpers {

        public static bool IsVirtualFolder(this string path) {
            return path.ToLower().EndsWith(".vf");
        }

        public static bool IsShortcut(this string path) {
            return path.ToLower().EndsWith(".lnk"); 
        }
    }
}
