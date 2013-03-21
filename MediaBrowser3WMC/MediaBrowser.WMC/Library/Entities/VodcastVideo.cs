using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Network;
using System.IO;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Filesystem;
using System.Threading;
using System.Net;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Entities {
    public class VodCastVideo : Video {

        VodCast VodCast {
            get {
                return this.Parent as VodCast;
            }
        }

        string CleanName {
            get {
                return Helper.RemoveInvalidFileChars(Name);
            } 
        }

       

        string LocalFileName {
            get {
                // we may need to make this smarter, worst case we will need to defer this to after the file starts downloading.
                return System.IO.Path.Combine(VodCast.DownloadPath, CleanName + System.IO.Path.GetExtension(Path));
            }
        }

        string PartialFileName {
            get {
                return System.IO.Path.Combine(VodCast.DownloadPath, CleanName + ".partial");
            }
        }


        bool LocalFileIsDownloaded {
            get {
                return File.Exists(LocalFileName) && !File.Exists(PartialFileName);
            }
        }

        public void EnsureLocalFileIsDownloading() {
            if (!LocalFileIsDownloaded) {
                Async.Queue("Vodcast Downloader", DownloadFile);
            }
        }

        public void DownloadFile() {
            Stream partialInfoStream = null;
            Stream downloadStream = null;
            Stream localStream = null;

            object partialLock = ProtectedFileStream.GetLock(PartialFileName);
            // someone else is handling the download, exit
            if (!Monitor.TryEnter(partialLock)) return;

            try {
                partialInfoStream = new FileStream(PartialFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                localStream = new FileStream(LocalFileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);

                long totalRead = 0;
                try {
                    BinaryReader reader = new BinaryReader(partialInfoStream);
                    totalRead = reader.ReadInt64();
                } catch {
                    // ignore
                }

                WebRequest request = HttpWebRequest.Create(Path);
                downloadStream = request.GetResponse().GetResponseStream();

                try {
                    if (totalRead > 0) {
                        downloadStream.Seek(totalRead, SeekOrigin.Begin);
                        localStream.Seek(totalRead, SeekOrigin.Begin);
                    }
                } catch {
                    // if stream supports no resume we must restart 
                    totalRead = 0;
                }

                byte[] buffer = new byte[128 * 1024];

                while (true) {
                    int read = downloadStream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    localStream.Write(buffer, 0, read);

                    partialInfoStream.Seek(0, SeekOrigin.Begin);
                    BinaryWriter bw = new BinaryWriter(partialInfoStream);
                    bw.Write(totalRead);
                }

                // at this point we are done. 
                partialInfoStream.Close();
                partialInfoStream = null;

                File.Delete(PartialFileName);

            } catch (Exception e) {
                Logger.ReportException("Failed to download podcast!", e);
            } finally {

                Monitor.Exit(partialLock);

                if (partialInfoStream != null) {
                    partialInfoStream.Dispose();
                }

                if (downloadStream != null) {
                    downloadStream.Dispose();
                }

                if (localStream != null) {
                    localStream.Dispose();
                }
            }
        }

        public override IEnumerable<string> VideoFiles {
            get {

                if (VodCast.DownloadPolicy == DownloadPolicy.Stream) {
                    yield return Path;
                } else {
                    EnsureLocalFileIsDownloading();
                    yield return LocalFileName;
                }

            }
        }

        public override bool RefreshMetadata(MediaBrowser.Library.Metadata.MetadataRefreshOptions options) {
            // do nothing
            // Metadata is assigned outside the provider framework
            return false;
        }
    }
}
