using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Resources;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library
{
    public static class EntryPointResolver
    {
        public static string EntryPointPath
        {
            get
            {
                string entryPointPath = String.Empty;

                try
                {
                    if (Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.EntryPointInfo.ContainsKey("context"))
                    {
                        entryPointPath = Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.EntryPointInfo["context"].ToString().Trim().ToLower();

                        entryPointPath = entryPointPath.Replace("{", String.Empty);
                        entryPointPath = entryPointPath.Replace("}", String.Empty);
                        entryPointPath = entryPointPath.Trim();
                    }
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error obtaining entry point path", ex);
                    entryPointPath = String.Empty;
                }
                return entryPointPath;
            }
        }

        public static BaseItem EntryPoint(string entryPointPath)
        {
            if (entryPointPath.Length > 0 && entryPointPath.ToLower() != Application.CONFIG_ENTRY_POINT)
            {
                // Find entry point path in root folder                            
                foreach (BaseItem item in Application.CurrentInstance.RootFolder.Children)
                {
                    String itemGUID = item.Id.ToString();
                    if (entryPointPath.ToLower() == itemGUID.ToLower() || (item.Path != null && (entryPointPath.ToLower() == item.Path.ToLower())))
                    {
                        return item;
                    }
                }
                //we'll only fall through to here if we can't find the entry point
                string msg = "Error : " + entryPointPath + " not found in root folder.";
                Logger.ReportError(msg);
                throw new Exception(msg);
            }
            return Application.CurrentInstance.RootFolder;
        }

    }
}
