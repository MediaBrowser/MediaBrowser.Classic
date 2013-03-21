using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.LinqExtensions {
    static class DiffExtension {
        public static IEnumerable<DiffPair<T>> Diff<T>
                 (
                     this IEnumerable<T> original,
                     IEnumerable<T> target, 
                     IEqualityComparer<T> comparer   
                 ) {

            Dictionary<T, DiffAction> results = new Dictionary<T, DiffAction>(comparer);
            
            foreach (var item in original) {
                results[item] = DiffAction.Removed;
            }
            
            foreach (var item in target) {
                if (results.ContainsKey(item)) {
                    results[item] = DiffAction.Same;
                } else {
                    results[item] = DiffAction.Added;
                }
            }
            return results.Select(
                pair => new DiffPair<T> {
                    Value=pair.Key, 
                    DiffAction = pair.Value
                });
        }
    }
}
