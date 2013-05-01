using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities
{
    public class FavoritesCollectionFolder : LocalCacheFolder
    {
        public override bool AllowRemoteChildren
        {
            get
            {
                return false;
            }
        }

        public override string Name
        {
            get
            {
                return "Favorites";
            }
            set
            {
                base.Name = value;
            }
        }

        public override string PrimaryImagePath
        {
            get { return base.PrimaryImagePath ?? "resx://MediaBrowser/MediaBrowser.Resources/Favorites"; }
            set
            {
                base.PrimaryImagePath = value;
            }
        }

        public void Clear()
        {
            foreach (var child in Children.OfType<FavoritesTypeFolder>())
            {
                child.Clear();
            }
        }
    }
}
