using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Configuration;

namespace MediaBrowser.Library.Logging {

    /// <summary>
    /// This is the class you should use for logging, it redirects all the calls to the Kernel.
    /// </summary>
    public static class Logger {

        static Logger() {
            // our default logger
            LoggerInstance = new TraceLogger();
        }

        public static ILogger LoggerInstance { get; set; }

        public static void ReportVerbose(string message) {
            LoggerInstance.ReportVerbose(message);
        }

        public static void ReportVerbose(string message, params object[] paramList) {
            LoggerInstance.ReportVerbose(string.Format(message, paramList));
        }

        public static void ReportInfo(string message) {
            LoggerInstance.ReportInfo(message);
        }

        public static void ReportInfo(string message, params object[] paramList) {
            LoggerInstance.ReportInfo(string.Format(message, paramList));
        }

        public static void ReportWarning(string message) {
            LoggerInstance.ReportWarning(message);
        }

        public static void ReportWarning(string message, params object[] paramList) {
            LoggerInstance.ReportWarning(string.Format(message, paramList));
        }

        public static void ReportException(string message, Exception exception) {
            LoggerInstance.ReportException(message, exception);
        }

        public static void ReportException(string message, Exception exception, params object[] paramList) {
            LoggerInstance.ReportException(string.Format(message, paramList), exception);
        }

        public static void ReportError(string message) {
            LoggerInstance.ReportError(message);
        }

        public static void ReportError(string message, params object[] paramList) {
            LoggerInstance.ReportError(string.Format(message, paramList));
        }
    }
}
