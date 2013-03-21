using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser.Library {
    internal class ItemNameComparer : IComparer<Item> {
        public ItemNameComparer() {

        }

        #region IComparer<Item> Members

        public int Compare(Item x, Item y) {
            if (x.BaseItem.Name == null)
                if (y.BaseItem.Name == null)
                    return 0;
                else
                    return 1;
            if (Config.Instance.EnableAlphanumericSorting)
                return BaseItemComparer.AlphaNumericCompare(x.BaseItem.Name, y.BaseItem.Name,StringComparison.CurrentCultureIgnoreCase);
            else
                return x.BaseItem.Name.CompareTo(y.BaseItem.Name);
        }
        #endregion
    }

}
