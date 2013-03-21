using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace System.IO {
    public static class BinaryReaderExtensions {
        public static string SafeReadString(this BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadString();
            else
                return null;
        }

        public static Guid ReadGuid(this BinaryReader br) {
            return new Guid(br.ReadBytes(16));
        }
    }
}
