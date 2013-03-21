using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Filesystem;

namespace MediaBrowser.Library.EntityDiscovery {
    public class MovieVolumeInitializationParameter : InitializationParameter {
        public List<IMediaLocation> Volumes { get; set; }
    }
}
