using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library.Interfaces {


    public interface IMetadataProvider {

        BaseItem Item { get; set; }
        void Fetch();
        bool NeedsRefresh();
        bool RequiresInternet { get;  }
        bool IsSlow { get; }
    }
}
