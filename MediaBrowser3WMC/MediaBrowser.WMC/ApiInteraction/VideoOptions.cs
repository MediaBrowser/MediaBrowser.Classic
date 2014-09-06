using MediaBrowser.Model.Dlna;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Class VideoOptions.
    /// </summary>
    public class VideoOptions : AudioOptions
    {
        public int? AudioStreamIndex { get; set; }
        public int? SubtitleStreamIndex { get; set; }
    }
}