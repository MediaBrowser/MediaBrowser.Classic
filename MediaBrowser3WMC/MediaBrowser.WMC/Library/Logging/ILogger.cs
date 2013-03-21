using System;
namespace MediaBrowser.Library.Logging {
    public interface ILogger : IDisposable {
        bool Enabled { get; set; }
        void ReportError(string message, string category);
        void ReportError(string message);
        void ReportException(string message, Exception exception, string category);
        void ReportException(string message, Exception exception);
        void ReportInfo(string message, string category);
        void ReportInfo(string message);
        void ReportVerbose(string message, string category);
        void ReportVerbose(string message);
        void ReportWarning(string message, string category);
        void ReportWarning(string message);
        void LogMessage(LogRow row);
        void Flush();
        LogSeverity Severity { get; set; }
    }
}
