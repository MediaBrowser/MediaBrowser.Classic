using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.EntityDiscovery;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Factories {

    public abstract class BaseItemFactory {
        public abstract BaseItem CreateInstance(IMediaLocation location, IEnumerable<InitializationParameter> setup);
        public abstract Type EntityType { get; }
    }   
}
