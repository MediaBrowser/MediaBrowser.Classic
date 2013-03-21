using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.EntityDiscovery;

namespace MediaBrowser.Library.Providers {
    public class LocalTrailerProvider : ITrailerProvider {
       
        public IEnumerable<string> GetTrailers(ISupportsTrailers movie) {
            var folder = movie.MediaLocation as IFolderMediaLocation;
            if (folder != null && folder.ContainsChild(MovieResolver.TrailersPath)) {

                var trailers = folder.GetChild(MovieResolver.TrailersPath) as IFolderMediaLocation;
                if (trailers != null) {
                    foreach (var path in Video.GetChildVideos(trailers, new string[] { MovieResolver.TrailersPath })) {
                        yield return path;
                    }
                }
            }
        }
    }
}
