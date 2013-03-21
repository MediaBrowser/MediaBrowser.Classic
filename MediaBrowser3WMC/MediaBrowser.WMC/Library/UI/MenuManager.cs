using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library;
using MediaBrowser.Library.Entities;

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

        private string watchedText(Item item)
        {
            if (item.HaveWatched)
                return Kernel.Instance.StringData.GetString("MarkUnwatchedCMenu");
            else return Kernel.Instance.StringData.GetString("MarkWatchedCMenu");
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
            List<Type> playableItems = new List<Type>() { typeof(Movie), typeof(Episode) };
            playableItems.AddRange(Kernel.Instance.ExternalPlayableItems);
            //and the folder-type queue options
            List<Type> playableFolders = new List<Type>() { typeof(Folder), typeof(Series), typeof(Season), typeof(Index), typeof(BoxSet) };
            playableFolders.AddRange(Kernel.Instance.ExternalPlayableFolders);
            List<Type> allPlayables = new List<Type>();
            allPlayables.AddRange(playableItems);
            allPlayables.AddRange(playableFolders);

            Kernel.Instance.AddMenuItem(new ResumeMenuItem(Kernel.Instance.StringData.GetString("ResumeCMenu"), "resx://MediaBrowser/MediaBrowser.Resources/IconResume", Application.CurrentInstance.Resume, new List<MenuType>() { MenuType.Item }), 0);
            Kernel.Instance.AddMenuItem(new MenuItem(playText, "resx://MediaBrowser/MediaBrowser.Resources/IconPlay", Application.CurrentInstance.Play, allPlayables, new List<MenuType>() { MenuType.Item, MenuType.Play }), 1);
            Kernel.Instance.AddMenuItem(new MenuItem(Kernel.Instance.StringData.GetString("ShufflePlayCMenu"), "resx://MediaBrowser/MediaBrowser.Resources/IconShuffle", Application.CurrentInstance.Shuffle, playableFolders, new List<MenuType>() { MenuType.Item, MenuType.Play }), 2);
            Kernel.Instance.AddMenuItem(new MenuItem(watchedText, "resx://MediaBrowser/MediaBrowser.Resources/Tick", toggleWatched, allPlayables, new List<MenuType>() { MenuType.Item }), 3);

            Kernel.Instance.AddMenuItem(new MenuItem(Kernel.Instance.StringData.GetString("PlayAllFromHereCMenu"), "resx://MediaBrowser/MediaBrowser.Resources/IconPlay", Application.CurrentInstance.PlayFolderBeginningWithItem, new List<Type>() { typeof(Episode) }, new List<MenuType>() { MenuType.Item, MenuType.Play }), 2);
        }
    }

}
