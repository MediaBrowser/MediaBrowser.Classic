using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Code {
    [Flags]
    public enum ItemType {
        None = 0,
        Movie = 1,
        Series = 2,
        Season = 4,
        Episode = 8,
        Folder = 16,
        VirtualFolder = 32,
        Actor = 64,
        Director = 128,
        Year = 256,
        Genre = 512,
        Studio = 768,
        Other = 1024,
        All = 2047
    }
}
