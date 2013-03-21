using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Extensions {
    public static class DirectoryInfoExtensions {
        public static FileInfo ToFileInfo (this System.IO.FileSystemInfo info) 
        {
            DateTime created, modified;
            try {
                created = info.CreationTimeUtc;
            } catch (Exception e) {
                Logger.ReportException("You have a bad creation file date in the system for " + info.FullName + " this can be an issue on some linux shares or if the location is unavailable", e);
                created = DateTime.MinValue;
            }
            try {
                modified = info.LastWriteTimeUtc;
            } catch (Exception e) {
                Logger.ReportException("You have a bad modification file date in the system for " + info.FullName + " this can be an issue on some linux shares or if the location is unavailable", e);
                modified = DateTime.MinValue;
            }
            return new FileInfo()
            {
                IsDirectory = info is System.IO.DirectoryInfo,
                Path = info.FullName,
                DateCreated = created,
                DateModified = modified,
                Attributes = info.Attributes
            };
        }
    }
}
