using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Logging;
using System;
using System.IO;
using System.Text;

namespace MediaBrowser.Library.Util
{
    public class DeviceId
    {
        private readonly object _syncLock = new object();

        private static string CachePath
        {
            get { return Path.Combine(ApplicationPaths.CommonConfigPath, "device.txt"); }
        }

        private string GetCachedId()
        {
            try
            {
                lock (_syncLock)
                {
                    MigrateId();

                    var value = File.ReadAllText(CachePath, Encoding.UTF8);

                    try
                    {
                        var guid = new Guid(value);
                        return value;
                    }
                    catch (Exception)
                    {
                        Logger.ReportError("Invalid value found in device id file");
                    }

                }
            }
            catch (FileNotFoundException ex)
            {
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error reading file", ex);
            }

            return null;
        }

        private void MigrateId()
        {
            try
            {
                var oldFile = Path.Combine(ApplicationPaths.AppProgramPath, "device.txt");
                if (!Application.RunningOnExtender && File.Exists(oldFile))
                {
                    File.Move(oldFile, CachePath);
                    Logger.ReportInfo("Device file migrated");
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error migrating device.txt",e);
            }
        }

        private void SaveId(string id)
        {
            try
            {
                var path = CachePath;

                lock (_syncLock)
                {
                    File.WriteAllText(path, id, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error writing to device file", ex);
            }
        }

        private static string GetNewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private string GetDeviceId()
        {
            var id = GetCachedId();

            if (string.IsNullOrEmpty(id))
            {
                id = GetNewId();
                SaveId(id);
            }

            return id;
        }

        private string _id;

        public string Value
        {
            get { return _id ?? (_id = GetDeviceId()); }
        }
    }
}
