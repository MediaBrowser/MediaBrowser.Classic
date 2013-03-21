using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.EntityDiscovery {
    public class MediaTypeInitializationParameter : InitializationParameter {
        public MediaType MediaType { get; set; }
    }
}
