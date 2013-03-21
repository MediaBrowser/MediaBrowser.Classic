using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaBrowser.Library.Filesystem;
using System.Net;
using System.Diagnostics;
using MediaBrowser.Library.Logging;
using System.Drawing;
using System.Drawing.Imaging;

namespace MediaBrowser.Library.ImageManagement {
    public class RemoteImage : LibraryImage {

        const int MIN_WIDTH = 10; 
        const int MIN_HEIGHT = 10; 

        protected override System.Drawing.Image OriginalImage {
            get {
                var image = DownloadUsingRetry();
                if (image != null && (image.Width < MIN_WIDTH || image.Height < MIN_HEIGHT)) {
                    image = null;
                }
                return image; 
            }
        }

        internal Image DownloadImage() {
            Logger.ReportVerbose("Fetching image: " + Path);
            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(Path);
            MemoryStream ms = new MemoryStream();
            req.Timeout = 60000;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse()) {
                Stream r = resp.GetResponseStream();
                int read = 1;
                byte[] buffer = new byte[10000];
                while (read > 0) {
                    read = r.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, read);
                }
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                return Image.FromStream(ms);
            }
        }


        internal Image DownloadUsingRetry()
        {
            int attempt = 0;
            Image image = null;
            while (attempt < 2)
            {
                try
                {
                    attempt++;
                    image = DownloadImage();
                    break;
                }
                catch (Exception e)
                {
                    Logger.ReportException("Failed to download image: " + Path, e);
                }
            }
            return image;
        }
    }
}
