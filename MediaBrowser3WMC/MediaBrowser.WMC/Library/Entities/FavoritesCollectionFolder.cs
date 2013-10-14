using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class FavoritesCollectionFolder : LocalIbnSourcedFolder
    {

        public override string Name
        {
            get
            {
                return Kernel.Instance.ConfigData.FavoriteFolderName;
            }
            set
            {
                base.Name = value;
            }
        }

        public override string DefaultPrimaryImagePath
        {
            get
            {
                return "resx://MediaBrowser/MediaBrowser.Resources/Favorites";
            }
        }

        protected override string RalParentId
        {
            get { return Kernel.Instance.RootFolder.ApiId; }
        }

        public override ItemFilter[] AdditionalRalFilters
        {
            get
            {
                return new[] {ItemFilter.IsFavorite};
            }
        }

        public override string[] RalExcludeTypes
        {
            get
            {
                return new string[] {};
            }
        }

        public void Clear()
        {
            //this.ActualChildren.Clear();
            //AddChildren(new List<BaseItem> { new FavoritesTypeFolder(new string[] { "Movie", "Video", "BoxSet" }, "Movies"), new FavoritesTypeFolder(new[] { "Series", "Season", "Episode" }, "TV"), new FavoritesTypeFolder(new[] { "Audio", "MusicAlbum", "MusicArtist", "MusicVideo" }, "Music") });
            foreach (var child in Children.OfType<FavoritesTypeFolder>())
            {
                child.Clear();
            }
            OnChildrenChanged(null);
            OnQuickListChanged(null);
        }
    }
}
