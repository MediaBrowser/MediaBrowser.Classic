using System.Collections.Generic;
using System.IO;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Providers.TVDB;
using MediaBrowser.LibraryManagement;

namespace MediaBrowser.Library.EntityDiscovery {
    public class EpisodeResolver : EntityResolver {
        
        public override void ResolveEntity(IMediaLocation location, 
            out BaseItemFactory factory, 
            out IEnumerable<InitializationParameter> setup) 
        {
            factory = null;
            setup = null;

            if (!location.IsHidden()) {

                bool isFolder = !Path.HasExtension(location.Path);

                bool containsIfo = false;
                bool isDvd = isFolder ? IsDvd(location, out containsIfo) : false;
                bool isIso = isFolder ? false : Helper.IsIso(location.Path);
                bool isBD = isFolder ? Helper.IsBluRayFolder(location.Path, null) : false;
                
                bool isVideo = !(location is IFolderMediaLocation) &&
                    (isIso || isBD || Helper.IsVideo(location.Path) || location.IsVob());

                if ( (isDvd || isBD || isVideo ) &&
                    TVUtils.IsEpisode(location.Path)) {

                    if (isBD)
                    {
                        setup = new List<InitializationParameter>() {
                            new MediaTypeInitializationParameter() {MediaType = MediaType.BluRay}
                        };
                    }
                    else if (containsIfo || isIso) {
                        MediaType mt = isIso ? MediaType.ISO : MediaType.DVD;
                        setup = new List<InitializationParameter>() {
                            new MediaTypeInitializationParameter() {MediaType = mt}
                        };
                    }
                    else if (isVideo)
                    {
                        MediaType mt = location.GetVideoMediaType();
                        setup = new List<InitializationParameter>() {
                            new MediaTypeInitializationParameter() {MediaType = mt}
                        };
                    }

                    factory = BaseItemFactory<Episode>.Instance;
                }
            }
        }

        private bool IsDvd(IMediaLocation location, out bool containsIfo) {
            bool isDvd = false;
            containsIfo = false;

            var folder = location as IFolderMediaLocation;
            if (folder != null && folder.Children != null) {
                foreach (var item in folder.Children) {
                    isDvd |= Helper.IsVob(item.Path);
                    if (item.Path.ToUpper().EndsWith("VIDEO_TS")) {
                        isDvd = true;
                        containsIfo = true;
                    }
                    containsIfo |= Helper.IsIfo(item.Path);

                    if (isDvd && containsIfo) break;
                } 
            }
            
            return isDvd;
        }
    }
}
