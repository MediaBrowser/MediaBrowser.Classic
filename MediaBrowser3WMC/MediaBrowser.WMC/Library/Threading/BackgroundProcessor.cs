using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library
{
    public class BackgroundProcessor<T> : IDisposable where T : class
    {
        LinkedList<T> list = new LinkedList<T>();
        LinkedList<T> front = new LinkedList<T>();
        private AutoResetEvent itemArrived = new AutoResetEvent(false);
        private bool disposed = false;
        private int activeThreads = 0;
        private ProcessItemHandler handler;


        /// <summary>
        /// Delegate used for firing events when items are queued
        /// </summary>
        /// <param name="item"></param>
        public delegate void ProcessItemHandler(T item);


        /// <summary>
        /// Creates a processing queue
        /// </summary>
        /// <param name="threads">number of threads to fire delegates on to process items</param>
        /// <param name="itemHandler">delegate to call</param>
        /// <param name="name">name to assign to the threads</param>
        /// <param name="supportInjection">Whether or not the queue should be created to support injection onto a high priority list which gets processed ahead of the main list</param>
        public BackgroundProcessor(int threads, ProcessItemHandler itemHandler, string name)
        {
            if (threads <= 0)
                throw new ArgumentException("threads cannot be <= zero", "threads");
            if (itemHandler == null)
                throw new ArgumentNullException("itemHandler");

            this.handler = itemHandler;
            for (int i = 0; i < threads; ++i)
            {
                Thread t = new Thread(new ThreadStart(WorkerThread));
                t.Name = name + "-Worker";
                t.IsBackground = true;
                t.Priority = ThreadPriority.Lowest;
                t.Start();
            }
        }

        public bool PullToFront(T value)
        {
            lock (list)
            {
                LinkedListNode<T> n = list.Find(value);
                bool found = n != null;
                if (found)
                {
                    list.Remove(n);
                    front.AddFirst(n);
                }
                return found;
            }
        }

        /// <summary>
        /// Adds an item to the queue
        /// </summary>
        /// <param name="value"></param>
        public void Enqueue(T value)
        {
            lock (list)
                list.AddLast(value);
            itemArrived.Set();
        }

        /// <summary>
        /// Injects an item onto a high priority queue that is processes in front of the main queue. 
        /// ProcessingQueue must have been created with the supportInjection parameter set to true.
        /// </summary>
        /// <param name="value"></param>
        public void Inject(T value)
        {
            lock (list)
                list.AddFirst(value);
            itemArrived.Set();
        }

        /// <summary>
        /// Clears all items from the queue
        /// </summary>
        public void Clear()
        {
            lock (list)
                list.Clear();
        }

        private void WorkerThread()
        {
            Interlocked.Increment(ref activeThreads);
            try
            {
                itemArrived.WaitOne();
                while (!disposed)
                {
                    while ((list.Count > 0) || (front.Count > 0))
                    {
                        try
                        {
                            T item = Dequeue();
                            if (item != null)
                                this.handler(item);
                        }
                        catch (Exception ex)
                        {
                            Logger.ReportException("Error in background processor.", ex);
                        }
                    }
                    itemArrived.WaitOne();
                }
            }
            finally
            {
                // in case the thread is aborted. (during testing) 
                Interlocked.Decrement(ref activeThreads);
            }
        }

        private T Dequeue() {
            lock (list) {
                T item = null;
                if (front.Count > 0) {
                    item = front.First.Value;
                    front.RemoveFirst();
                } else if (list.Count > 0) {
                    item = list.First.Value;
                    list.RemoveFirst();
                }
                return item;
            }
        }


        #region IDisposable Members

        /// <summary>
        /// Cleanup threads that have been running
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                Clear();
                while (activeThreads > 0)
                {
                    itemArrived.Set();
                    Thread.Sleep(10);
                }
                itemArrived.Close();
                if (disposing)
                    GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~BackgroundProcessor()
        {
            Dispose(false);
        }

        #endregion
    }
}
