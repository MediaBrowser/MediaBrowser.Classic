/* 
 * Based off: http://geekswithblogs.net/sdorman/archive/2009/01/10/reading-all-bytes-from-a-stream.aspx
 * Which is in turn base off Jon Skeet's work 
 *
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaBrowser.Library.Extensions {
    public static class StreamExtensions {


        public static byte[] ReadAllBytes(this Stream source) {

          
            byte[] readBuffer = new byte[4096];

            int totalBytesRead = 0;
            int bytesRead = 0;

            while ((bytesRead = source.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0) {
                totalBytesRead += bytesRead;

                if (totalBytesRead == readBuffer.Length) {
                    int nextByte = source.ReadByte();
                    if (nextByte != -1) {
                        byte[] temp = new byte[readBuffer.Length * 2];
                        Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                        Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                        readBuffer = temp;
                        totalBytesRead++;
                    }
                }
            }

            byte[] buffer = readBuffer;
            if (readBuffer.Length != totalBytesRead) {
                buffer = new byte[totalBytesRead];
                Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
            }
            return buffer;
           
        }
    }
}
