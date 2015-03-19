using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Filesystem
{
    [Obsolete]
    public static class CacheCleanUp
    {
        /// <summary>
        /// Deletes the cache files from directory with a last write time less than a given date
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <param name="minDateModified">The min date modified.</param>
        public static void DeleteCacheFilesFromDirectory(string directory, DateTime minDateModified)
        {
            Logger.ReportInfo("Clearing files older than {0} from {1}", minDateModified, directory);
            var filesToDelete = new DirectoryInfo(directory).GetFiles("*", SearchOption.AllDirectories)
                .Where(f => f.LastWriteTimeUtc < minDateModified)
                .ToList();

            foreach (var file in filesToDelete)
            {
                DeleteFile(file.FullName);

            }

        }

        private static void DeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException ex)
            {
                Logger.ReportException("Error deleting file {0}", ex, path);
            }
        }


    }
}
