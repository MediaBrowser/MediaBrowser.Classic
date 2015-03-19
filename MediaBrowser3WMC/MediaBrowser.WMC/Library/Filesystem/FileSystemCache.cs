using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Threading;

namespace MediaBrowser.Library.Filesystem
{
    public class FileSystemCache
    {
        protected string BasePath { get; set; }
        private const string Prefixes = "0123456789abcdef";
        Timer cleanupTimer;


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


        internal string GetCacheFileName(string id, int width, int height)
        {
            string name = GetCacheFileName(id);
            if (name != null)
                return name + string.Format("_{0}x{1}", width, height);
            else
                return null;
        }

        internal void Clean(DateTime utcCutOff)
        {
            try
            {
                Logger.ReportInfo("Cleaning image cache to UTC time: " + utcCutOff.ToString("HH:mm dd-MMM-yy"));
                foreach (string folder in Directory.GetDirectories(this.BasePath))
                    CleanFolder(folder, utcCutOff);
            }
            catch (Exception e)
            {
                Logger.ReportError("Error cleaning cache: " + e.Message);
            }
        }

        private void CleanFolder(string path, DateTime utcCutOff)
        {
            
            foreach (string file in Directory.GetFiles(path))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < utcCutOff)
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    Logger.ReportVerbose("Unable to remove cache file {0} error:{1}", file, ex.Message);
                }
            }
        }
    }
}
