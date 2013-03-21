using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaBrowser.Library.Filesystem;
using System.Net;
using System.Diagnostics;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.ImageManagement
{
    public class ProxyCachedRemoteImage : RemoteImage
    {

        private void DownloadImage() {
            DownloadImage(true);
        }

        public override string GetLocalImagePath()
        {
            lock (Lock) {
                string localProxyPath = ConvertRemotePathToLocal(Path);
                if (File.Exists(LocalFilename)) {
                    #region Might be handy to get proxy cache in sync, however with reinstall it clears everything away?
                    if (!File.Exists(localProxyPath)) {
                        try {
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(localProxyPath));
                            File.Copy(LocalFilename, localProxyPath);
                        }
                        catch (Exception e) {
                            Logger.ReportException("Failed to copy to proxy cache: ", e);
                        }
                    }
                    //end
                    #endregion
                    return LocalFilename;
                }
                else if (File.Exists(localProxyPath)) {
                    try {
                        File.Copy(localProxyPath, LocalFilename);
                    }
                    catch (Exception e) {
                        Logger.ReportException("Failed to copy from proxy cache: ", e);
                    }
                    return LocalFilename;
                }

                bool success = DownloadUsingRetry();
                return success ? LocalFilename : null;
            }
        }
    }
}
