using System;
using System.Collections.Generic;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library.Playables
{
    /// <summary>
    /// Represents a PlayableItem that uses the internal WMC player
    /// </summary>
    public class PlayableInternal : PlayableItem
    {
        /// <summary>
        /// Determines whether this PlayableItem can play a list of Media objects
        /// </summary>
        public override bool CanPlay(IEnumerable<Media> mediaList)
        {
            foreach (Media media in mediaList)
            {
                if (!CanPlay(media))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether this PlayableItem can play a Media object
        /// </summary>
        public override bool CanPlay(Media media)
        {
            // can play DVDs and normal videos
            Video video = media as Video;

            if (video == null)
            {
                return false;
            }

            return CanPlay(video.MediaType);
        }

        /// <summary>
        /// Determines whether this PlayableItem can play a file
        /// </summary>
        public override bool CanPlay(string path)
        {
            return CanPlay(MediaTypeResolver.DetermineType(path));
        }

        /// <summary>
        /// Determines whether this PlayableItem can play a MediaType
        /// </summary>
        private bool CanPlay(MediaType type)
        {
            if (type == MediaType.DVD)
            {
                return true;
            }

            if (Video.IsRippedMedia(type))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether this PlayableItem can play a list of files
        /// </summary>
        public override bool CanPlay(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                if (!CanPlay(file))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the type of PlaybackController that this Playable uses
        /// </summary>
        protected override Type PlaybackControllerType
        {
            get
            {
                return typeof(PlaybackController);
            }
        }
    }
}
