using System.Collections.Generic;
using System.Net;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.Library.Threading;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities
{
    public class LocalIbnSourcedCacheFolder : LocalCacheFolder
    {
        public override string PrimaryImagePath
        {
            get { return base.PrimaryImagePath ?? (base.PrimaryImagePath = GetPrimaryImagePath()); }
            set
            {
                base.PrimaryImagePath = value;
            }
        }

        protected virtual string GetPrimaryImagePath()
        {
            if (this.Name == null) return null;

            //Look for it on the server IBN
            return Kernel.ApiClient.GetGeneralIbnImageUrl(this.Name, new ImageOptions { ImageType = ImageType.Primary });

            //Have to actually try to download it to know if it is there
            //Async.Queue("remote image download", () =>
            //                                         {
            //                                             var temp = new RemoteImage {Path = path};
            //                                             try
            //                                             {
            //                                                 temp.DownloadImage();
            //                                                 base.PrimaryImagePath = path;
            //                                             }
            //                                             catch (WebException)
            //                                             {
            //                                                 // Not there - use our default
            //                                                 base.PrimaryImagePath = DefaultPrimaryImagePath;
            //                                             }

            //                                             OnMetadataChanged(null);
            //                                         });

            //return base.PrimaryImagePath;
        }

    }
}