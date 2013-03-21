using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MediaBrowser.Library.Logging {
    public abstract class ThreadedLogger : LoggerBase {


        Thread loggingThread;
        Queue<Action> queue = new Queue<Action>();
        AutoResetEvent hasNewItems = new AutoResetEvent(false);
        volatile bool terminate = false; 
        bool waiting = false;

        public ThreadedLogger() : base() {
            loggingThread = new Thread(new ThreadStart(ProcessQueue));
            loggingThread.IsBackground = true;
            loggingThread.Start();
        }


        void ProcessQueue() {
            while (!terminate) {
                waiting = true;
                hasNewItems.WaitOne(10000,true);
                waiting = false;

                Queue<Action> queueCopy;
                lock (queue) {
                    queueCopy = new Queue<Action>(queue);
                    queue.Clear();
                }

                foreach (var log in queueCopy) {
                    log();
                }
            }
        }

        public override void LogMessage(LogRow row) {
            lock (queue) {
                queue.Enqueue(() => AsyncLogMessage(row));
            }
            hasNewItems.Set();
        }

        protected abstract void AsyncLogMessage(LogRow row);
        

        public override void Flush() {
            while (!waiting) {
                Thread.Sleep(1);
            }
        }

        public override void Dispose() {
            Flush();
            terminate = true;
            hasNewItems.Set();
            base.Dispose();
        }
    }
}
