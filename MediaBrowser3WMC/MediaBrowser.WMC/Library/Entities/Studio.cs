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
        protected string ImageTag { get; set; }

          public static Guid GetStudioId(string name) {
            return ("studio" + name.Trim()).GetMD5();
        }

        public static Studio GetStudio(string name) {
            return StudioCache.GetValueOrDefault(name, new Studio {Name = name});
        }

        public Studio() {
        }

        protected bool ImageLoaded = false;

        public static void AddToCache(StudioDto dto)
        {
            lock(StudioCache)
            {
                if (!StudioCache.ContainsKey(dto.Name))
                {
                    StudioCache[dto.Name] = new Studio
                                                {
                                                    Name = dto.Name,
                                                    PrimaryImagePath = dto.HasPrimaryImage ? Kernel.ApiClient.GetImageUrl("Studios/" + dto.Name + "/Images/Primary", new ImageOptions { Tag = dto.PrimaryImageTag }, new QueryStringDictionary()) : null
                                                };
                }
            }
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
