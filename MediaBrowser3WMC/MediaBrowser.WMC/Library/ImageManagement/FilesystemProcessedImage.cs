using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Logging;
using System.Drawing;

namespace MediaBrowser.Library.ImageManagement
{
    public class FilesystemProcessedImage : FilesystemImage
    {
        protected BaseItem item;

        protected FilesystemProcessedImage() { }

        public FilesystemProcessedImage(BaseItem parentItem)
        {
            item = parentItem;
        }

        protected override void CacheImage()
        {
            try
            {
                Image data = Image.FromFile(Path);
                data = ProcessImage(data); //hook in to do something to the image now that we cached it
                data.Save(LocalFilename);
            }
            catch (Exception e)
            {
                Logger.ReportException("Failed to process image.  Path was " + Path,e);
            }
        }

        protected override Image ProcessImage(Image rootImage)
        {
            if (Kernel.Instance.ImageProcessor != null)
            {
                return Kernel.Instance.ImageProcessor(rootImage, item);
            }
            else return rootImage;
        }
    }
}
