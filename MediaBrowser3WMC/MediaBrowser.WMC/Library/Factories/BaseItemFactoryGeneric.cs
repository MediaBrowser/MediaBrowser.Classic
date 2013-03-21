using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.EntityDiscovery;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Factories {
    public class BaseItemFactory<T> : BaseItemFactory where T : BaseItem, new() {
        public static BaseItemFactory<T> Instance = new BaseItemFactory<T>();

        private BaseItemFactory() { }

        public override BaseItem CreateInstance(IMediaLocation location, IEnumerable<InitializationParameter> setup) {
            var entity = new T();
            entity.Assign(location, setup, GetId(location));
            return entity;
        }

        public Guid GetId(IMediaLocation location) {
            return (TypeName + location.Path.ToLower()).GetMD5();
        }

        private static Type MyType = typeof(T);
        private static string TypeName = MyType.FullName;

        public override Type EntityType {
            get { return MyType; }
        }
    }
}
