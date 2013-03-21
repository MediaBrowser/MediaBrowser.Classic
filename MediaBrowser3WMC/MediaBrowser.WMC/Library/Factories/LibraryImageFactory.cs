using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.ImageManagement;
using System.Diagnostics;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Filesystem;
using System.Text.RegularExpressions;

namespace MediaBrowser.Library.Factories {

    public delegate LibraryImage ImageResolver(string path, bool canBeProcessed, BaseItem item); 

    public class LibraryImageFactory {
        public static LibraryImageFactory Instance = new LibraryImageFactory();

        private LibraryImageFactory() {
            // scrub off any image that is older than 3 weeks and is resized -- Aaack - maybe this is why images disappear on folks! -ebr
            //Async.Queue(Async.STARTUP_QUEUE, () => {
            //    var location = Kernel.Instance.MediaLocationFactory.Create(MediaBrowser.Library.Configuration.ApplicationPaths.AppImagePath) as IFolderMediaLocation;
            //    if (location != null)
            //    {
            //        foreach (var file in location.Children)
            //        {
            //            if (!(file is IFolderMediaLocation) && file.DateCreated < DateTime.Now.AddDays(-21))
            //            {
            //                if (Regex.IsMatch(file.Name, "\\d+x\\d+"))
            //                {
            //                    try
            //                    {
            //                        System.IO.File.Delete(file.Path);
            //                    }
            //                    catch (Exception ex)
            //                    {
            //                        Logger.ReportException("Failed to clean up cache file " + file.Path, ex);
            //                    }
            //                }
            //            }
            //        }
            //        ClearCache();
            //    }
            //});
        }

        Dictionary<string, LibraryImage> cache = new Dictionary<string, LibraryImage>();

        public void ClearCache() {
            lock (cache) {
                cache.Clear();
            }
        }

        public void ClearCache(string path) {
            lock (cache) {
                if (cache.ContainsKey(path)) {
                    cache.Remove(path);
                }
            }
        }

        public LibraryImage GetImage(string path)
        {
            return GetImage(path, false, null);
        }

        public LibraryImage GetImage(string path, bool canBeProcessed, BaseItem item) {
            LibraryImage image = null;
            bool cached = false;

            lock(cache){
                cached = cache.TryGetValue(path, out image);
            }

            if (!cached && image == null) {
                try {

                    foreach (var resolver in Kernel.Instance.ImageResolvers) {
                        image = resolver(path, canBeProcessed, item);
                        if (image != null) break;
                    }

                    if (image == null) {
                       image = new FilesystemImage();
                    }

                    image.Path = path;
                    image.Init(canBeProcessed, item);

                } catch (Exception ex) {
                    Logger.ReportException("Failed to load image: " + path + " ", ex);
                    image = null;
                }
            }

            lock (cache) {
                cache[path] = image;
            }

            return image;
        }
    }
}
