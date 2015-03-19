using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using MediaBrowser.Library.Filesystem;
using System.Text.RegularExpressions;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Configuration;


namespace MediaBrowser.Library.ImageManagement {

    public class ImageCache
    {
        private static ImageCache _instance;
        public static ImageCache Instance {
            get {
                return _instance ?? (_instance = new ImageCache(ApplicationPaths.AppImagePath));
            }
        }

        protected readonly FileSystemCache Cache;

        public string Path { get; protected set; }

        public ImageCache(string path) 
        {
            Path = path;
            Cache = new FileSystemCache(path);
        }

        public string GetImagePath(string id)
        {
            var fn = Cache.GetCacheFileName(id);
            return CheckExistsAndTouch(fn);
        }

        public string GetImagePath(string id, int width, int height)
        {
            var fn = Cache.GetCacheFileName(id, width, height);
            return CheckExistsAndTouch(fn);
        }

        private static string CheckExistsAndTouch(string fn)
        {
            if (File.Exists(fn))
            {
                try
                {
                    File.SetLastWriteTimeUtc(fn, DateTime.UtcNow);
                }
                catch (IOException) { } // occurs when we have just fetched the image on one thread and are accessing it on another
                return fn;
            }
            else
                return null;
        }

        public string CacheImage(string id, Image image)
        {
            var fn = Cache.GetCacheFileName(id);
            return SaveImage(image, fn);
        }

        public string CacheImage(string id, Image image, int width, int height)
        {
            string fn = Cache.GetCacheFileName(id, width, height);
            return SaveImage(image, fn);
        }

        private static string SaveImage(Image image, string fn)
        {
            try
            {

                using (var fs = ProtectedFileStream.OpenExclusiveWriter(fn))
                {
                    image.Save(fs, image.RawFormat.Equals(ImageFormat.MemoryBmp) ? ImageFormat.Png : image.RawFormat);
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error saving cache image {0}", e, fn);
                return null;
            }

            return fn;
        }

        [Obsolete("Not used by core, last modified is now updated to track usage")]
        public DateTime GetDate(string id) 
        {
            return Cache.LastModified(id);
        }

        public void ClearCache(string id) 
        {
            var filename = GetImagePath(id);
            string path = System.IO.Path.GetDirectoryName(filename);
            string file = System.IO.Path.GetFileName(filename);
            // ensure we capture any resized versions as well
            foreach (string fn in Directory.GetFiles(path, file + "*"))
            {
                try
                {
                    File.Delete(fn);
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error clearing cache file {0}", e, filename);
                }
            }
        }

        public void Clean(DateTime utcCutOff)
        {
            this.Cache.Clean(utcCutOff);
        }
        
    }
}
