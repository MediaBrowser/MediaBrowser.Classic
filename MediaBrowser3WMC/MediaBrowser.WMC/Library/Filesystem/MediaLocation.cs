using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MediaBrowser.Library.Util;
using MediaBrowser.LibraryManagement;

namespace MediaBrowser.Library.Filesystem {
    public class MediaLocation : IMediaLocation {

        public MediaLocation(FileInfo info, IFolderMediaLocation parent) {
            this.Path = info.Path;
            this.Parent = parent;
            this.DateCreated = info.DateCreated;
            this.DateModified = info.DateModified;
            this.Attributes = info.Attributes;
            SetName();
        }

        protected virtual void SetName(){
            Name = System.IO.Path.GetFileNameWithoutExtension(Path);
        }

        public virtual string Name { get; protected set; }
        public virtual string Path { get; private set; }
        public IFolderMediaLocation Parent { get; private set; }
        public virtual DateTime DateModified {get; private set; }
        public virtual DateTime DateCreated { get; private set; }
        public virtual FileAttributes Attributes { get; private set; }

        public string Contents {
            get {
                return File.ReadAllText(Path);
            }
            set {
                File.WriteAllText(Path, value); 
            }
        }

    }
}
