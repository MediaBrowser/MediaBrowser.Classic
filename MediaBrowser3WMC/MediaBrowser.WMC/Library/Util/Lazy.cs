using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser.Library.Util {

    internal class Lazy<T> {
        
        Func<T> init;
        T result;
        bool hasValue;
        Action changeNotify;


        public Lazy(Func<T> func) {
            this.init = func;
            this.hasValue = false;
        }


        public Lazy(Func<T> func, Action changeNotify) : this(func) {
            this.changeNotify = changeNotify;
        }



        public T Value {
            get {
                bool changed = false; 
                lock (init) {
                    if (!hasValue) {
                        result = this.init();
                        hasValue = true;
                        changed = true;
                    }
                }
                if (changed && changeNotify != null) {
                    changeNotify();
                }
                return this.result;
            }
            set
            {
                lock (init) {
                    result = value;
                    hasValue = true;
                }
                if (changeNotify != null) {
                    changeNotify();
                }
            }
        }
    }
}
