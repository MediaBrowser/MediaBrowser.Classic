using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MediaBrowser.Library
{
    public class ImageSource
    {
        private static readonly byte Version = 1;
        public string OriginalSource { get; set; }
        public string LocalSource { get; set; }
        public DateTime SourceTimestamp = DateTime.MaxValue;

        public void WriteToStream(BinaryWriter bw)
        {
            bw.Write(Version);
            bw.SafeWriteString(this.OriginalSource);
            bw.SafeWriteString(this.LocalSource);
            bw.Write(this.SourceTimestamp.Ticks);
        }

        public static ImageSource ReadFromStream(BinaryReader br)
        {
            ImageSource i = new ImageSource();
            byte v = br.ReadByte();
            i.OriginalSource = br.SafeReadString();
            i.LocalSource = br.SafeReadString();
            i.SourceTimestamp = new DateTime(br.ReadInt64());
            return i;
        }
    }
}
