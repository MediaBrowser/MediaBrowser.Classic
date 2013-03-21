using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Util
{
    public class Profiler : IDisposable
    {

        public static void TimeAction(string description, Action func)
        {
            var watch = new Stopwatch();
            watch.Start();
            func();
            watch.Stop();
            Trace.Write(description);
            Trace.WriteLine(string.Format(" Time Elapsed {0} ms", watch.ElapsedMilliseconds));
        }

        string caller;
        string name;
        Stopwatch stopwatch;

        public Profiler(string name)
        {
            this.name = name;
            StackTrace st = new StackTrace();
            caller = st.GetFrame(1).GetMethod().Name;
            stopwatch = new Stopwatch();
            stopwatch.Start();
        }
        #region IDisposable Members

        public void Dispose()
        {
            stopwatch.Stop();
            string message;
            if (stopwatch.ElapsedMilliseconds > 300000)
            {
                message = string.Format("{1} took {2} minutes.",
                    caller, name, ((float)stopwatch.ElapsedMilliseconds / 60000).ToString("F"));
            }
            else
            {
                message = string.Format("{1} took {2} seconds.",
                    caller, name, ((float)stopwatch.ElapsedMilliseconds / 1000).ToString("F"));
            }
            Logger.ReportInfo(message);

            // DO NOT hook this into the UI, we use this for internal diagnostics
        }

        #endregion
    }
}
