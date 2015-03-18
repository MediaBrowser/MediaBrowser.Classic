using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using Microsoft.MediaCenter.UI;
using System.IO;
using MediaBrowser.Library.Factories;
using System.Reflection;
using MediaBrowser.Library;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Filesystem;

namespace MediaBrowser.Code.ModelItems
{
    public class AsyncImageLoader
    {

        static BackgroundProcessor<Action> ImageLoadingProcessors = new BackgroundProcessor<Action>(2, action => action(), "Image loader");
        static BackgroundProcessor<Action> NetImageLoadingProcessors = new BackgroundProcessor<Action>(2, action => action(), "Net Image loader");

        Func<LibraryImage> source;
        Action afterLoad;
        Image image = null;
        Image defaultImage = null;
        Microsoft.MediaCenter.UI.Size size;
        bool doneProcessing = false;
        object sync = new object();
        LibraryImage localImage;
        string localPath;

        public Microsoft.MediaCenter.UI.Size Size
        {
            get
            {
                return size;
            }
            set
            {
                // never does anything but left for potential compatability calls
            }
        }

        public bool IsLoaded
        {
            get;
            private set;
        }

        public bool IsCorrupt
        {
            get { return localImage != null && localImage.Corrupt; }
        }

        public AsyncImageLoader(Func<LibraryImage> source, Image defaultImage, Action afterLoad)
        {
            this.source = source;
            this.afterLoad = afterLoad;
            this.IsLoaded = false;
            this.defaultImage = defaultImage;
            this.LowPriority = false;
        }

        public bool LowPriority { get; set; }

        bool queued = false;

        public Image Image
        {
            get
            {
                lock (this)
                {
                    if (image == null && source != null && !queued)
                    {
                        if (LowPriority)
                        {
                            ImageLoadingProcessors.Enqueue(() => LoadImage(Loader.NormalLoader));
                        }
                        else
                        {
                            ImageLoadingProcessors.Inject(() => LoadImage(Loader.NormalLoader));
                        }
                        queued = true;
                    }

                    if (image != null)
                    {
                        return image;
                    }
                    else
                    {
                        if (doneProcessing)
                        {
                            return defaultImage;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

        private enum Loader
        {
            SlowLoader,
            NormalLoader
        }

        private void LoadImage(Loader loader)
        {
            try
            {
                lock (sync)
                {
                    int retries = 0;
                    while (retries++ < 4 && localImage == null)
                    {
                        localImage = source();
                        if (localImage != null) break;
                        // during aggressive metadata updates - images may be blank
                        Logger.ReportInfo("Image source not available waiting...");
                        Thread.Sleep(100 * retries);
                    }

                    // if the image is invalid it may be null.
                    if (localImage != null)
                    {
                        if (loader == Loader.NormalLoader && !localImage.IsCached)
                        {
                            // we have a library image but fetching it will make a call to the original source as it is not cached so transfer to another thread pool
                            if (LowPriority)
                            {
                                NetImageLoadingProcessors.Enqueue(() => LoadImage(Loader.SlowLoader));
                            }
                            else
                            {
                                NetImageLoadingProcessors.Inject(() => LoadImage(Loader.SlowLoader));
                            }
                        }
                        else
                        {
                            // fetch a locally cached image
                            FetchImage();
                        }
                    }
                    else
                    {
                        doneProcessing = true;
                    }
                }
            }
            catch (Exception e)
            {
                // this may fail in if we are unable to write a file... its not a huge problem cause we will pick it up next time around
                Logger.ReportException("Failed to load image", e);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }


        private void FetchImage()
        {

            localPath = localImage.GetLocalImagePath();

            if (localImage.Corrupt)
            {
                Logger.ReportWarning("Image " + localPath + " is Corrupt.");
                doneProcessing = true;
                IsLoaded = true;
                if (afterLoad != null)
                {
                    afterLoad();
                }

                return;
            }

            Image newImage = null;
            
            if (newImage == null)
            {
                string imageRef = "file://" + localPath;
                newImage = new Image(imageRef);
            }

            lock (this)
            {
                image = newImage;
                {
                    size = new Size(localImage.Width, localImage.Height);
                }
                doneProcessing = true;
            }

            IsLoaded = true;

            if (afterLoad != null)
            {
                afterLoad();
            }

        }

    }
}
