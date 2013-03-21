using System;
using System.Collections.Generic;
using System.IO;
namespace MediaBrowser.Library.Filesystem {
    public interface IMediaLocation {
        IFolderMediaLocation Parent { get; }
        string Path { get; }
        string Name { get; }
        string Contents { get; set; }
        DateTime DateModified { get; }
        DateTime DateCreated { get; }
        FileAttributes Attributes { get; }
    }
}
