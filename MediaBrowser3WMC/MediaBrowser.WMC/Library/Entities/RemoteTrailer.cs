using MediaBrowser.Model.Dto;

namespace MediaBrowser.Library.Entities
{
    public class RemoteTrailer : Movie
    {
        public override System.Collections.Generic.IEnumerable<string> Files
        {
            get
            {
                return new[] {Kernel.ApiClient.GetVideoStreamUrl(new VideoStreamOptions
                                                              {
                                                                  ItemId = ApiId,
                                                                  Static = true

                                                              })};
            }
        }
    }
}