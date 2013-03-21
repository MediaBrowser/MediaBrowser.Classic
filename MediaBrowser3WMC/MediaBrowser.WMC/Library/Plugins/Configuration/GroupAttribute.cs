using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Plugins.Configuration {
    [global::System.AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class GroupAttribute : Attribute {
        private string _group;

        public string Group { get { return _group; } }

        public GroupAttribute(string Group) {
            this._group = Group;
        }
    }
}
