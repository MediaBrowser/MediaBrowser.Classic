using System.Collections.Generic;
using MediaBrowser.Library.Filesystem;

namespace MediaBrowser.Library.Interfaces
{
    /// <summary>
    /// Add this to entities to indicate they support Trailers
    /// </summary>
    public interface ISupportsTrailers
    {
        bool ContainsTrailers { get; }
        IEnumerable<string> TrailerFiles { get; }
        IMediaLocation MediaLocation { get; }
    }
}
