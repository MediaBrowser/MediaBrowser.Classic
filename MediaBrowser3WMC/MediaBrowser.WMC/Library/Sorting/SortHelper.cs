using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Sorting {
    static class SortHelper {

        public static string GetSortableName(string name) {
            if (name == null) return ""; //some items may not have name filled in properly

            string sortable = name.ToLower();
            foreach (string search in Config.Instance.SortRemoveCharactersArray) {
                sortable = sortable.Replace(search.ToLower(), string.Empty);
            }
            foreach (string search in Config.Instance.SortReplaceCharactersArray) {
                sortable = sortable.Replace(search.ToLower(), " ");
            }
            foreach (string search in Config.Instance.SortReplaceWordsArray) {
                string searchLower = search.ToLower();
                // Remove from beginning if a space follows
                if (sortable.StartsWith(searchLower + " ")) {
                    sortable = sortable.Remove(0, searchLower.Length + 1);
                }
                // Remove from middle if surrounded by spaces
                sortable = sortable.Replace(" " + searchLower + " ", " ");

                // Remove from end if followed by a space
                if (sortable.EndsWith(" " + searchLower)) {
                    sortable = sortable.Remove(sortable.Length - (searchLower.Length + 1));
                }
            }
            //sortableDescription = sortableDescription.Trim();
            return sortable.Trim();
        }
    }
}
