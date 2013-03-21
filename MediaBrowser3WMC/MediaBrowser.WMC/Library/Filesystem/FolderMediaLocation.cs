using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using MediaBrowser.Library.Util;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Interop;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Filesystem {
    public class FolderMediaLocation : MediaLocation, IFolderMediaLocation  {

        private IFolderMediaLocation location;
        MediaBrowser.Library.Util.Lazy<IList<IMediaLocation>> children;
        MediaBrowser.Library.Util.Lazy<Dictionary<string, IMediaLocation>> index;
        protected List<string> unavailableLocations = new List<string>();

        public FolderMediaLocation(FileInfo info, IFolderMediaLocation parent)
            : this(info, parent, null) 
        {
        }

        // special constructor used by the virtual folders (allows for folder relocation)
        public FolderMediaLocation(FileInfo info, IFolderMediaLocation parent, IFolderMediaLocation location)
            : base(info, parent) {
            children = new MediaBrowser.Library.Util.Lazy<IList<IMediaLocation>>(GetChildren);
            index = new MediaBrowser.Library.Util.Lazy<Dictionary<string, IMediaLocation>>(CreateIndex); 
            if (location == null) {
                this.location = this;
            } else {
                this.location = location;
            }
        }

        public bool IsUnavailable(string location) {
            foreach(var path in unavailableLocations) {
                if (location.ToLower().StartsWith(path)) return true;
            }
            return false;
        }

        protected override void SetName()
        {
            Name = Helper.GetNameFromFile(Path);
        }

        Dictionary<string, IMediaLocation> CreateIndex() {
            // the juggling here is to workaround a situation where we have 2 children with the same name
            return children
                .Value
                .Select(item => new {Name = System.IO.Path.GetFileName(item.Path).ToLower(), Value = item})
                .Distinct(item => item.Name)
                .ToDictionary(item => item.Name, item => item.Value);
        }

        /// <summary>
        /// Will return the first child with the specific name 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IMediaLocation GetChild(string name) {
            return index.Value[name.ToLower()];
        }

        public bool ContainsChild(string name) {
            return index.Value.ContainsKey(name.ToLower());
        }

        public IList<IMediaLocation> Children {
            get {
                return children.Value;
            }
        }

        protected virtual IList<IMediaLocation> GetChildren() {
            var children = new List<IMediaLocation>();

            foreach (var file in GetFileInfos(Path)) {

                FileInfo resolved = file; 

                if (file.Path.IsShortcut()) {
                    var resolvedPath = Helper.ResolveShortcut(file.Path);
                    if (File.Exists(resolvedPath)) {
                        resolved = new System.IO.FileInfo(resolvedPath).ToFileInfo();
                    } else if (Directory.Exists(resolvedPath)) {
                        resolved = new System.IO.DirectoryInfo(resolvedPath).ToFileInfo();
                    } else {
                        continue;
                    }
                }

                if (resolved.Path.IsVirtualFolder()) {
                    children.Add(new VirtualFolderMediaLocation(resolved, location)); 
                }  
                else {
                    if (resolved.IsDirectory) {
                        children.Add(new FolderMediaLocation(resolved, location));
                    } else {
                        children.Add(new MediaLocation(resolved, location));
                    }
                }
            }

            return children;
        }


    
        static List<FileInfo> GetFileInfos(string directory) {
            IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
            FindFileApis.WIN32_FIND_DATAW findData;
            IntPtr findHandle = INVALID_HANDLE_VALUE;

            var info = new List<FileInfo>();
            try {
                findHandle = FindFileApis.FindFirstFileW(directory + @"\*", out findData);
                if (findHandle != INVALID_HANDLE_VALUE) {

                    do {
                        if (findData.cFileName == "." || findData.cFileName == "..") continue;

                        string fullpath = directory + (directory.EndsWith(@"\") ? "" : @"\") +
                              findData.cFileName;

                        bool isDir = false;

                        if ((findData.dwFileAttributes & FileAttributes.Directory) != 0) {
                            isDir = true;
                        }

                        info.Add(new FileInfo()
                        {
                            DateCreated = findData.ftCreationTime.ToDateTime(),
                            DateModified = findData.ftLastWriteTime.ToDateTime(),
                            IsDirectory = isDir,
                            Path = fullpath,
                            Attributes = findData.dwFileAttributes
                        });
                    }
                    while (FindFileApis.FindNextFile(findHandle, out findData));

                }
            } finally {
                if (findHandle != INVALID_HANDLE_VALUE) FindFileApis.FindClose(findHandle);
            }
            return info;
        }


     


      

     
    }
}
