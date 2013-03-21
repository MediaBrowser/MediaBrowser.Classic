using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Plugins.Configuration {
    [global::System.AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class DefaultAttribute : Attribute {
        private object _default;

        public object Default { get { return _default; } }

        public DefaultAttribute(object _default) {
            this._default = _default;
        }
    }
}
