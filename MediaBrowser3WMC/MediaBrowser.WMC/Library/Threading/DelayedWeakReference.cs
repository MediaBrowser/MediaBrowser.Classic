using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace MediaBrowser.Library.Threading {
    public class DelayedWeakReference<T> where T : class {

        private object sync = new object();
        private T obj;
        private WeakReference delayedRef;

        public T Value {
            get {
                lock (sync) {
                    if (obj != null) {
                        return obj;
                    }
                    if (delayedRef != null) {
                        return delayedRef.Target as T;
                    }
                    return null;
                }
            }
        }

        public DelayedWeakReference(T instance, int timeout) {
            Debug.Assert(timeout > 0); 
            obj = instance;
            Async.Queue("WeakRefExpiry", () => { delayedRef = new WeakReference(obj); obj = null;  }, timeout);
        }


    }
}
