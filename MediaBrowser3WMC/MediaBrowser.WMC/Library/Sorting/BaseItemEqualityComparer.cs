using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library
{
    class BaseItemEqualityComparer : IEqualityComparer<BaseItem>
    {
        #region IEqualityComparer<BaseItem> Members

        public bool Equals(BaseItem x, BaseItem y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(BaseItem obj)
        {
            return obj.Id.GetHashCode();
        }

        #endregion
    }
}
