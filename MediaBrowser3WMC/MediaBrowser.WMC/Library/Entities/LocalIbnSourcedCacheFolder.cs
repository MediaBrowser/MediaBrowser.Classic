using System.Collections.Generic;
using System.Net;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities
{
    public class LocalIbnSourcedCacheFolder : LocalCacheFolder
    {
        private string _cacheImagePath;
        public override string PrimaryImagePath
        {
            get { return _cacheImagePath ?? (_cacheImagePath = GetPrimaryImagePath()); }
            set
            {
                base.PrimaryImagePath = value;
            }
        }

        protected string GetPrimaryImagePath()
        {
            if (base.PrimaryImagePath != null) return base.PrimaryImagePath;
            if (this.Name == null) return null;

            //Look for it on the server IBN
            var path = Kernel.ApiClient.GetGeneralIbnImageUrl(this.Name, new ImageOptions { ImageType = ImageType.Primary });

            //Have to actually try to download it to know if it is there
            var temp = new RemoteImage { Path = path };
            try
            {
                temp.DownloadImage();
                return path;
            }
            catch (WebException)
            {
                // Not there - use our default
                return DefaultPrimaryImagePath;
            }
        }

    }
}