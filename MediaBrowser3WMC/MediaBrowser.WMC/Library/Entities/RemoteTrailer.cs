using MediaBrowser.Library.Logging;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Library.Entities
{
    public class RemoteVideo : Movie
    {
        public override System.Collections.Generic.IEnumerable<string> Files
        {
            get
            {
                //Logger.ReportVerbose("File to be played is: {0}", Path);
                //yield return Path;
                return new[] {Kernel.ApiClient.GetVideoStreamUrl(new VideoStreamOptions
                                                              {
                                                                  ItemId = ApiId,
                                                                  Static = true

                                                              })};
            }
        }
    }
}