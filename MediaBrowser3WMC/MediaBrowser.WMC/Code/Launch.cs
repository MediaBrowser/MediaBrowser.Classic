using System.Collections.Generic;
using Microsoft.MediaCenter.Hosting;
using Microsoft.MediaCenter;
using System.Diagnostics;
using System.IO;
using System;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using MediaBrowser.LibraryManagement;
using System.Xml;
using System.Reflection;
using Microsoft.MediaCenter.UI;
using System.Text;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library;
using MediaBrowser.Library.Util;

namespace MediaBrowser
{
    public class MyAddIn : IAddInModule, IAddInEntryPoint
    {

        protected Application App;

        public void Initialize(Dictionary<string, object> appInfo, Dictionary<string, object> entryPointInfo)
        {
        }

        public void Uninitialize()
        {
            if (App != null)
            {
                App.Dispose();
            }
        }

        public void Launch(AddInHost host)
        {
            //  uncomment to debug
#if DEBUG
            host.MediaCenterEnvironment.Dialog("Attach debugger and hit ok", "debug", DialogButtons.Ok, 100, true); 
#endif

            using (Mutex mutex = new Mutex(false, Kernel.MBCLIENT_MUTEX_ID))
            {
                //set up so everyone can access
                var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
                var securitySettings = new MutexSecurity();
                try
                {
                    //don't bomb if this fails
                    securitySettings.AddAccessRule(allowEveryoneRule);
                    mutex.SetAccessControl(securitySettings);
                }
                catch (Exception)
                {
                    //we don't want to die here and we don't have a logger yet so just go on
                }
                try
                {
                    if (mutex.WaitOne(100,false))
                    {

                        var config = GetConfig();
                        if (config == null)
                        {
                            AddInHost.Current.ApplicationContext.CloseApplication();
                            return;
                        }

                        Environment.CurrentDirectory = ApplicationPaths.AppProgramPath;
                        using (new Util.Profiler("Total Kernel Init"))
                        {
                            Kernel.Init(config);
                            if (!Kernel.ServerConnected)
                            {
                                host.MediaCenterEnvironment.Dialog("Could not connect to Media Browser 3 Server.  Please be sure it is running on the local network.", "Error", DialogButtons.Ok, 100, true);
                                AddInHost.Current.ApplicationContext.CloseApplication();
                                return;
                            }
                        }
                        using (new Util.Profiler("Application Init"))
                        {
                            App = new Application(new MyHistoryOrientedPageSession(), host);

                            App.Init();
                        }

                        Kernel.Instance.OnApplicationInitialized();

                        mutex.ReleaseMutex();
                    }
                    else
                    {
                        //another instance running and in initialization - just blow out of here
                        Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.CloseApplication();
                        return;
                    }

                }
                catch (AbandonedMutexException)
                {
                    // Log the fact the mutex was abandoned in another process, it will still get acquired
                    Logger.ReportWarning("Previous instance of core ended abnormally...");
                    mutex.ReleaseMutex();
                }
            }
        }

        private static CommonConfigData GetConfig()
        {
            CommonConfigData config = null;
            try
            {
                config = CommonConfigData.FromFile(ApplicationPaths.CommonConfigFile);
            }
            catch (Exception ex)
            {
                MediaCenterEnvironment ev = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
                DialogResult r = ev.Dialog(ex.Message + "\n" + Application.CurrentInstance.StringData("ConfigErrorDial"), Application.CurrentInstance.StringData("ConfigErrorCapDial"), DialogButtons.Yes | DialogButtons.No, 600, true);
                if (r == DialogResult.Yes)
                {
                    config = new CommonConfigData(ApplicationPaths.CommonConfigFile);
                    config.Save();
                }
                else
                {
                    AddInHost.Current.ApplicationContext.CloseApplication();

                }
            }

            return config;
        }

        

    }
}