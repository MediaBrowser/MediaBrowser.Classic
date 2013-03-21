using System;

namespace MediaBrowser.Library.Events
{
    public class GenericEventArgs<TItemType> : EventArgs
    {
        public TItemType Item { get; set; }
    }
}
