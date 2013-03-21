using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Persistance {
    [global::System.Serializable]
    public class SerializationException : Exception {

        public SerializationException() { }
        public SerializationException(string message) : base(message) { }
        public SerializationException(string message, Exception inner) : base(message, inner) { }
        protected SerializationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
