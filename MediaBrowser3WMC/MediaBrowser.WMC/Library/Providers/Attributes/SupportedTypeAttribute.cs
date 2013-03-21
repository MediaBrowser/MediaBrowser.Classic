using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Providers.Attributes {

    public enum SubclassBehavior {
        Include, 
        DontInclude
    }

    [global::System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class SupportedTypeAttribute : Attribute {

       /// <summary>
       /// Use to mark the entity types the provider supports 
       ///  By default all inherited types will also be included
       /// </summary>
       /// <param name="type"></param>
        public SupportedTypeAttribute(Type type)
            : this(type, SubclassBehavior.Include) {
        }

        public SupportedTypeAttribute(Type type, SubclassBehavior behavior) {
            Type = type;
            IncludeInheritedTypes = behavior == SubclassBehavior.Include;
        }

        public Type Type { get; private set; }

        public bool IncludeInheritedTypes { get; private set; }

    }

}
