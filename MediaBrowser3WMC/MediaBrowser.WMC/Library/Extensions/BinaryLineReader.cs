using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace MediaBrowser.Library.Extensions
{
    public class BinaryLineReader : BinaryReader
    {
        byte[] _buffer = new byte[1024 * 32];
        int _buffersize = 0, _bufferpos = 0;

        public BinaryLineReader(Stream s)
            : base(s, Encoding.ASCII)
        {
        }

        public override char ReadChar()
        {
            byte[] b = new byte[1];
            Read(b, 0, 1);
            return (char)b[0];
        }

        void CheckBuffer()
        {
            //buffer empty load next 
            if (_bufferpos >= _buffersize)
            {
                _bufferpos = 0;
                for (int t = 0; t < 10; t++)
                {
                    _buffersize = base.Read(_buffer, 0, _buffer.Length);
                    if (_buffersize == 0)
                    {
                        Thread.Sleep(100);
                    }
                    else break;
                }
            }
        }

        public new char PeekChar()
        {
            CheckBuffer();
            if (_bufferpos < _buffersize)
            {
                return (char)_buffer[_bufferpos];
            }
            else return (char)0;
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckBuffer();
            int cc = Math.Min(count, _buffersize - _bufferpos);
            System.Buffer.BlockCopy(_buffer, _bufferpos, buffer, 0, cc);
            _bufferpos += cc;
            return cc;
        }


        public string ReadLine()
        {
            //Readline that uses Peek, handy for different types of CRLF
            //eg  #13 #10 #10#13 #13#10, just in case sender is none compliant
            //If the sender has used a differnt CRLF this should then still work.
            string result = "";
            char b;
            while (true)
            {
                b = ReadChar();
                if (b == '\r')
                {
                    if (PeekChar() == '\n')
                    {
                        ReadChar();
                    }
                    break;
                }
                else if (b == '\n')
                {
                    if (PeekChar() == '\r')
                    {
                        ReadChar();
                    }
                    break;
                }
                else
                {
                    result = result + b;
                }
            }
            return result;
        }
    }

}
