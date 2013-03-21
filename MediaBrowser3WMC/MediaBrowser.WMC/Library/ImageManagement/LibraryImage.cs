using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Extensions;
using System.IO;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Entities;
using System.Drawing;
using MediaBrowser.Library.Threading;
using System.Diagnostics;

namespace MediaBrowser.Library.ImageManagement {
    public abstract class LibraryImage {

        static Dictionary<Guid, object> _locks = new Dictionary<Guid, object>();

        private object Lock {
            get {
                lock (_locks) {
                    object lck;
                    Guid id = Id;
                    if (!_locks.TryGetValue(id, out lck)) {
                        lck = new object(); 
                        _locks[id] = lck; 
                    }
                    return lck;
                }
            }
        }


        protected BaseItem item;
        bool canBeProcessed; 

        /// <summary>
        /// The raw path of this image including http:// or grab:// 
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The image is not valid, bad url or file 
        /// </summary>
        public bool Corrupt { private set; get; }

        public Guid Id { get { return Path.ToLower().GetMD5(); } }

        Guid OldId { get { return Path.GetMD5(); } }

        public virtual void Init(bool canBeProcessed, BaseItem item)
        {
            this.item = item;
            this.canBeProcessed = canBeProcessed;
        }

        public bool MigrateFromOldID()
        {
            //deprecated

            //lock (Lock)
            //{
            //    try
            //    {
            //        var info = ImageCache.Instance.GetPrimaryImage(OldId);
            //        if (info != null)
            //        {
            //            try
            //            {
            //                var image = Image.FromFile(info.Path);
            //                if (image == null)
            //                {
            //                    Logger.ReportError("Could not migrate image " + info.Path);
            //                    return false;
            //                }
            //                ImageCache.Instance.CacheImage(Id, image);
            //                image.Dispose();
            //            }
            //            catch (FileNotFoundException)
            //            {
            //                //this is okay - it may have already been migrated
            //                return false;
            //            }
            //            try
            //            {
            //                File.Delete(info.Path);
            //            }
            //            catch (Exception e)
            //            {
            //                //we tried...
            //                Logger.ReportException("Unable to delete old cache file " + info.Path, e);
            //            }
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        Logger.ReportException("Failed to migrate image " + this.Path, e);
            //        return false;
            //    }
                return true;
            //}
        }

        bool loaded = false;
        private void EnsureLoaded() {
            if (loaded) return;
            lock (Lock) {
                try {
                    if (!loaded) {
                        var info = ImageCache.Instance.GetPrimaryImageInfo(Id);
                        if (info == null) {
                            var image = OriginalImage;
                            if (image == null) {
                                Corrupt = true;
                                //if (Debugger.IsAttached) Debugger.Break();
                                return;
                            }
                            ImageCache.Instance.CacheImage(Id, ProcessImage(image));
                        }
                        info = ImageCache.Instance.GetPrimaryImageInfo(Id);
                        if (info != null) {
                            _width = info.Width;
                            _height = info.Height;
                        } else {
                            if (Debugger.IsAttached) Debugger.Break();
                            Corrupt = true;
                        }

                        Async.Queue("Validate Image Thread", () =>
                        {
                            if (info != null)
                            {
                                if (ImageOutOfDate(info.Date + TimeSpan.FromMinutes(2)))  //fudge this to account for descrepancies between systems
                                {
                                    Logger.ReportVerbose("Image out of date for " + item.Name + " mod date: " + info.Date);
                                    ClearLocalImages();
                                    EnsureLoaded(); //and cause to re-cache
                                }
                            }
                        });

                    }
                } catch (Exception e) {
                    Logger.ReportException("Failed to deal with image: " + Path, e);
                    Corrupt = true;
                } finally {
                    loaded = true;
                }
            }
        }

        protected virtual bool CacheOriginalImage {
            get {
                return true;
            }
        } 

        protected abstract Image OriginalImage {
            get;
        }

        protected virtual bool ImageOutOfDate(DateTime data) {
            return false;
        }

        /// <summary>
        /// Will ensure a local copy is cached and return the path to the caller
        /// </summary>
        /// <returns></returns>
        public string GetLocalImagePath() {
            EnsureLoaded();
            string path = ImageCache.Instance.GetImagePath(Id);
            if (String.IsNullOrEmpty(path)) this.Corrupt = true;
            return path;
        }

        public string GetLocalImagePath(int width, int height) {
            EnsureLoaded();
            string path = ImageCache.Instance.GetImagePath(Id, width, height);
            if (String.IsNullOrEmpty(path)) this.Corrupt = true;
            return path;
        }


        int _width = -1;
        int _height = -1;
        public int Width { 
            get {
                EnsureLoaded();
                return _width;
            } 
        }
       
        public int Height { 
            get {
                EnsureLoaded();
                return _height; 
            } 
        }

        public float Aspect {
            get {
                return ((float)Height) / (float)Width;;
            }
        }


        /// <summary>
        /// Will return true if the image is cached locally. 
        /// </summary>
        public bool IsCached {
            get {
                return ImageCache.Instance.GetImagePath(Id) != null;
            }
        } 

        // will clear all local copies
        public void ClearLocalImages() {
            ImageCache.Instance.ClearCache(Id);
            loaded = false;
        }


       
        System.Drawing.Image ProcessImage(System.Drawing.Image image)
        {
            if (canBeProcessed && Kernel.Instance.ImageProcessor != null && System.Threading.Thread.CurrentThread.Name != "Application") //never allow processor on app thread - just have to catch it next time
            {
                return Kernel.Instance.ImageProcessor(image, item);
            } else {
                return image;
            }
        }

        /*
        protected string ConvertRemotePathToLocal(string remotePath) {
            string localPath = remotePath;

            if (localPath.ToLower().Contains("http://"))
                localPath = localPath.Replace("http://", "");

            localPath = System.IO.Path.Combine(cachePath, localPath.Replace('/', '\\'));

            return localPath;

        }*/

    }
}
