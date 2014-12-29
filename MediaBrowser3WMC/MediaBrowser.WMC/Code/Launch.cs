using System.Collections.Generic;
using Microsoft.MediaCenter.Hosting;
using Microsoft.MediaCenter;
using System;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library;

namespace MediaBrowser
{
    public class MyAddIn : IAddInModule, IAddInEntryPoint
    {

        protected Application App;

        public void Initialize(Dictionary<string, object> appInfo, Dictionary<string, object> entryPointInfo)
        {
            // set up assembly resolution hooks, so earlier versions of the plugins resolve properly 
            AppDomain.CurrentDomain.AssemblyResolve += Kernel.OnAssemblyResolve;

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


            var config = GetConfig();
            if (config == null)
            {
                AddInHost.Current.ApplicationContext.CloseApplication();
                return;
            }
            //set us up for single instance
            AddInHost.Current.ApplicationContext.SingleInstance = true;

            Environment.CurrentDirectory = ApplicationPaths.AppProgramPath;
            using (new Util.Profiler("Total Kernel Init"))
            {
                Kernel.Init(config);
            }
            using (new Util.Profiler("Application Init"))
            {
                App = new Application(new MyHistoryOrientedPageSession(), host);

                App.Init();
            }

            Kernel.Instance.OnApplicationInitialized();

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