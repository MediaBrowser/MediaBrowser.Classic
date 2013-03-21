using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace MediaBrowser.Library.Logging {
    public class FileLogger : ThreadedLogger {

        static string instanceDisamiguator = Guid.NewGuid().ToString().Replace("-", "");
        static string configID
        {
            get
            {
                if (AppDomain.CurrentDomain.FriendlyName.ToLower().Contains("configurator"))
                {
                    return "Configurator-";
                }
                else
                    if (AppDomain.CurrentDomain.FriendlyName.ToLower().Contains("mbmigrate"))
                    {
                        return "Migration-";
                    }
                    else
                    {
                        switch (Kernel.LoadContext)
                        {
                            case MBLoadContext.Service:
                                return "MBService-";
                            case MBLoadContext.Core:
                                return "MBCore-";
                            default:
                                return "Unknown-";
                        }
                    }
            }
        }

        string path;
        
        FileStream stream;
        StreamWriter writer;
        string filename;
        

        public FileLogger(string path) : base() {
            this.path = path;
        }
        
        private string CurrentFile {
            get
            {
                return Path.Combine(path, configID+DateTime.Now.ToString("dMyyyy") + instanceDisamiguator + ".log");
            }
        }

        public void UpdateStream() {
            if (filename != CurrentFile) {
                if (stream != null) stream.Close();
                filename = CurrentFile;
                stream = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read);
                writer = new StreamWriter(stream);
                writer.AutoFlush = true;
            } 
        }


        protected override void AsyncLogMessage(LogRow row) {
            UpdateStream();
            writer.WriteLine(row.ToString());
        }

        public override void Dispose() {

            base.Dispose();

            if (filename != null) {
                writer.Close();
            }
        }
    }
}
