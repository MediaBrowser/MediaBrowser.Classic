using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Network {
    public enum DownloadPolicy {
        /// <summary>
        /// Vodcast is streamed, never downloaded locally 
        /// </summary>
        Stream,
        /// <summary>
        /// Vodcast is downloaded locally once its played for the first time 
        /// </summary>
        FirstPlay,

        /// <summary>
        /// Latest vodcat is always downloaded 
        /// </summary>
        Latest
    }
}
