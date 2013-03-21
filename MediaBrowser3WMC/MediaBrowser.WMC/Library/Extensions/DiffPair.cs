using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.LinqExtensions {
    class DiffPair<T> {
        public T Value { get; set; }
        public DiffAction DiffAction { get; set; }
    }
}
