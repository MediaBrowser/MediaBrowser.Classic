using MediaBrowser.Library.Logging;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Library.Entities
{
    public class RemoteVideo : Movie
    {
        /// <summary>
        /// This will bypass our normal streaming selection logic
        /// </summary>
        public override System.Collections.Generic.IEnumerable<string> Files
        {
            get
            {
                yield return Path;
            }
        }
    }
}