using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Metadata {
    [Flags]
    public enum MetadataRefreshOptions {
        
        /// <summary>
        /// By default we will do normal refresh with no force 
        /// </summary>
        Default,

        /// <summary>
        /// Only run the fast meta data providers (non internet and ones not marked slow) 
        /// </summary>
        FastOnly, 
        /// <summary>
        /// Force a refresh on all providers
        /// </summary>
        Force
    }
}
