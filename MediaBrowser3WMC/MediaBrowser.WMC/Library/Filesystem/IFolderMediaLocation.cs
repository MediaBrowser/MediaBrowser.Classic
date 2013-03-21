using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser.Library.Filesystem {
    public interface IFolderMediaLocation : IMediaLocation {
        IList<IMediaLocation> Children { get; }
        /// <summary>
        /// Return a child with a specific name or null 
        /// </summary>
        IMediaLocation GetChild(string name);

        bool ContainsChild(string name);
        bool IsUnavailable(string location);
    }
}
