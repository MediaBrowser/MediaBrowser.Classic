using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Filesystem;
using System.IO;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Extensions;
using System.Diagnostics;

namespace MediaBrowser.Library.Factories {
    public class MediaLocationFactory : IMediaLocationFactory {

        public MediaLocationFactory() {
        }

        public IMediaLocation Create(string path) {

            //Debug.Assert(path != null); (null path okay sometimes)
            if (path == null) return null;

            if (Helper.IsShortcut(path)) {
                path = Helper.ResolveShortcut(path);
            }

            IMediaLocation location = null;
            if (Directory.Exists(path)) {
                var info = new DirectoryInfo(path).ToFileInfo();
                location = new FolderMediaLocation(info, null);
            } else if (File.Exists(path)) {
                var info = new System.IO.FileInfo(path).ToFileInfo();
                if (path.ToLower().EndsWith(".vf")) {
                    location = new VirtualFolderMediaLocation(info, null);
                } else {
                    location = new MediaLocation(info, null);
                }
            }

            return location;
        }
    }
}
