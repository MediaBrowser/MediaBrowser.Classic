using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Persistance {
    [global::System.AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class PersistAttribute : Attribute {

        // This is a positional argument
        public PersistAttribute() {
        }

        public string Name  { get; private set; }
        public bool Required { get; private set; }

    }

}
