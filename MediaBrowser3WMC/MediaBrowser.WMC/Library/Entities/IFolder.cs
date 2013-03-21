using System;
using System.Collections.Generic;
namespace MediaBrowser.Library.Entities {
    public interface IFolder {
        IList<BaseItem> Children { get; }
        event EventHandler<ChildrenChangedEventArgs> ChildrenChanged;
        void EnsureChildrenLoaded();
        IList<Index> IndexBy(string indexType);
        IEnumerable<BaseItem> RecursiveChildren { get; }
        Index Search(Func<BaseItem, bool> searchFunction, string name);
        void Sort(IComparer<BaseItem> sortFunction);
        void ValidateChildren();
        int UnwatchedCount { get; }
        bool Watched { set; }
    }
}
