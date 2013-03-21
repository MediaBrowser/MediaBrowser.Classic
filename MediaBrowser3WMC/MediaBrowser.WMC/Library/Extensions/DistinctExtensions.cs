using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Extensions {
    public static class DistinctExtensions {
        class EqualityComparer<T, TKey> : IEqualityComparer<T> {

            Func<T, TKey> lookup;

            public EqualityComparer(Func<T, TKey> lookup) {
                this.lookup = lookup;
            }

            public bool Equals(T x, T y) {
                return lookup(x).Equals(lookup(y));
            }

            public int GetHashCode(T obj) {
                return lookup(obj).GetHashCode();
            }
        }

        public static IEnumerable<T> Distinct<T, TKey>(this IEnumerable<T> list, Func<T, TKey> lookup)  {
            // null handling perhaps ? 
            return list.Distinct(new EqualityComparer<T, TKey>(lookup));
        }


    }
}
