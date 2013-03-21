using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Logging {
    public class MultiLogger : LoggerBase{

        public MultiLogger(LogSeverity severity): base(severity) {
            Loggers = new List<ILogger>();
        }

        public List<ILogger> Loggers { get; private set; }

        public void AddLogger(ILogger logger) {
            Loggers.Add(logger);
        }

        public override void LogMessage(LogRow row) {

            foreach (var logger in Loggers) {
                if (logger.Enabled) logger.LogMessage(row);
            }
        }

        public override void Dispose() {

            foreach (var logger in Loggers) {
                logger.Dispose();
            }

            base.Dispose();

        }

    }
}
