using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Extensions {
    static class FileTimeExtensions {
        public static DateTime ToDateTime(this System.Runtime.InteropServices.ComTypes.FILETIME filetime) {
            long highBits = filetime.dwHighDateTime;
            highBits = highBits << 32;
            long longTime = highBits + (long)filetime.dwLowDateTime;

            // Don't crash if the date time is invalid
            if ((longTime < 0L) || (longTime > 0x24c85a5ed1c03fffL)) {
                return DateTime.MinValue;
            }

            return DateTime.FromFileTimeUtc(longTime);
         
        }
    }
}
