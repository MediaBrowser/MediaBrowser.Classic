using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Caching;
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

        /// <summary>
        /// Return true if we need to re-download this image on every MBC start up
        /// </summary>
        public bool ReAcquireOnStart { get; set; }

        /// <summary>
        /// If we have aquired the image at least once
        /// </summary>
        public bool AcquiredOnce { get; set; }

        public Guid Id { get { return Path.ToLower().GetMD5(); } }

        Guid OldId { get { return Path.GetMD5(); } }

        public virtual void Init(bool canBeProcessed, BaseItem item)
        {
            this.item = item;
            this.canBeProcessed = canBeProcessed;
        }

        public bool MigrateFromOldID()
        {
                return true;
        }

        bool loaded = false;

        

        private string EnsureImageCached(int width, int height)
        {
            EnsureImageCached();
            string cachedPath = ImageCache.Instance.GetImagePath(Path, width, height);
            if (cachedPath == null)
                return ResizeImage(width, height);
            else
                return cachedPath;

        }

        private string ResizeImage(int width, int height)
        {
            string sourcePath = ImageCache.Instance.GetImagePath(Path);
            
            using (System.Drawing.Bitmap bmp = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromFile(sourcePath))
            {
                double xscale = (double)width / bmp.Width;
                double yscale = (double)height / bmp.Height;
                double scale = Math.Min(xscale, yscale);
                using (System.Drawing.Bitmap newBmp = new System.Drawing.Bitmap((int)(bmp.Width * scale), (int)(bmp.Height * scale)))
                using (System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(newBmp))
                {

                    graphic.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphic.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphic.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphic.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    graphic.DrawImage(bmp, 0, 0, (int)(bmp.Width * scale), (int)(bmp.Height * scale));

                    return ImageCache.Instance.CacheImage(Path, newBmp, width, height);
                }
            }
        }

        private void EnsureImageCached()
        {
            if (loaded) return;
            lock (Lock)
            {
                try
                {
                    if (!loaded)
                    {
                        var cached = !ReAcquireOnStart && !AcquiredOnce ? ImageCache.Instance.GetImagePath(Path) : null;
                        if (cached == null)
                        {
                            AcquiredOnce = true;
                            var image = OriginalImage;
                            if (image == null)
                            {
                                Corrupt = true;
                                //if (Debugger.IsAttached) Debugger.Break();
                                return;
                            }
                            _width = image.Width;
                            _height = image.Height;
                            ImageCache.Instance.CacheImage(Path, image);
                            loaded = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.ReportException("Failed to deal with image: " + Path, e);
                    Corrupt = true;
                    loaded = true;
                }
            }
        }

        private void EnsureLoaded() {
            if (loaded) return;
            lock (Lock) {
                try {
                    if (!loaded) 
                    {
                        EnsureImageCached();
                        var cached = !ReAcquireOnStart && !AcquiredOnce ? ImageCache.Instance.GetImagePath(Path) : null;
                        if ((!loaded)  && cached!=null)
                        {
                            //Logger.ReportVerbose("=================== Image {0} obtained from local cache.", Path);
                            
                            //Need to load the image to determine width and height
                            using (var temp = Image.FromFile(cached))
                            {
                                _height = temp.Height;
                                _width = temp.Width;
                            }
                        }

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
            EnsureImageCached();
            var path = ImageCache.Instance.GetImagePath(Path);
            if (String.IsNullOrEmpty(path)) this.Corrupt = true;
            return path;
        }

        public string GetLocalImagePath(int width, int height) 
        {
            if (Config.Instance.UseResizedImages)
            {
                var path = EnsureImageCached(width, height);
                if (String.IsNullOrEmpty(path)) this.Corrupt = true;
                return path;
            }
            else
            {
                return GetLocalImagePath();
            }
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
                return ImageCache.Instance.GetImagePath(Path) != null;
            }
        } 

        // will clear all local copies
        public void ClearLocalImages() {
            ImageCache.Instance.ClearCache(Path);
            loaded = false;
        }

    }
}
