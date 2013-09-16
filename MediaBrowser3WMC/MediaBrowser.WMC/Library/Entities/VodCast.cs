using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Network;
using System.IO;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Entities {


    public class VodCast : IbnSourcedFolder, IContainer
    {
        public List<Actor> Actors { get; set; }
        public List<string> Directors { get; set; }
        public List<string> Genres { get; set; }
        public float? ImdbRating { get; set; }
        public string MpaaRating { get; set; }
        public int? RunningTime { get; set; }
        public List<string> Studios { get; set; }
        public string AspectRatio { get; set; }
        public int? ProductionYear { get; set; }
    }
}
