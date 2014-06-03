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
                    //Clear out on each start
                    try
                    {
                        foreach (var file in new DirectoryInfo(ApplicationPaths.CustomImagePath).GetFiles())
                        {
                            file.Delete();
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.ReportException("Error clearing custom Image path {0}", e, ApplicationPaths.CustomImagePath);
                    }
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

        public string GetImagePath(Guid id, bool includePrefix)
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
                return fn == null ? null : includePrefix ? "file://"+fn : fn;
            }
        }
    }
}
