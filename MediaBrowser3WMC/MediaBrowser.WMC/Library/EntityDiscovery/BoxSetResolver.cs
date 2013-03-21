using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.EntityDiscovery
{
    class BoxSetResolver : EntityResolver
    {
        public override void ResolveEntity(IMediaLocation location,
            out BaseItemFactory factory,
            out IEnumerable<InitializationParameter> setup)
        {

            factory = null;
            setup = null;

            var folderLocation = location as IFolderMediaLocation;

            if (folderLocation != null && !folderLocation.IsHidden())
            {
                if (location.IsBoxSetFolder())
                {
                    factory = BaseItemFactory<BoxSet>.Instance;
                }
            }
        }
    }
}
