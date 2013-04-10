using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Threading;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Web;

namespace MediaBrowser.Library.Entities {
    public class Studio : BaseItem {
        protected static Dictionary<string, Studio> StudioCache = new Dictionary<string, Studio>(StringComparer.OrdinalIgnoreCase);

          public static Guid GetStudioId(string name) {
            return ("studio" + name.Trim()).GetMD5();
        }

        public static Studio GetStudio(string name) {
            //Guid id = GetStudioId(name);
            return GetOrAdd(name);
        }

        public Studio() {
        }

        protected bool ImageLoaded = false;

        protected static Studio GetOrAdd(string name)
        {
            var studio = StudioCache.GetValueOrDefault(name, null) ?? 
                new Studio
                    {
                        Name = name, 
                        PrimaryImagePath = Kernel.ApiClient.GetImageUrl("/Studios/" + name + "/Images/Primary", new ImageOptions(), new QueryStringDictionary())
                    };

            StudioCache[name] = studio;

            if (!studio.ImageLoaded)
            {
                Async.Queue("studio image load", () =>
                                                     {
                                                         studio.ImageLoaded = true;
                                                         // force the primary image to load if there is one
                                                         if (studio.PrimaryImage != null) {var ignore = studio.PrimaryImage.GetLocalImagePath();}
                                                         if (studio.PrimaryImage == null || studio.PrimaryImage.Corrupt)
                                                         {
                                                             // didn't have an image - blank out the reference
                                                             Logger.ReportVerbose("No image for studio {0}",name);
                                                             studio.PrimaryImagePath = null;
                                                         }
                                                     });
            }

            return studio;
        }

        [Persist]
        [NotSourcedFromProvider]
        string name;

        public override string Name {
            get {
                return name;
            }
            set {
                name = value;
            }
        }

        public Studio(Guid id, string name) {
            this.name = name;
            this.Id = id;
        }
    }
}
