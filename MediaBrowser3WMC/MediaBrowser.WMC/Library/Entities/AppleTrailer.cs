using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.ApiInteraction;
using MediaBrowser.Library.Playables;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Library.Entities
{
    class StreamingTrailer : Movie
    {
        /// <summary>
        /// This will override our standard playback logic and force us to stream this through the server
        /// This is required because the server needs to trick Apple into thinking we are QuickTime
        /// </summary>
        public override IEnumerable<string> Files
        {
            get { yield return PlaybackControllerHelper.BuildStreamingUrl(this, Kernel.ApiClient.GetMaxBitRate()); }
        }
    }
}
