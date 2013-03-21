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

namespace MediaBrowser.Code.ModelItems {
    public class AsyncImageLoader {

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

        public Microsoft.MediaCenter.UI.Size Size {
            get {
                return size;
            }
            set {
                lock (this) {
                    size = value;
                    image = null;
                }
            }
        }

        public bool IsLoaded {
            get;
            private set;
        }

        public AsyncImageLoader(Func<LibraryImage> source, Image defaultImage, Action afterLoad) {
            this.source = source;
            this.afterLoad = afterLoad;
            this.IsLoaded = false;
            this.defaultImage = defaultImage;
            this.LowPriority = false;
        }

        public bool LowPriority { get; set; }

        bool queued = false; 

        public Image Image {
            get {
                lock (this) {
                    if (image == null && source != null && !queued) {
                        if (LowPriority) {
                            ImageLoadingProcessors.Enqueue(() => LoadImage(Loader.NormalLoader));
                        } else {
                            ImageLoadingProcessors.Inject(() => LoadImage(Loader.NormalLoader));
                        }
                        queued = true;
                    }

                    if (image != null) {
                        return image;
                    }
                    else {
                        if (doneProcessing) {
                            return defaultImage;
                        } else {
                            return null;
                        }
                    }
                }
            }
        }

        private enum Loader {
            SlowLoader, 
            NormalLoader
        }

        private void LoadImage(Loader loader) {
            try {
                lock (sync) {
                    LoadImageImpl(loader);
                }
            } catch (Exception e) {
                // this may fail in if we are unable to write a file... its not a huge problem cause we will pick it up next time around
                Logger.ReportException("Failed to load image", e);
                if (Debugger.IsAttached) {
                    Debugger.Break();
                }
            }
        }


        private void LoadImageImpl(Loader loader) {
            int retries = 0;   
            while (retries++ < 4 && localImage == null) {
                localImage = source();
                if (localImage != null) break;
                // during aggressive metadata updates - images may be blank
                Logger.ReportInfo("Image source not available waiting..."); 
                Thread.Sleep(100 * retries); 
            }

            // if the image is invalid it may be null.
            if (localImage != null) {

                if (loader == Loader.NormalLoader && !localImage.IsCached) {
                    if (LowPriority) {
                        NetImageLoadingProcessors.Enqueue(() => LoadImage(Loader.SlowLoader));
                    } else {
                        NetImageLoadingProcessors.Inject(() => LoadImage(Loader.SlowLoader));
                    }
                } else {

                    FetchImage();
                }
            } else {
                doneProcessing = true;
            }
        }

        private void FetchImage() {
            bool sizeIsSet = Size != null && Size.Height > 0 && Size.Width > 0;
            localPath = localImage.GetLocalImagePath();
            if (sizeIsSet) {
                localPath = localImage.GetLocalImagePath(Size.Width, Size.Height);
            }

            if (localImage.Corrupt) {
                Logger.ReportWarning("Image " + localPath + " is Corrupt.");
                doneProcessing = true;
                IsLoaded = true;
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>
                {
                    if (afterLoad != null) {
                        afterLoad();
                    }
                });
                return;
            }

          

            Image newImage = null;
            if (Kernel.Instance.ConfigData.CacheAllImagesInMemory)
            {
                //defunct code..
                //if (Kernel.Instance.ConfigData.UseSQLImageCache)
                //{
                //    Logger.ReportVerbose("Loading image (from sql): " + localPath);
                //    var imageStream = ImageCache.Instance.GetImageStream(localImage.Id, localImage.Width);
                //    //System.Drawing.Image test = System.Drawing.Image.FromStream(imageStream);
                //    //test.Save("c:\\users\\eric\\my documents\\imagetest\\" + localImage.Id + localImage.Width + ".png");
                //    //test.Dispose();
                //    newImage = (Image)ImageFromStream.Invoke(null, new object[] { null, imageStream });
                //}
                //else
                {
                    Logger.ReportVerbose("Loading image (cacheall true) : " + localPath);
                    byte[] bytes;
                    lock (ProtectedFileStream.GetLock(localPath))
                    {
                        bytes = File.ReadAllBytes(localPath);
                    }

                    MemoryStream imageStream = new MemoryStream(bytes);
                    imageStream.Position = 0;
                    newImage = Image.FromStream(imageStream, null);
                }
            }


            Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>
            {

                if (newImage == null) {
                    //Logger.ReportVerbose("Loading image : " + localPath);
                    string imageRef = "file://" + localPath;
                    newImage = new Image(imageRef);
                }

                lock (this) {
                    image = newImage;
                    if (!sizeIsSet) {
                        size = new Size(localImage.Width, localImage.Height);
                    }
                    doneProcessing = true;
                }

                IsLoaded = true;

                if (afterLoad != null) {
                    afterLoad();
                }
            });
        }

    }
}
