using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Extensions {
    public static class DictionaryExtensions {
        public static U GetValueOrDefault<T,U>(this Dictionary<T,U> dictionary, T key, U defaultValue) {
            U val;
            if (!dictionary.TryGetValue(key, out val)) {
                val = defaultValue;
            }
            return val;
            
        }
    }
}
