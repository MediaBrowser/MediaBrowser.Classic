using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Playables;
using MediaBrowser.Library.Playables.ExternalPlayer;
using MediaBrowser.Library.Playables.MpcHc;
using MediaBrowser.Library.Playables.TMT5;
using MediaBrowser.Library.Playables.VLC2;

namespace MediaBrowser.Library.Factories
{
    /// <summary>
    /// This is used to create PlayableItems
    /// </summary>
    public class PlayableItemFactory
    {
        private static PlayableItemFactory _Instance = null;
        public static PlayableItemFactory Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new PlayableItemFactory();
                }

                return _Instance;
            }
        }

        private List<Type> RegisteredTypes = new List<Type>();
        private List<Type> RegisteredExternalPlayerTypes = new List<Type>();

        private PlayableItemFactory()
        {
            if (!Application.RunningOnExtender)
            {
                // Add the externals
                RegisterExternalPlayerType<PlayableMpcHc>();
                RegisterExternalPlayerType<PlayableTMT5>();
                RegisterExternalPlayerType<PlayableTMT5AddInForWMC>();
                RegisterExternalPlayerType<PlayableVLC2>();
                RegisterExternalPlayerType<PlayableExternal>();
            }
        }

        /// <summary>
        /// Registers a new type of PlayableItem to be utilized by the Create methods
        /// </summary>
        public void RegisterType<T>()
            where T : PlayableItem, new()
        {
            RegisteredTypes.Add(typeof(T));
        }

        /// <summary>
        /// Registers a new type of PlayableExternal to be utilized by the Create methods AND show up in the extenral player section of the configurator
        /// </summary>
        public void RegisterExternalPlayerType<TPlayableExternalType>()
            where TPlayableExternalType : PlayableExternal, new()
        {
            RegisteredExternalPlayerTypes.Add(typeof(TPlayableExternalType));
        }

        /// <summary>
        /// Creates a PlayableItem based on a media path
        /// </summary>
        public PlayableItem Create(string path)
        {
            return Create(new string[] { path });
        }

        /// <summary>
        /// Creates a PlayableItem based on a list of files
        /// </summary>
        public PlayableItem Create(IEnumerable<string> paths)
        {
            PlayableItem playable = GetAllKnownPlayables().FirstOrDefault(p => p.CanPlay(paths)) ?? new PlayableInternal();

            playable.Files = paths;

            return playable;
        }

        /// <summary>
        /// Creates a PlayableItem based using the internal player
        /// </summary>
        public PlayableItem CreateForInternalPlayer(IEnumerable<string> paths)
        {
            PlayableItem playable = new PlayableInternal();

            playable.Files = paths;

            return playable;
        }

        /// <summary>
        /// Creates a PlayableItem based on a Media object
        /// </summary>
        public PlayableItem Create(Media media)
        {
            Video video = media as Video;

            bool unmountISOAfterPlayback = false;
            bool useAutoPlay = false;

            if (video != null && video.MediaType == MediaType.ISO && !CanPlayIsoDirectly(GetAllKnownPlayables(), video))
            {
                media = MountAndGetNewMedia(video);
                unmountISOAfterPlayback = true;
                useAutoPlay = Config.Instance.UseAutoPlayForIso;
            }

            PlayableItem playable = Create(new Media[] { media });

            playable.UnmountISOAfterPlayback = unmountISOAfterPlayback;
            playable.UseAutoPlay = useAutoPlay;

            return playable;
        }

        /// <summary>
        /// Creates a PlayableItem based on a list of Media objects
        /// </summary>
        public PlayableItem Create(IEnumerable<Media> mediaList)
        {
            PlayableItem playable = GetAllKnownPlayables().FirstOrDefault(p => p.CanPlay(mediaList)) ?? new PlayableInternal();

            playable.MediaItems = mediaList;

            return playable;
        }

        /// <summary>
        /// Creates a PlayableItem based on an Item
        /// </summary>
        public PlayableItem Create(Item item)
        {
            if (item.IsFolder)
            {
                return Create(item.BaseItem as Folder);
            }
            else if (item.IsPlayable)
            {
                item.EnsurePlayStateChangesBoundToUI();
                return Create(item.BaseItem as Media);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a PlayableItem based on a Folder object
        /// </summary>
        public PlayableItem Create(Folder folder)
        {
            PlayableItem playable = Create(folder.RecursiveMedia);

            playable.Folder = folder;

            return playable;
        }

        private List<PlayableItem> GetAllKnownPlayables()
        {
            List<PlayableItem> playables = new List<PlayableItem>();

            foreach (Type type in RegisteredTypes)
            {
                playables.Add(Activator.CreateInstance(type) as PlayableItem);
            }

            playables.AddRange(GetConfiguredExternalPlayerTypes());

            return playables;
        }

        private List<PlayableItem> GetConfiguredExternalPlayerTypes()
        {
            List<PlayableItem> playables = new List<PlayableItem>();

            IEnumerable<KeyValuePair<PlayableExternal, PlayableExternalConfigurator>> allPlayableExternals =
                RegisteredExternalPlayerTypes.Select(p => Activator.CreateInstance(p) as PlayableExternal)
                .Select(p => new KeyValuePair<PlayableExternal, PlayableExternalConfigurator>(p, Activator.CreateInstance(p.ConfiguratorType) as PlayableExternalConfigurator));

            // Important - need to add them in the order they appear in configuration
            foreach (ConfigData.ExternalPlayer externalPlayerConfiguration in Config.Instance.ExternalPlayers)
            {
                if (allPlayableExternals.Any(p => p.Value.ExternalPlayerName == externalPlayerConfiguration.ExternalPlayerName))
                {
                    PlayableExternal playable = allPlayableExternals.FirstOrDefault(p => p.Value.ExternalPlayerName == externalPlayerConfiguration.ExternalPlayerName).Key;

                    playable.ExternalPlayerConfiguration = externalPlayerConfiguration;

                    playables.Add(playable);
                }
            }

            return playables;
        }

        /// <summary>
        /// Gets all external players that should be exposed in the configurator
        /// </summary>
        public IEnumerable<PlayableExternalConfigurator> GetAllPlayableExternalConfigurators()
        {
            return RegisteredExternalPlayerTypes.Select(t => Activator.CreateInstance(t) as PlayableExternal).Select(p => Activator.CreateInstance(p.ConfiguratorType) as PlayableExternalConfigurator);
        }

        /// <summary>
        /// Gets an external player configurator based on the name of the external player
        /// </summary>
        public PlayableExternalConfigurator GetPlayableExternalConfiguratorByName(string name)
        {
            return GetAllPlayableExternalConfigurators().First(p => p.ExternalPlayerName == name);
        }

        /// <summary>
        /// Determines if there is a PlayableItem configured to play an ISO-based entity directly without mounting
        /// </summary>
        private bool CanPlayIsoDirectly(List<PlayableItem> allKnownPlayables, Video video)
        {
            return allKnownPlayables.Where(p => p.CanPlay(video)).Count() > 0;
        }

        /// <summary>
        /// Mounts an iso based Video and updates it's path
        /// </summary>
        private Media MountAndGetNewMedia(Video video)
        {
            string mountedPath = Application.CurrentInstance.MountISO(video.IsoFiles.First());

            // Clone it so we can modify some of it's properties
            Video clone = Serializer.Clone<Video>(video);

            clone.Path = mountedPath;

            clone.MediaType = MediaTypeResolver.DetermineType(mountedPath);
            clone.DisplayMediaType = clone.MediaType.ToString();

            // Application.AddNewlyWatched requires this to be set
            clone.Parent = video.Parent;

            return clone;
        }
    }
}
