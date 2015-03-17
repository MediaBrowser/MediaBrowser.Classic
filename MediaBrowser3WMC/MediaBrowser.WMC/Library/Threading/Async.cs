using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using MediaBrowser.Library.Logging;
using System.Text;

namespace MediaBrowser.Library.Threading 
{
    public static class Async
    {
        public const string STARTUP_QUEUE = "Startup Queue";

        class ThreadPool
        {
            LinkedList<Action> actions = new LinkedList<Action>();
            List<Thread> threads = new List<Thread>();
            string name;
            volatile int maxThreads = 10;

            public ThreadPool(string name)
            {
                Debug.Assert(name != null);
                if (name == null)
                {
                    throw new ArgumentException("name should not be null");
                }
                this.name = name;
            }

            public void SetMaxThreads(int maxThreads)
            {
                Debug.Assert(maxThreads > 0);
                if (maxThreads < 1)
                {
                    throw new ArgumentException("maxThreads should be larger than 0");
                }

                this.maxThreads = maxThreads;
            }

            public void Queue(Action action, bool urgent)
            {
                Queue(action, urgent, 0);
            }

            public void Queue(Action action, bool urgent, int delay)
            {

                if (delay > 0)
                {
                    Timer t = null;
                    t = new Timer(_ =>
                    {
                        Queue(action, urgent, 0);
                        t.Dispose();
                    }, null, delay, Timeout.Infinite);
                    return;
                }

                lock (threads)
                {
                    // we are spinning up too many threads
                    // should be fixed 
                    if (maxThreads > threads.Count)
                    {
                        Thread t = new Thread(new ThreadStart(ThreadProc));
                        t.IsBackground = true;
                        // dont affect the UI.
                        t.Priority = ThreadPriority.Lowest;
                        t.Name = "Worker thread for " + name;
                        t.Start();
                        threads.Add(t);
                    }
                }

                lock (actions)
                {
                    //Logger.ReportVerbose("Queuing action for " + this.name);
                    if (urgent)
                    {
                        actions.AddFirst(action);
                    }
                    else
                    {
                        actions.AddLast(action);
                    }
                    Monitor.Pulse(actions);
                }
                
            }

            private void ThreadProc()
            {

                while (true)
                {

                    lock (threads)
                    {
                        if (maxThreads < threads.Count)
                        {
                            threads.Remove(Thread.CurrentThread);
                            break;
                        }
                    }

                    Action action;

                    lock (actions)
                    {
                        while (actions.Count == 0)
                        {
                            Monitor.Wait(actions);
                        }
                        action = actions.First.Value;
                        actions.RemoveFirst();
                    }
                    //Logger.ReportVerbose("Performing action for " + this.name + " on thread " + Thread.CurrentThread.ManagedThreadId.ToString());
                    action();
                }
            }

            public string Diagnostics
            {
                get
                {
                    var builder = new StringBuilder();
                    builder.AppendFormat(" >> maxThreads: {0}\n", maxThreads);
                    lock (threads)
                    {
                        builder.AppendFormat(" >> Total threads: {0}\n", threads.Count);
                    }
                    return builder.ToString();
                }
            }
        }

        static Dictionary<ThreadPoolName, ThreadPool> threadPool = new Dictionary<ThreadPoolName, ThreadPool>();

        public static Timer Every(int milliseconds, Action action)
        {
            Timer timer = new Timer(_ => action(), null, 0, milliseconds);
            return timer;
        }

        public enum ThreadPoolName
        {
            IsPlayingVideoDelay,
            AccessError,
            ActorLoad,
            AddUser,
            AspectRatioCalculator,
            BackdropLoader,
            BackgroundSorter, 
            BasePlaybackControllerOnProgress,
            ChildRefresh,
            ConfigNotify,
            ConnectError,
            Context,
            CustomMsg,
            DeleteMediaItem,
            DetailLoad,
            Error, 
            ExtPlayerMgmt, 
            ExtSplashShow, 
            FakeProgressForDownload,
            FastLoadLoader, 
            FavToggle,
            FileWatcherRefresher,
            FolderModelItemCount,
            FolderModelMediaCount,
            FolderSetWatched,
            GenreNavigation,
            Ident,
            ImageCacheCleanup,
            IndexBy,
            LaunchError,
            LoadUser,
            LogfileCleanup,
            Logout,
            ManualFullRefresh,
            MBRegistration,
            Migration,
            Msg,
            NewestItemLoader,
            NewViews,
            OnCurrentItemChanged,
            OnNavigatingInto,
            OnNavigationInto,
            OnPlaybackFinished,
            OnPlayStateSaved,
            OverviewLoader,
            PackageLoad,
            PackageRating,
            PanelReset,
            PersonNavigation,
            Ping,
            PlayableItemPlaybackFinished,
            PlayAction,
            PlaybackFinished,
            PlayIntros,
            PluginRemove,
            PluginUpdate,
            PostDeleteValidate,
            QuicklistUpdate,
            RefreshRateError,
            RefreshUI, 
            Registration,
            RuntimeCalc,
            Search,
            SetDirectoryPermissions, 
            SimilarNavigation,
            SpecialFeatureLoad,
            SqliteDisplayWriter, 
            SqliteWriter, 
            StartupQueue,
            SupporterCheck,
            ToggleWatched,
            UIForcedFolderMetadataLoader,
            UITriggeredMetadataLoader,
            UnwatchedCounterForFolderModel, 
            UpdateCheck,
            UpdateFav,
            UserConfigSave,
            WaitForExternalPlayerToLaunch, 
            WaitForProcessToExit, 
            WeakRefExpiry,
            Common,
            Legacy // allowed
        }

        /// <summary>
        /// Determines which pool a request thread identifier will actually run in.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static ThreadPoolName TranslatePool(ThreadPoolName name)
        {
            switch (name)
            {
                case ThreadPoolName.WaitForExternalPlayerToLaunch:
                case ThreadPoolName.WaitForProcessToExit:
                case ThreadPoolName.SqliteDisplayWriter:
                case ThreadPoolName.SqliteWriter:
                case ThreadPoolName.FastLoadLoader:
                case ThreadPoolName.Legacy:
                    return name;
                default:
                    return ThreadPoolName.Common;
            }
        }

       

        
        [Obsolete("Legacy method taking string uniqueId, for compatability with old plugins")]
        public static void SetMaxThreads(string uniqueId, int threads)
        {
            GetThreadPool(GetThreadPoolNameFromLegacyString(uniqueId)).SetMaxThreads(threads);
        }
        [Obsolete("Legacy method taking string uniqueId, for compatability with old plugins")]
        public static void Queue(string uniqueId, Action action)
        {
            Queue(GetThreadPoolNameFromLegacyString(uniqueId), action, null);
        }
        [Obsolete("Legacy method taking string uniqueId, for compatability with old plugins")]
        public static void Queue(string uniqueId, Action action, int delay)
        {
            Queue(GetThreadPoolNameFromLegacyString(uniqueId), action, null, false, delay);
        }
        [Obsolete("Legacy method taking string uniqueId, for compatability with old plugins")]
        public static void Queue(string uniqueId, Action action, Action done)
        {
            Queue(GetThreadPoolNameFromLegacyString(uniqueId), action, done, false);
        }
        [Obsolete("Legacy method taking string uniqueId, for compatability with old plugins")]
        public static void Queue(string uniqueId, Action action, Action done, bool urgent)
        {
            Queue(GetThreadPoolNameFromLegacyString(uniqueId), action, done, urgent, 0);
        }

        [Obsolete("Legacy method taking string uniqueId, for compatability with old plugins")]
        public static void Queue(string uniqueId, Action action, Action done, bool urgent, int delay)
        {
            Queue(GetThreadPoolNameFromLegacyString(uniqueId), action, done, urgent, delay);
        }


        
        public static void SetMaxThreads(ThreadPoolName uniqueId, int threads)
        {
            GetThreadPool(uniqueId).SetMaxThreads(threads);
        }

        public static void Queue(ThreadPoolName uniqueId, Action action)
        {
            Queue(uniqueId, action, null);
        }

        public static void Queue(ThreadPoolName uniqueId, Action action, int delay)
        {
            Queue(uniqueId, action, null, false, delay);
        }

        public static void Queue(ThreadPoolName uniqueId, Action action, Action done)
        {
            Queue(uniqueId, action, done, false);
        }

        public static void Queue(ThreadPoolName uniqueId, Action action, Action done, bool urgent)
        {
            Queue(uniqueId, action, done, urgent, 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <param name="action"></param>
        /// <param name="done"></param>
        /// <param name="urgent"></param>
        /// <param name="delay">Millisecond delay before executing </param>
        public static void Queue(ThreadPoolName uniqueId, Action action, Action done, bool urgent, int delay)
        {

            //Debug.Assert(uniqueId != null);
            Debug.Assert(action != null);

            Action workItem = () =>
            {
                //Logger.ReportVerbose("Starting work item");
                try
                {
                    action();
                }
                catch (ThreadAbortException) { /* dont report on this, its normal */ }
                catch (Exception ex)
                {
                    Logger.ReportException("Async thread threw the following exception: ", ex);
                    Debug.Assert(false, "Async thread threw the following exception: " + ex.ToString());
                }
                //Logger.ReportVerbose("Finished work item");
                if (done != null) done();
                //Logger.ReportVerbose("Finished done callback for work item");
            };
            //Logger.ReportVerbose("Getting threadpool " + uniqueId);
            GetThreadPool(uniqueId).Queue(workItem, urgent, delay);
        }

        private static ThreadPoolName GetThreadPoolNameFromLegacyString(string uniqueId)
        {
            ThreadPoolName name = ThreadPoolName.Legacy;
            try
            {
                name = (ThreadPoolName)Enum.Parse(typeof(ThreadPoolName), uniqueId);
            }
            catch { }
            return name;
        }

        private static ThreadPool GetThreadPool(string uniqueId)
        {
            return GetThreadPool(GetThreadPoolNameFromLegacyString(uniqueId));
        }

        private static ThreadPool GetThreadPool(ThreadPoolName uniqueId)
        {
            uniqueId = TranslatePool(uniqueId);
            ThreadPool currentPool;
            lock (threadPool)
            {
                if (!threadPool.TryGetValue(uniqueId, out currentPool))
                {
                    currentPool = new ThreadPool(uniqueId.ToString());
                    threadPool[uniqueId] = currentPool;
                    currentPool.SetMaxThreads(10);
                }
            }
            return currentPool;
        }


        /// <summary>
        /// Run an action with given timeout - WON"T Stop the action but will return
        /// </summary>
        /// <param name="action">Action to perform</param>
        /// <param name="timeout">timeout in milliseconds</param>
        public static void RunWithTimeout(Action action, int timeout)
        {
            IAsyncResult ar = action.BeginInvoke(null, null);
            if (ar.AsyncWaitHandle.WaitOne(timeout))
                action.EndInvoke(ar); // This is necesary so that any exceptions thrown by action delegate is rethrown on completion         
            else
                throw new TimeoutException("Operation failed to complete in " + timeout / 1000 + " seconds.");
        }

        public static object Diagnostics
        {
            get
            {
                var builder = new StringBuilder();
                lock (threadPool)
                {
                    foreach (var pool in threadPool)
                    {
                        builder.AppendFormat("{0}: \n{1}\n\n", pool.Key, pool.Value.Diagnostics);
                    }
                }
                return builder.ToString();
            }

        }
    }
}
