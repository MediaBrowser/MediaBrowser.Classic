using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Filesystem
{
    public class FileSystemCache
    {
        protected string BasePath { get; set; }
        private const string Prefixes = "0123456789abcdef";

        public FileSystemCache(string path)
        {
            BasePath = path;
            try
            {
                if (!Directory.Exists(BasePath))
                {
                    Directory.CreateDirectory(BasePath);
                }

                foreach (var prefix in Prefixes)
                {
                    var prefixPath = Path.Combine(BasePath, prefix.ToString(CultureInfo.InvariantCulture));
                    if (!Directory.Exists(prefixPath)) Directory.CreateDirectory(prefixPath);
                }

            }
            catch (Exception e)
            {
                Logger.ReportException("Error creating file system cache {0}", e, path);
                throw;
            }
        }

        public string GetCacheFileName(string uniqueName)
        {
            if (uniqueName == null)
            {
                Logger.ReportError("Attempt to get cache file name with null name");
                return null;
            }

            return FullName(uniqueName);
        }

        private string FullName(string uniqueName)
        {
            var cacheFileName = uniqueName.GetMD5().ToString("N");
            var prefix = cacheFileName.Substring(1, 1).ToLower();
            return Path.Combine(Path.Combine(BasePath, prefix), cacheFileName);
            
        }

        public DateTime LastModified(string uniquename)
        {
            try
            {
                return File.GetLastWriteTimeUtc(FullName(uniquename));
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

    }
}
