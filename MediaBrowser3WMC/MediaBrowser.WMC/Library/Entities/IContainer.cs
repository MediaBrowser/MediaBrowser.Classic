using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities
{
    public interface IContainer : IFolder, IShow
    {
        string Name { get; set; }
        string Overview { get; set; }
        string PrimaryImagePath { get; set; }
        string SecondaryImagePath { get; set; }
        string BannerImagePath { get; set; }
        List<string> BackdropImagePaths { get; set; }
        string DisplayMediaType { get; set; }
    }
}
