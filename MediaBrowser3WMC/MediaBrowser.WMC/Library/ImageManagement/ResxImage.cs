using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.ImageManagement
{
    public class ResxImage : LibraryImage
    {
        protected override System.Drawing.Image OriginalImage
        {
            get
            {
                //Logger.ReportVerbose("MBTV Loading resx image: " + Path + " / " + System.IO.Path.GetFileName(Path));
                return (System.Drawing.Image)Resources.ResourceManager.GetObject(System.IO.Path.GetFileName(Path));
            }
        }
    }
}
