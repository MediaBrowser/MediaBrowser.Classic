using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace System.IO
{
    public static class BinaryWriterExtensions
    {
        public static void SafeWriteString(this BinaryWriter bw, string val)
        {
            bw.Write(val != null);
            if (val != null)
                bw.Write(val);
        }

        public static void Write(this BinaryWriter bw, Guid guid) {
            bw.Write(guid.ToByteArray());
        }
    }

}
