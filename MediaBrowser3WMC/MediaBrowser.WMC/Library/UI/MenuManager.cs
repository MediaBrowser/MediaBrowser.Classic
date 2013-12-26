using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Threading;

namespace MediaBrowser.Library
{
    public class MenuManager
    {
        public MenuManager()
        {
            this.init();
        }

        private void init()
        {
            addDefaultMenuItems();
        }

        private void toggleWatched(Item item)
        {
            item.ToggleWatched();
        }

        private void toggleFavorite(Item item)
        {
            // Mark this one and return quickly
            var newValue = !item.IsFavorite;
            item.ToggleFavorite();

            // But kick off a search to be sure we get all instances marked
            Async.Queue("fav toggle", () =>
                                          {
                                              foreach (var instance in Kernel.Instance.FindItems(item.Id).Where(instance => instance != item.BaseItem))
                                              {
                                                  Logging.Logger.ReportVerbose("marking instance with parent {0}",instance.Parent.Name);
                                                  instance.IsFavorite = newValue;
                                              }

                                              Application.CurrentInstance.ClearFavorites();
                                          });

        }

        private string watchedText(Item item)
        {
            if (item.HaveWatched)
                return Kernel.Instance.StringData.GetString("MarkUnwatchedCMenu");
            else return Kernel.Instance.StringData.GetString("MarkWatchedCMenu");
        }

        private string favoriteText(Item item)
        {
            if (!item.IsFavorite)
                return Kernel.Instance.StringData.GetString("AddFavoriteCMenu");
            else return Kernel.Instance.StringData.GetString("RemoveFavoriteCMenu");
        }

        private string playText(Item item)
        {
            if (item.IsFolder)
                return Kernel.Instance.StringData.GetString("PlayAllCMenu");
            else return Kernel.Instance.StringData.GetString("Playstr");
        }

        private void addDefaultMenuItems()
        {
            //build a list of types that support the play/resume menu options so external plugin types can work with this too
            var playableItems = new List<Type>() { typeof(Movie), typeof(Episode), typeof(Song), typeof(AppleTrailer), typeof(RemoteVideo), typeof(Photo) };
            playableItems.AddRange(Kernel.Instance.ExternalPlayableItems);
            //and the folder-type queue options
            var playableFolders = new List<Type>() { typeof(Folder), typeof(Series), typeof(Season), typeof(Index), typeof(BoxSet), typeof(MusicAlbum), typeof(MusicArtist), typeof(IbnSourcedFolder), typeof(PhotoFolder), typeof(ApiGenreFolder), typeof(ApiPersonFolder), typeof(ApiYearFolder) };
            playableFolders.AddRange(Kernel.Instance.ExternalPlayableFolders);
            var allPlayables = new List<Type>();
            allPlayables.AddRange(playableItems);
            allPlayables.AddRange(playableFolders);

            Kernel.Instance.AddMenuItem(new ResumeMenuItem(Kernel.Instance.StringData.GetString("ResumeCMenu"), "resx://MediaBrowser/MediaBrowser.Resources/IconResume", Application.CurrentInstance.Resume, new List<MenuType>() { MenuType.Item }), 0);
            Kernel.Instance.AddMenuItem(new MenuItem(playText, "resx://MediaBrowser/MediaBrowser.Resources/IconPlay", Application.CurrentInstance.Play, allPlayables, new List<MenuType>() { MenuType.Item, MenuType.Play }), 1);
            Kernel.Instance.AddMenuItem(new MenuItem(Kernel.Instance.StringData.GetString("ShufflePlayCMenu"), "resx://MediaBrowser/MediaBrowser.Resources/IconShuffle", Application.CurrentInstance.Shuffle, playableFolders, new List<MenuType>() { MenuType.Item, MenuType.Play }), 2);
            Kernel.Instance.AddMenuItem(new MenuItem(watchedText, "resx://MediaBrowser/MediaBrowser.Resources/Tick", toggleWatched, allPlayables, new List<MenuType>() { MenuType.Item }), 3);
            Kernel.Instance.AddMenuItem(new MenuItem(favoriteText, "resx://MediaBrowser/MediaBrowser.Resources/IconFavorite", toggleFavorite), 4);
            Kernel.Instance.AddMenuItem(new MenuItem(Kernel.Instance.StringData.GetString("SimilarTo")+"...", "resx://MediaBrowser/MediaBrowser.Resources/Star_Full", Application.CurrentInstance.NavigateToSimilar, new List<Type> {typeof(Movie), typeof(MusicAlbum), typeof(Series)}, new List<MenuType> { MenuType.Item }),5);
            Kernel.Instance.AddMenuItem(new MenuItem("Queue All", "resx://MediaBrowser/MediaBrowser.Resources/Lines", Application.CurrentInstance.AddToQueue, new List<Type>() { typeof(MusicAlbum), typeof(MusicArtist) }, new List<MenuType>() { MenuType.Item, MenuType.Play }));
            Kernel.Instance.AddMenuItem(new MenuItem("Queue", "resx://MediaBrowser/MediaBrowser.Resources/Lines", Application.CurrentInstance.AddToQueue, new List<Type>() { typeof(Song) }, new List<MenuType>() { MenuType.Item, MenuType.Play }));

            Kernel.Instance.AddMenuItem(new MenuItem(Kernel.Instance.StringData.GetString("PlayAllFromHereCMenu"), "resx://MediaBrowser/MediaBrowser.Resources/IconPlay", Application.CurrentInstance.PlayFolderBeginningWithItem, new List<Type>() { typeof(Episode), typeof(Song) }, new List<MenuType>() { MenuType.Item, MenuType.Play }), 2);
        }
    }

}
