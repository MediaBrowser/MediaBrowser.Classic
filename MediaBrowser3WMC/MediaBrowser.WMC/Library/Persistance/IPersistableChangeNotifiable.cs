using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Persistance {
    public interface IPersistableChangeNotifiable {
        void OnChanged(); 
    }
}
