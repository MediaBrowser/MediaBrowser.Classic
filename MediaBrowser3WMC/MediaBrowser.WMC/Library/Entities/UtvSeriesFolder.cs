using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Library.Entities
{
    public class UtvSeriesFolder : SearchResultFolder
    {
        public UtvSeriesFolder(Series series, IEnumerable<Episode> children) : base(children.Cast<BaseItem>().ToList())
        {
            Name = series.Name;
            BannerImagePath = series.BannerImagePath;
            BackdropImagePaths = series.BackdropImagePaths;
            ThumbnailImagePath = series.ThumbnailImagePath;
            LogoImagePath = series.LogoImagePath;
            Id = Guid.NewGuid();
        }
    }
}
