using System;
using System.IO;
namespace MediaBrowser.Library.ImageManagement
{
    public interface IImageCache
    {
        System.Collections.Generic.List<ImageSize> AvailableSizes(Guid id);
        string CacheImage(Guid id, System.Drawing.Image image);
        DateTime GetDate(Guid id);
        string GetImagePath(Guid id, int width, int height);
        string GetImagePath(Guid id);
        ImageInfo GetPrimaryImageInfo(Guid id);
        ImageSize GetSize(Guid id);
        MemoryStream GetImageStream(Guid id, int width);
        MemoryStream GetImageStream(Guid id);
        void DeleteResizedImages();
        void ClearCache(Guid id);
        string Path { get; }
    }
}
