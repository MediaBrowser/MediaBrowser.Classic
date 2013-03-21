using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MediaBrowser.Library.Util {
    class MutexWrapper : IDisposable {
        Mutex m;

        public MutexWrapper(Mutex m) {
            this.m = m;
            try {
                m.WaitOne();
            } catch (AbandonedMutexException) { }
        }

        #region IDisposable Members

        public void Dispose() {
            m.ReleaseMutex();
            m = null;
            GC.SuppressFinalize(this);
        }

        #endregion

        ~MutexWrapper() {
            if (m != null) {
                m.ReleaseMutex();
            }
        }
    }

}
