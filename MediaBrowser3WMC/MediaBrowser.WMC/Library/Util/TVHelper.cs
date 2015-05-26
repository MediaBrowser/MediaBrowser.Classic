using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Util
{
    public static class TVHelper
    {
        public static bool CreateEpisodeParents(Item item, FolderModel topParent = null)
        {
            var episode = item.BaseItem as Episode;
            if (episode == null) return false;
            //this item loaded out of context (no season/series parent) we need to derive and create them
            var mySeason = episode.Season;
            if (mySeason != null)
            {
                //found season - attach it
                episode.Parent = mySeason;
                //and create a model item for it
                item.PhysicalParent = ItemFactory.Instance.Create(mySeason) as FolderModel;
            }
            //gonna need a series too
            var mySeries = episode.Series;
            if (mySeries != null)
            {
                if (mySeason != null)
                    mySeason.Parent = mySeries;
                else
                    episode.Parent = mySeries;

                if (item.PhysicalParent == null)
                    item.PhysicalParent = ItemFactory.Instance.Create(mySeries) as FolderModel;
                else
                    item.PhysicalParent.PhysicalParent = ItemFactory.Instance.Create(mySeries) as FolderModel;

                if (topParent != null) mySeries.Parent = topParent.Folder;

                //now force the blasted images to load so they will inherit
                var ignoreList = mySeries.BackdropImages;
                ignoreList = mySeason != null ? mySeason.BackdropImages : null;
                ignoreList = episode.BackdropImages;
                var ignore = mySeries.ArtImage;
                ignore = mySeries.PrimaryImage;
                ignore = mySeries.LogoImage;
                ignore = mySeason != null ? mySeason.ArtImage : null;
                ignore = mySeason != null ? mySeason.LogoImage : null;
                ignore = episode.ArtImage;
                ignore = episode.LogoImage;
                return true;
            }
            else
            {
                //something went wrong deriving all this
                return false;
            }

        }

    }
}
