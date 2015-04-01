using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MediaBrowser.Model.Dlna;

namespace MediaBrowser.Library.Playables
{
    public class LocalPlayer : ILocalPlayer
    {
        public bool CanAccessFile(string path)
        {
            return File.Exists(path);
        }

        public bool CanAccessDirectory(string path)
        {
            return File.Exists(path);
        }

        public bool CanAccessUrl(string url, bool requiresCustomRequestHeaders)
        {
            return !requiresCustomRequestHeaders;
        }
    }
}
