using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Factories;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.EntityDiscovery {
    public class MovieResolver : EntityResolver {

        public const string TrailersPath = "TRAILERS";

        int maxVideosPerMovie;
        bool searchForVideosRecursively;
        bool enableTrailerSupport; 

        public MovieResolver(int maxVideosPerMovie, bool searchForVideosRecursively, bool enableTrailerSupport) {
            this.maxVideosPerMovie = maxVideosPerMovie;
            this.searchForVideosRecursively = searchForVideosRecursively;
            this.enableTrailerSupport = enableTrailerSupport; 
        }

        public override void ResolveEntity(IMediaLocation location, 
            out BaseItemFactory factory, 
            out IEnumerable<InitializationParameter> setup) {

            factory = null;
            setup = null;
            bool isMovie = false;
            MediaType mediaType = MediaType.Unknown;
            List<IMediaLocation> volumes = null;

            if (location.IsHidden()) return;

            var folder = location as IFolderMediaLocation;
            if (folder != null && !folder.ContainsChild(FolderResolver.IGNORE_FOLDER) && folder.Name.ToUpper() != TrailersPath) {
                DetectFolderWhichIsMovie(folder, out isMovie, out mediaType, out volumes);

            } else {
                if (location.IsIso()) {
                    isMovie = true;
                    mediaType = MediaType.ISO;
                } else {
                    isMovie = location.IsVideo();
                    if (isMovie) mediaType = location.GetVideoMediaType();
                }

            }

            if (isMovie) {
                factory = BaseItemFactory<Movie>.Instance;
                setup = new List<InitializationParameter>() {
                    new MediaTypeInitializationParameter() {MediaType = mediaType}
                };

                if (volumes != null && volumes.Count > 0) {
                    (setup as List<InitializationParameter>).Add(new MovieVolumeInitializationParameter() { Volumes = volumes });
                }
            }
            
        }

        private MediaType? GetSpecialMediaType(string pathUpper) {

            MediaType? mediaType = null;

            if (pathUpper.EndsWith("VIDEO_TS") || pathUpper.EndsWith(".VOB")) {
                mediaType = MediaType.DVD;
            }

            else if (pathUpper.EndsWith("HVDVD_TS")) {
                mediaType = MediaType.HDDVD;
            }

            else if (pathUpper.EndsWith("BDMV")) {
                mediaType = MediaType.BluRay;

            }

            else if (Helper.IsIso(pathUpper)) {
                mediaType = MediaType.ISO;
            }

            return mediaType;
        }

        private void DetectFolderWhichIsMovie(IFolderMediaLocation folder, out bool isMovie, out MediaType mediaType, out List<IMediaLocation> volumes) {
            int isoCount = 0;
            var childFolders = new List<IFolderMediaLocation>();
            isMovie = false;
            mediaType = MediaType.Unknown;

            volumes = new List<IMediaLocation>();

            foreach (var child in folder.Children) {
                var pathUpper = child.Path.ToUpper();

                var tmpMediaType = GetSpecialMediaType(pathUpper);
                // DVD/ BD or ISO
                if (tmpMediaType != null) {
                    mediaType = tmpMediaType.Value;
                    if (tmpMediaType.Value == MediaType.ISO) {
                        isoCount++;
                        if (isoCount > 1) {
                            break;
                        }
                    } else {
                        isMovie = true;
                        break;
                    }
                }

                var childFolder = child as IFolderMediaLocation;
                if (enableTrailerSupport &&
                    childFolder != null &&
                    childFolder.Name.ToUpper() == TrailersPath) {
                    continue;
                }

                
                if (childFolder != null && !childFolder.IsHidden()) {
                    childFolders.Add(childFolder);
                }

                if (!child.IsHidden() && child.IsVideo()) {
                    volumes.Add(child);
                    if (volumes.Count > maxVideosPerMovie || isoCount > 0) {
                        break;
                    }
                }
            }

            if (searchForVideosRecursively && isoCount == 0) {

                foreach (var location in childFolders
                    .Select(child => ChildVideos(child))
                    .SelectMany(x => x)) {

                    // this should be refactored, but I prefer this to throwing exceptions 
                    if (location == null) {
                        // get out of here, recursive BD / DVD / ETC found 
                        return;
                    } else { 
                        // another video found
                        volumes.Add(location);
                        if (volumes.Count > maxVideosPerMovie) break;
                    }
                }

            }

            if (volumes.Count > 0 && isoCount == 0) {

                if (volumes.Count <= maxVideosPerMovie) {
                    //figure out media type from file extension (first one will win...)
                    mediaType = volumes[0].GetVideoMediaType();
                    isMovie = true;
                }
            }

            if (volumes.Count == 0 && isoCount == 1) {
                isMovie = true;
            } 

            return;
        }

        private IEnumerable<IMediaLocation> ChildVideos(IFolderMediaLocation location) {

            if (location.ContainsChild(FolderResolver.IGNORE_FOLDER) ) yield break;

            if (location.ContainsChild("mymovies.xml")) yield return null;

            if (location.ContainsChild("movie.xml")) yield return null;

            foreach (var child in location.Children) {

                // nested DVD or BD
                if (GetSpecialMediaType(child.Path.ToUpper()) != null) {
                    yield return null;
                }

                if (child.IsHidden()) continue;

                if (child.IsVideo()) { 
                    yield return child;
                }
                var folder = child as IFolderMediaLocation;
                if (folder != null) {

                    if (enableTrailerSupport &&
                       folder.Name.ToUpper() == TrailersPath) {
                        continue;
                    }

                    foreach (var grandChild in ChildVideos(folder)) {
                        yield return grandChild;  
                    } 
                }
            }
            
        }
    }
}
