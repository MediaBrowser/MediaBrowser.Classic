using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library.EntityDiscovery {
    public class VodCastResolver : EntityResolver {
        public override void ResolveEntity(IMediaLocation location, out BaseItemFactory factory, out IEnumerable<InitializationParameter> setup) {
            factory = null;
            setup = null;

            if (!location.IsHidden() && location.IsVodcast()) {
                factory = BaseItemFactory<VodCast>.Instance;
            }

        }
    }
}
