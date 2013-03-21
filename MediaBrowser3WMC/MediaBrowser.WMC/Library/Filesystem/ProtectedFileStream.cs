using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Drawing;

namespace MediaBrowser.Library.Filesystem {

    // just like filestream except has some built in protection to avoid blocking.


    public class ProtectedFileStream : Stream, IDisposable {


        // Theoretically ReaderWriterLockSlim is a better choice, its just that I can not get it not to hang
        static Dictionary<string, object> fileLocks = new Dictionary<string, object>();

        const int SLEEP_TIME = 25;
        const int ATTEMPTS = 80;


        public static object GetLock(string filename) {
            object fileLock; 
            lock (fileLocks) {
                if (!fileLocks.TryGetValue(filename, out fileLock)) {
                    fileLock = new object();
                    fileLocks[filename] = fileLock;
                }
            }
            return fileLock;
        }


        FileStream stream;
        Action releaseLock;

        private ProtectedFileStream(Func<FileStream> func, Action getLock, Action releaseLock) {
            
            try {
                getLock();
                stream = GetStreamWithRetry(func, ATTEMPTS, SLEEP_TIME);
                this.releaseLock = releaseLock;
                
            } catch {
                releaseLock();
                throw; 
            }
        }


        static FileStream GetStreamWithRetry(Func<FileStream> getter, int attempts, int sleepTime) {
            FileStream fs = null;
            while (attempts > 0 && fs == null) {
                try { 
                    fs = getter();
                    break;
                } 
                catch (System.IO.IOException e) {
                    if (e is FileNotFoundException) {
                        throw;
                    }
                    // retry 
                }
                Thread.Sleep(sleepTime);
                attempts--;
            }
            if (fs == null) {
                // leak out exception
                fs = getter();
            }
            return fs;
        }

        public static ProtectedFileStream OpenExclusiveWriter(string filename) {

            var lck = GetLock(filename);

            return new ProtectedFileStream (() => 
                new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None),
                () => Monitor.Enter(lck),
                () => Monitor.Exit(lck)
                );
        }


        public static ProtectedFileStream OpenSharedReader(string filename) {

            var lck = GetLock(filename);

            return new ProtectedFileStream (() => 
                new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read),
                () => Monitor.Enter(lck),
                () => Monitor.Exit(lck)
                );
        }

        public override bool CanRead {
            get { return stream.CanRead; }
        }

        public override bool CanSeek {
            get { return stream.CanSeek; }
        }

        public override bool CanWrite {
            get { return stream.CanWrite; }
        }

        public override void Flush() {
            stream.Flush();
        }

        public override long Length {
            get { return stream.Length; }
        }

        public override long Position {
            get {
                return stream.Position;
            }
            set {
                stream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value) {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            stream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing) {
            if (disposing)
            {
                try
                {
                    if (stream != null) stream.Dispose();
                }
                finally
                {
#if (!DEBUG)
                    if (releaseLock != null) releaseLock();
#else 
                    // I want to catch double dispose in debug
                    releaseLock();
#endif
                    releaseLock = null;
                }
            }
        }

    }
}
