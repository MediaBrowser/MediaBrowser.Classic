using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace MediaBrowser.Code.Exceptions
{
    public class ConnectionIsDownException : ApplicationException
    {
        public ConnectionIsDownException()
        {
            // Add implementation.
        }
        public ConnectionIsDownException(string message)
        {
            // Add implementation.
        }
        public ConnectionIsDownException(string message, Exception inner)
        {
            // Add implementation.
        }

        // This constructor is needed for serialization.
        protected ConnectionIsDownException(SerializationInfo info, StreamingContext context)
        {
            // Add implementation.
        }
    }
}