using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser.Library
{
    static class ThreadPoolSizes
    {
        internal const int METADATA_REFRESH_THREADS = 2;
        internal const int CHILD_VERIFICATION_THREADS = 2;
        internal const int CHILD_LOAD_THREADS = 2;
        internal const int IMAGE_CACHING_THREADS = 2;
        internal const int IMAGE_RESIZE_THREADS = 2;
    }
}
