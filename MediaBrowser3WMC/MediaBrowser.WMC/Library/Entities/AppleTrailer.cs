using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.ApiInteraction;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Library.Entities
{
    class AppleTrailer : Movie
    {
        /// <summary>
        /// This will override our standard playback logic and force us to stream this statically through the server
        /// This is required because the server needs to trick Apple into thinking we are QuickTime
        /// </summary>
        public override IEnumerable<string> Files
        {
            get
            {
                return new[] {Config.Instance.UseCustomStreamingUrl ? string.Format(Config.Instance.CustomStreamingUrl, Kernel.ApiClient.ServerHostName+":"+Kernel.ApiClient.ServerApiPort, ApiId) :
                    Kernel.ApiClient.GetVideoStreamUrl(new VideoStreamOptions
                                                              {
                                                                  ItemId = ApiId,
                                                                  Static = true

                                                              })};
            }
        }
    }
}
