using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Factories;

namespace MediaBrowser.Library.EntityDiscovery {
    public class ChainedEntityResolver : List<EntityResolver> {
        public void ResolveEntity(IMediaLocation location,
            out BaseItemFactory factory,
            out IEnumerable<InitializationParameter> setup) {

            factory = null;
            setup = null;

            foreach (var item in this) {
                item.ResolveEntity(location, out factory, out setup);
                if (factory != null) break;
            }
        }

        public Type ResolveType(IMediaLocation location) {
            
            BaseItemFactory factory;
            IEnumerable<InitializationParameter> setup;
            ResolveEntity(location, out factory, out setup);
            return factory == null ?  null : factory.EntityType;
            
        }
    }
}
