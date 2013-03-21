using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.EntityDiscovery {
    public class FolderResolver : EntityResolver {

        static readonly string[] ignoreFolders = { "metadata", ".metadata", "$recycle.bin" };
        public const string IGNORE_FOLDER = ".ignore";

        public override void ResolveEntity(IMediaLocation location,
            out BaseItemFactory factory, 
            out IEnumerable<InitializationParameter> setup) {
        
            factory = null;
            setup = null;

            var folder = location as IFolderMediaLocation; 
            if (!(folder==null) && !folder.IsHidden() && folder.Name.ToUpper() != MovieResolver.TrailersPath)
            {
                if (folder.Children.Count > 0 && !ignoreFolders.Contains(folder.Name.ToLower())) {
                    if (!folder.ContainsChild(IGNORE_FOLDER)) { 
                        factory = BaseItemFactory<Folder>.Instance;
                    }
                }
            }

            return;
        }
    }
}
