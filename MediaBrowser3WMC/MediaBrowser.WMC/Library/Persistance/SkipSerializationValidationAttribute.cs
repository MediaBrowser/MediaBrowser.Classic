using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Persistance {
    [global::System.AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class SkipSerializationValidationAttribute : Attribute {
        // Skip validating size on serilization - used to keep stream backwards compatible
        public SkipSerializationValidationAttribute() {

        }

    }
}
