using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities.Attributes {
    [global::System.AttributeUsage(AttributeTargets.Property | AttributeTargets.Field , Inherited = false, AllowMultiple = false)]
    public sealed class NotSourcedFromProviderAttribute : Attribute {

        public NotSourcedFromProviderAttribute() {
        }
    }

}
