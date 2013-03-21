using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library {

    public class MetadataProviderFactory {

        public bool RequiresInternet { get; private set; }
        public int Priority { get; private set; }
        public bool Slow { get; private set; }
        public Type Type { get; private set; }
        private SupportedTypeAttribute[] SupportedTypes { get; set; }

        public static MetadataProviderFactory Get<T>() where T : IMetadataProvider {
            return new MetadataProviderFactory(typeof(T));
        }  

        public MetadataProviderFactory(Type type) {
            Type = type;
            Slow = GetAttribute<SlowProviderAttribute>() != null;
            RequiresInternet = GetAttribute<RequiresInternetAttribute>() != null;
            var priorityAttribute = GetAttribute<ProviderPriorityAttribute>();
            if (priorityAttribute != null) {
                Priority = priorityAttribute.Priority;
            } else {
                Priority = int.MaxValue;
            }
            SupportedTypes = GetAttributes<SupportedTypeAttribute>().ToArray();
        }

        public bool Supports(BaseItem item) {
            bool supported = false;

            foreach (var type in SupportedTypes) {
                supported = type.IncludeInheritedTypes && type.Type.IsAssignableFrom(item.GetType());
                if (supported) break;
                supported = !type.IncludeInheritedTypes && item.GetType() == type.Type;
                if (supported) break;
            }
            return supported;
        }

        public long Order {
            get {
                long order = Priority;
                if (Slow) order += 2 * (long)Int32.MaxValue;
                if (RequiresInternet) order += Int32.MaxValue;
                return order;
            }
        }

        private T[] GetAttributes<T>() where T : Attribute {
            T[] found = null;
            var attribs = Type.GetCustomAttributes(typeof(T), false);
            if (attribs != null && attribs.Length > 0) {
                found = attribs.Select(a => (T)a).ToArray();
            }
            return found;
        }

        private T GetAttribute<T>() where T : Attribute {
            T attribute = null;
            var attribs = Type.GetCustomAttributes(typeof(T), false);
            if (attribs != null && attribs.Length == 1) {
                attribute = (T)attribs[0];
            }
            return attribute;
        }

        public IMetadataProvider Construct() {
            return (IMetadataProvider)Type.GetConstructor(new Type[] { }).Invoke(null);
        }
    }
}
