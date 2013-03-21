using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Plugins.Configuration {
    [global::System.AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class ExtAttribute : Attribute {
        private string _ext;

        public string Ext { get { return _ext; } }

        public ExtAttribute(string ext) {
            this._ext = ext;
        }
    }
}
