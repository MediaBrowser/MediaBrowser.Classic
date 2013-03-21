using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaBrowser.Library.Configuration;


namespace MediaBrowser.Library.ImageManagement
{
    class CustomImageCache : ImageCache
    {
        private static CustomImageCache _instance;
        public new static CustomImageCache Instance {
            get {
                if (_instance == null) {
                    _instance = new CustomImageCache(ApplicationPaths.CustomImagePath);
                }
                return _instance;
            }
        }

        public CustomImageCache(string path)
        {
            this.Path = path;
            LoadInfo();
        }

        protected Dictionary<Guid, string> ResourceCache = new Dictionary<Guid, string>();

        public void CacheResource(Guid id, string resourceRef)
        {
            //Logging.Logger.ReportVerbose("CustomCache Caching resource " + resourceRef);
            ResourceCache[id] = resourceRef;
        }

        public override string GetImagePath(Guid id)
        {
            if (ResourceCache.ContainsKey(id))
            {
                //Logging.Logger.ReportVerbose("CustomCache returning resource " + ResourceCache[id]);
                return ResourceCache[id];
            }
            else
            {
                string fn = base.GetImagePath(id);
                //if (fn != null) Logging.Logger.ReportVerbose("CustomCache returning file " + fn);
                return fn == null ? null : "file://"+fn;
            }
        }
    }
}
