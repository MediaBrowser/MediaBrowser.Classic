using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Configuration;
using System.Reflection;
using System.Xml;

using MediaBrowser.Code.ShadowTypes;
using MediaBrowser.Library;
using MediaBrowser.LibraryManagement;
using System.Xml.Serialization;
using MediaBrowser.Library.Playables;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Plugins;

namespace MediaBrowser
{
    [Serializable]
    public class ServiceConfigData
    {

        [Comment("The frequency at which we will force a full refresh of the library (in days)")]
        public int FullRefreshInterval = 1;

        [Comment("The hour of day full refresh is supposed to run")]
        public int FullRefreshPreferredHour = 2;

        [Comment("Option to put the computer to sleep after a refresh.  Will only do it if the refresh is running at it's normal time.")]
        public bool SleepAfterScheduledRefresh = false;

        [Comment("Option to allow internet and other slow providers in scheduled refresh")]
        public bool AllowSlowProviders = false;

        [Comment("The last time a full refresh was done.")]
        public DateTime LastFullRefresh = DateTime.MinValue;

        [Comment("Show balloon tip on close window.")]
        public bool ShowBalloonTip = true;

        [Comment("Suppress warning about the image cache.")]
        public bool DontWarnImageCache = false;

        [Comment("Suppress warning about people images.")]
        public bool DontWarnPeopleImages = false;

        [Comment("A forced rebuild is underway.")]
        public bool ForceRebuildInProgress = false;

        [Comment("The refresh process failed.")]
        public bool RefreshFailed = false;

        // for our reset routine
        public ServiceConfigData()
        {
            try
            {
                File.Delete(ApplicationPaths.ServiceConfigFile);
            }
            catch (Exception e)
            {
                MediaBrowser.Library.Logging.Logger.ReportException("Unable to delete config file " + ApplicationPaths.ServiceConfigFile, e);
            }
            //continue anyway
            this.file = ApplicationPaths.ConfigFile;
            this.settings = XmlSettings<ServiceConfigData>.Bind(this, file);
        }


        public ServiceConfigData(string file)
        {
            this.file = file;
            this.settings = XmlSettings<ServiceConfigData>.Bind(this, file);
        }

        [SkipField]
        string file;

        [SkipField]
        XmlSettings<ServiceConfigData> settings;


        public static ServiceConfigData FromFile(string file)
        {
            return new ServiceConfigData(file);
        }

        public void Save()
        {
            this.settings.Write();
        }

    }
}
