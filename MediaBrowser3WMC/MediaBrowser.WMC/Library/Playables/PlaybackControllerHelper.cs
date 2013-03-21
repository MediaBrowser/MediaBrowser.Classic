using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Logging;
using MediaBrowser.LibraryManagement;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.Hosting;

namespace MediaBrowser.Library.Playables
{
    /// <summary>
    /// This is just a helper class to reduce the size of PlaybackController.cs and make it easier to follow.
    /// </summary>
    public static class PlaybackControllerHelper
    {
        static MediaBrowser.Library.Transcoder transcoder;

        public static bool UseLegacyApi(PlayableItem item)
        {
            // Extenders don't support MediaCollections
            if (Application.RunningOnExtender)
            {
                return true;
            }

            int numFiles = item.FilesFormattedForPlayer.Count();

            // Use the old api when there is just one file in order to avoid the annoying ding sound after playback.
            if (numFiles == 1)
            {
                return true;
            }

            // MediaCollections have performance issues with a large number of items
            if (numFiles > 200)
            {
                return true;
            }

            if (item.HasVideo)
            {
                return false;
            }

            // No videos found, use the legacy api
            return true;
        }

        /// <summary>
        /// Retrieves the current playback item using MediaCollection properties
        /// </summary>
        public static PlayableItem GetCurrentPlaybackItemFromMediaCollection(IEnumerable<PlayableItem> allPlayableItems, MediaCollection currentMediaCollection, out int filePlaylistPosition, out int currentMediaIndex)
        {
            filePlaylistPosition = -1;
            currentMediaIndex = -1;

            MediaCollectionItem activeItem = currentMediaCollection.Count == 0 ? null : currentMediaCollection[currentMediaCollection.CurrentIndex];

            if (activeItem == null)
            {
                return null;
            }
            
            Guid playableItemId = new Guid(activeItem.FriendlyData["PlayableItemId"].ToString());
            filePlaylistPosition = int.Parse(activeItem.FriendlyData["FilePlaylistPosition"].ToString());

            object objMediaIndex = activeItem.FriendlyData["MediaIndex"];

            if (objMediaIndex != null)
            {
                currentMediaIndex = int.Parse(objMediaIndex.ToString());
            }

            return allPlayableItems.FirstOrDefault(p => p.Id == playableItemId);
        }

        /// <summary>
        /// Then playback is based on Media items, this will populate the MediaCollection using the items
        /// </summary>
        public static void PopulateMediaCollectionUsingMediaItems(PlaybackController controllerInstance, MediaCollection coll, PlayableItem playable)
        {
            int currentFileIndex = 0;
            int collectionIndex = coll.Count;
            int numItems = playable.MediaItems.Count();

            for (int mediaIndex = 0; mediaIndex < numItems; mediaIndex++)
            {
                Media media = playable.MediaItems.ElementAt(mediaIndex);

                IEnumerable<string> files = controllerInstance.GetPlayableFiles(media);

                int numFiles = files.Count();

                // Create a MediaCollectionItem for each file to play
                for (int i = 0; i < numFiles; i++)
                {
                    string path = files.ElementAt(i);

                    Dictionary<string, object> friendlyData = new Dictionary<string, object>();

                    // Embed the playlist index, since we could have multiple playlists queued up
                    // which prevents us from being able to use MediaCollection.CurrentIndex
                    friendlyData["FilePlaylistPosition"] = currentFileIndex.ToString();

                    // Embed the PlayableItemId so we can identify which one to track progress for
                    friendlyData["PlayableItemId"] = playable.Id.ToString();

                    // Embed the Media index so we can identify which one to track progress for
                    friendlyData["MediaIndex"] = mediaIndex.ToString();

                    // Set a friendly title
                    friendlyData["Title"] = media.Name;

                    coll.AddItem(path, collectionIndex, -1, string.Empty, friendlyData);

                    currentFileIndex++;
                    collectionIndex++;
                }
            }
        }

        /// <summary>
        /// When playback is based purely on file paths, this will populate the MediaCollection using the paths
        /// </summary>
        public static void PopulateMediaCollectionUsingFiles(MediaCollection coll, PlayableItem playable)
        {
            PopulateMediaCollectionUsingFiles(coll, playable, 0, playable.Files.Count());
        }

        /// <summary>
        /// When playback is based purely on file paths, this will populate the MediaCollection using the paths
        /// </summary>
        public static void PopulateMediaCollectionUsingFiles(MediaCollection coll, PlayableItem playable, int startIndex, int count)
        {
            int numFiles = playable.Files.Count();
            string idString = playable.Id.ToString();

            // Create a MediaCollectionItem for each file to play
            for (int i = startIndex; i < count; i++)
            {
                string path = playable.Files.ElementAt(i);

                Dictionary<string, object> friendlyData = new Dictionary<string, object>();

                // Embed the playlist index, since we could have multiple playlists queued up
                // which prevents us from being able to use MediaCollection.CurrentIndex
                friendlyData["FilePlaylistPosition"] = i.ToString();

                // Embed the PlayableItemId so we can identify which one to track progress for
                friendlyData["PlayableItemId"] = idString;
                
                coll.AddItem(path, i, -1, string.Empty, friendlyData);
            }
        }

        /// <summary>
        /// For Bluray folders this will return the largest m2ts file contained within. For the internal wmc player, this is the best we can do
        /// </summary>
        public static string GetBluRayPath(string path)
        {
            string folder = Path.Combine(path, "bdmv\\stream");

            string movieFile = string.Empty;
            long size = 0;

            foreach (FileInfo file in new DirectoryInfo(folder).GetFiles("*.m2ts"))
            {
                long currSize = file.Length;

                if (currSize > size)
                {
                    movieFile = file.FullName;
                    size = currSize;
                }
            }

            return movieFile;
        }
        
        public static Microsoft.MediaCenter.MediaType GetMediaType(PlayableItem playable)
        {
            if (playable.HasVideo)
            {
                return Microsoft.MediaCenter.MediaType.Video;
            }

            return Microsoft.MediaCenter.MediaType.Audio;
        }

        public static bool CallPlayMedia(MediaCenterEnvironment mediaCenterEnvironment, Microsoft.MediaCenter.MediaType type, object media, bool queue)
        {
            Logger.ReportVerbose("Calling MediaCenterEnvironment.PlayMedia");
            return mediaCenterEnvironment.PlayMedia(type, media, queue);
        }

        public static PlayableItem GetCurrentPlaybackItemUsingMetadataTitle(PlaybackController controllerInstance, IEnumerable<PlayableItem> playableItems, string metadataTitle, out int filePlaylistPosition, out int currentMediaIndex)
        {
            filePlaylistPosition = -1;
            currentMediaIndex = -1;

            metadataTitle = metadataTitle.ToLower();

            // Loop through each PlayableItem and try to find a match
            foreach (PlayableItem playable in playableItems)
            {
                if (playable.HasMediaItems)
                {
                    // The PlayableItem has Media items, so loop through each one and look for a match

                    int totalFileCount = 0;
                    int numMediaItems = playable.MediaItems.Count();

                    for (int i = 0; i < numMediaItems; i++)
                    {
                        Media media = playable.MediaItems.ElementAt(i);

                        IEnumerable<string> files = controllerInstance.GetPlayableFiles(media);

                        int index = PlaybackControllerHelper.GetIndexOfFileInPlaylist(files, metadataTitle);

                        if (index != -1)
                        {
                            filePlaylistPosition = index + totalFileCount;
                            currentMediaIndex = i;
                            return playable;
                        }

                        totalFileCount += files.Count();
                    }
                }
                else
                {
                    // There are no Media items so just find the index using the Files property
                    int index = PlaybackControllerHelper.GetIndexOfFileInPlaylist(playable.FilesFormattedForPlayer, metadataTitle);

                    if (index != -1)
                    {
                        filePlaylistPosition = index;
                        return playable;
                    }
                }
            }

            return null;
        }

        public static int GetIndexOfFileInPlaylist(IEnumerable<string> files, string metadataTitle)
        {
            metadataTitle = metadataTitle.Replace("%3f", "?").Replace("dvd:///", "dvd://");

            int numFiles = files.Count();

            for (int i = 0; i < numFiles; i++)
            {
                string file = files.ElementAt(i).ToLower();

                if (metadataTitle.EndsWith(file) || metadataTitle.EndsWith(file.Replace('\\', '/')) || metadataTitle == Path.GetFileNameWithoutExtension(file))
                {
                    return i;
                }
            }

            return -1;
        }

        public static Microsoft.MediaCenter.Extensibility.MediaType GetCurrentMediaType()
        {

            // Otherwise see if another app within wmc is currently playing (such as live tv)
            MediaExperience mce = AddInHost.Current.MediaCenterEnvironment.MediaExperience;

            // Try to access MediaExperience.Transport and get PlayState from there
            if (mce != null)
            {
                return mce.MediaType;
            }

            // At this point nothing worked, so return false
            return Microsoft.MediaCenter.Extensibility.MediaType.Unknown;

        }

        public static PlayState GetCurrentPlayState()
        {
            MediaExperience mce = AddInHost.Current.MediaCenterEnvironment.MediaExperience;

            // Try to access MediaExperience.Transport and get PlayState from there
            if (mce != null)
            {
                try
                {
                    MediaTransport transport = mce.Transport;

                    if (transport != null)
                    {
                        return transport.PlayState;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // We may not have access to the Transport if another application is playing media
                    Logger.ReportException("GetCurrentPlayState", ex);
                }

                // If we weren't able to access MediaExperience.Transport, it's likely due to another application playing media
                Microsoft.MediaCenter.Extensibility.MediaType mediaType = mce.MediaType;

                if (mediaType != Microsoft.MediaCenter.Extensibility.MediaType.Unknown)
                {
                    Logger.ReportVerbose("MediaExperience.MediaType is {0}. Assume content is playing.", mediaType);

                    return Microsoft.MediaCenter.PlayState.Playing;
                }
            }

            // At this point nothing worked, so return Undefined
            return PlayState.Undefined;

        }

        public static string GetNowPlayingTextForExternalWmcApplication()
        {
            return GetCurrentMediaType().ToString();
        }

        public static void Stop()
        {
            var transport = GetCurrentMediaTransport();

            if (transport != null)
            {
                transport.PlayRate = 0;
            }
        }

        // Cache this so we don't have to keep retrieving it
        private static FieldInfo _CheckedMediaExperienceFIeldInfo;

        /// <summary>
        /// This is a workaround for when AddInHost.Current.MediaCenterEnvironment.MediaExperience returns null
        /// </summary>
        public static MediaExperience GetMediaExperienceUsingReflection()
        {
            MediaCenterEnvironment env = AddInHost.Current.MediaCenterEnvironment;

            var mce = env.MediaExperience;

            // great window 7 has bugs, lets see if we can work around them 
            // http://mediacentersandbox.com/forums/thread/9287.aspx
            if (mce == null)
            {
                mce = env.MediaExperience;
                
                if (mce == null)
                {
                    try
                    {
                        if (_CheckedMediaExperienceFIeldInfo == null)
                        {
                            _CheckedMediaExperienceFIeldInfo = env.GetType().GetField("_checkedMediaExperience", BindingFlags.NonPublic | BindingFlags.Instance);
                        }

                        if (_CheckedMediaExperienceFIeldInfo != null)
                        {
                            _CheckedMediaExperienceFIeldInfo.SetValue(env, false);
                            mce = env.MediaExperience;
                        }

                    }
                    catch (Exception e)
                    {
                        // give up ... I do not know what to do 
                        Logger.ReportException("AddInHost.Current.MediaCenterEnvironment.MediaExperience is null", e);
                    }

                }

                if (mce == null)
                {
                    Logger.ReportVerbose("GetMediaExperienceUsingReflection was unsuccessful");
                }
                else
                {
                    Logger.ReportVerbose("GetMediaExperienceUsingReflection was successful");
                }

            }

            return mce;
        }

        /// <summary>
        /// Gets the title of the currently playing content
        /// </summary>
        public static string GetTitleOfCurrentlyPlayingMedia(MediaMetadata metadata)
        {
            if (metadata == null) return string.Empty;

            string title = string.Empty;

            // Changed this to get the "Name" property instead.  That makes it compatable with DVD playback as well.
            if (metadata.ContainsKey("Name"))
            {
                title = metadata["Name"] as string;
            }

            if (string.IsNullOrEmpty(title) || title.ToLower().EndsWith(".wpl"))
            {
                if (metadata.ContainsKey("Title"))
                {
                    title = metadata["Title"] as string;
                }

                else if (metadata.ContainsKey("Uri"))
                {
                    // Use this for audio. Will get the path to the audio file even in the context of a playlist
                    // But with video this will return the wpl file
                    title = metadata["Uri"] as string;
                }
            }

            return string.IsNullOrEmpty(title) ? string.Empty : title;
        }

        /// <summary>
        /// Gets the duration, in ticks, of the currently playing content
        /// </summary>
        public static long GetDurationOfCurrentlyPlayingMedia(MediaMetadata metadata)
        {
            if (metadata != null)
            {
                string duration = string.Empty;

                if (metadata.ContainsKey("Duration"))
                {
                    duration = metadata["Duration"] as string;
                }

                if (string.IsNullOrEmpty(duration) && metadata.ContainsKey("TrackDuration"))
                {
                    duration = metadata["TrackDuration"] as string;

                    // Found it in metadata, now parse
                    if (!string.IsNullOrEmpty(duration))
                    {
                        return TimeSpan.FromSeconds(double.Parse(duration)).Ticks;
                    }
                }

                // Found it in metadata, now parse
                if (!string.IsNullOrEmpty(duration))
                {
                    return TimeSpan.Parse(duration).Ticks;
                }
            }

            return 0;
        }

        public static void WaitForStream(MediaCenterEnvironment mce)
        {
            int i = 0;
            while ((i++ < 15) && (mce.MediaExperience.Transport.PlayState != Microsoft.MediaCenter.PlayState.Playing))
            {
                // settng the position only works once it is playing and on fast multicore machines we can get here too quick!
                Thread.Sleep(100);
            }
        }

        public static bool RequiresWPL(PlayableItem playable)
        {
            return playable.FilesFormattedForPlayer.Count() > 1 && playable.HasVideo;
        }

        public static string GetTranscodedPath(string path)
        {
            if (Helper.IsExtenderNativeVideo(path))
            {
                return path;
            }
            else
            {
                if (transcoder == null)
                {
                    transcoder = new MediaBrowser.Library.Transcoder();
                }

                string bufferpath = transcoder.BeginTranscode(path);

                // if bufferpath comes back null, that means the transcoder i) failed to start or ii) they
                // don't even have it installed
                if (string.IsNullOrEmpty(bufferpath))
                {
                    Application.DisplayDialog("Could not start transcoding process", "Transcode Error");
                    throw new Exception("Could not start transcoding process");
                }

                return bufferpath;
            }
        }

        public static string CreateWPLPlaylist(string name, IEnumerable<string> files, int startIndex)
        {

            // we need to filter out all invalid chars 
            name = new string(name
                .ToCharArray()
                .Where(e => !Path.GetInvalidFileNameChars().Contains(e))
                .ToArray());

            var playListFile = Path.Combine(ApplicationPaths.AutoPlaylistPath, name + ".wpl");


            StringWriter writer = new StringWriter();
            XmlTextWriter xml = new XmlTextWriter(writer);

            xml.Indentation = 2;
            xml.IndentChar = ' ';

            xml.WriteStartElement("smil");
            xml.WriteStartElement("body");
            xml.WriteStartElement("seq");

            for (int i = startIndex; i < files.Count(); i++)
            {
                string file = files.ElementAt(i);

                xml.WriteStartElement("media");
                xml.WriteAttributeString("src", file);
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();

            File.WriteAllText(playListFile, @"<?wpl version=""1.0""?>" + writer.ToString());

            return playListFile;
        }

        public static MediaTransport GetCurrentMediaTransport()
        {

            MediaExperience mce = AddInHost.Current.MediaCenterEnvironment.MediaExperience;

            if (mce != null)
            {
                try
                {
                    return mce.Transport;
                }
                catch (InvalidOperationException e)
                {
                    // well if we are inactive we are not allowed to get media experience ...
                    Logger.ReportException("GetCurrentMediaTransport : ", e);
                }
            }

            return null;

        }

        /// <summary>
        /// Use this to return to media browser after launching another wmc application
        /// Example: Internal WMC dvd player or audio player
        /// </summary>
        public static void ReturnToApplication(bool force)
        {
            Microsoft.MediaCenter.Hosting.ApplicationContext context = Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext;

            if (force || !context.IsForegroundApplication)
            {
                Logger.ReportVerbose("Ensuring MB is front-most app");
                context.ReturnToApplication();
            }
        }
    }
}
