using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace MediaBrowser.Library.Logging
{

    public abstract partial class LoggerBase : ILogger
    {
        public LoggerBase()
        {
            Enabled = true;
        }

        public LoggerBase(LogSeverity severity)
        {
            Enabled = true;
            Severity = severity;
        }

        public bool Enabled { get; set; }

        public LogSeverity Severity { get; set; }

        public void ReportVerbose(string message)
        {
            ReportVerbose(message, "");
        }

        public void ReportVerbose(string message, string category)
        {
            LogMessage(LogSeverity.Verbose, message, category);
        }

        public void ReportInfo(string message)
        {
            ReportInfo(message, "");
        }

        public void ReportInfo(string message, string category)
        {
            LogMessage(LogSeverity.Info, message, category);
        }

        public void ReportWarning(string message)
        {
            LogMessage(LogSeverity.Warning, message, "");
        }

        public void ReportWarning(string message, string category)
        {
            LogMessage(LogSeverity.Warning, message, category);
        }

        public void ReportException(string message, Exception exception)
        {
            ReportException(message, exception, "");
        }

        public void ReportException(string message, Exception exception, string category)
        {

            StringBuilder builder = new StringBuilder();
            if (exception != null)
            {
                var trace = new StackTrace(exception, true);
                builder.AppendFormat("Exception.  Type={0} Msg={1} Src={2} Method={5} Line={6} Col={7}{4}StackTrace={4}{3}",
                    exception.GetType().FullName,
                    exception.Message,
                    exception.Source,
                    exception.StackTrace,
                    Environment.NewLine,
                    trace.GetFrame(0).GetMethod().Name,
                    trace.GetFrame(0).GetFileLineNumber(),
                    trace.GetFrame(0).GetFileColumnNumber());
            }
            StackFrame frame = new StackFrame(1);
            ReportError(string.Format("{0} ( {1} )", message, builder),  "");
        }

        public void ReportError(string message)
        {
            ReportError(message, "");
        }

        public void ReportError(string message, string category)
        {
            LogMessage(LogSeverity.Error, message, category);
        }

        void LogMessage(LogSeverity severity, string message)
        {
            LogMessage(severity, message, "");
        }

        void LogMessage(LogSeverity severity, string message, string category)
        {

            if (!Enabled || severity < this.Severity) return;

            string threadName = Thread.CurrentThread.Name;
            int threadId = Thread.CurrentThread.ManagedThreadId;
            DateTime now = DateTime.Now;

            LogRow row = new LogRow()
            {
                Severity = severity,
                Message = message,
                Category = category,
                ThreadId = threadId,
                ThreadName = threadName,
                Time = now
            };

            LogMessage(row);
        }

        public virtual void Flush()
        {
        }

        public abstract void LogMessage(LogRow row);


        public virtual void Dispose()
        {
        }

    }
}
