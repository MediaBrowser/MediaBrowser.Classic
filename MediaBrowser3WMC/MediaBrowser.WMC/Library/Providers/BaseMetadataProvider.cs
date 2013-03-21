using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library.Providers {

    public abstract class BaseMetadataProvider : IMetadataProvider {

        static Dictionary<Type,MetadataProviderFactory> factoryMap = new Dictionary<Type,MetadataProviderFactory>();

        public BaseItem Item {
            get; set;
        }

        MetadataProviderFactory Factory {
            get {
                lock (factoryMap) {
                    MetadataProviderFactory factory;
                    if (!factoryMap.TryGetValue(this.GetType(), out factory)) {
                        // this is a bit naughty, dependencies should be set up in kernel 
                        factory = new MetadataProviderFactory(this.GetType());
                        factoryMap[this.GetType()] = factory;
                    }

                    return factory;
                } 
            }
        }

        public abstract void Fetch();
        public abstract bool NeedsRefresh();
        
        public virtual bool IsSlow {
            get {
                return Factory.Slow;
            }
        }

        public virtual bool RequiresInternet { 
            get {
                return Factory.RequiresInternet;
            }
        }
    }
}
