using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Plugins.Configuration {
    [global::System.AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class LabelAttribute : Attribute {
        private string _label;

        public string Label { get { return _label; } }

        public LabelAttribute(string label) {
            this._label = label;
        }
    }
}
