using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Logging;
using System.Drawing;


namespace MediaBrowser.Library.ImageManagement {
    public class FilesystemImage : LibraryImage{



        /*
         bool imageIsCached;
        public override void Init() {
            base.Init();

            imageIsCached = System.IO.Path.GetPathRoot(this.Path).ToLower() != System.IO.Path.GetPathRoot(cachePath).ToLower();

        }*/


        protected override bool ImageOutOfDate(DateTime date) {
            var info = new System.IO.FileInfo(Path);
            //make sure we have valid date info because some systems seem to have troubles with this
            DateTime now = DateTime.UtcNow + TimeSpan.FromHours(1);  //fudge to account for different system times
            if (info.CreationTimeUtc > now || info.LastWriteTimeUtc > now )
            {
                //something goofy with these dates...
                MediaBrowser.Library.Logging.Logger.ReportWarning("Bad date info for image "+Path+". Create date: " + info.CreationTimeUtc + " Mod date: " + info.LastWriteTimeUtc);
                return false;
            }
            if (date < info.LastWriteTimeUtc - TimeSpan.FromMinutes(20)) Logger.ReportVerbose(Path + " has changed since last cached at " + date + ".  Original image mod time: " + info.LastWriteTimeUtc);
            return date < info.LastWriteTimeUtc - TimeSpan.FromMinutes(20); //fudge this a little to account for differing times on different filesystems
        }

        protected override System.Drawing.Image OriginalImage {
            get {
                return Image.FromFile(Path);
            }
        }

        private static DateTime Max(DateTime first, DateTime second) {
            if (first > second) return first;
            return second;
        } 
    
    }
}
