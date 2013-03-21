using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Configuration;
using System.IO;

namespace MediaBrowser.Library.Providers
{
    [SupportedType(typeof(Video))]
    public class VideoFormatProvider : BaseMetadataProvider
    {
        public override bool NeedsRefresh()
        {
            return false;  //This shouldn't change through the life of a particular item - user can force if need be
        }

        public override void Fetch()
        {
            Video video = Item as Video;
            //determine our format as it pertains to 3D
            if (Item.Path.Contains("[3D]"))
                video.VideoFormat = VideoFormat.Digital3D.ToString();
            else if (Item.Path.Contains("[SBS3D]"))
                video.VideoFormat = VideoFormat.SBS3D.ToString();
            else video.VideoFormat = VideoFormat.Standard.ToString();
        }
    }
}
