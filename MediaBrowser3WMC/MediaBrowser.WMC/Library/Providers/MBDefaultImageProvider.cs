using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.LibraryManagement;
using System.IO;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Configuration;

namespace MediaBrowser.Library.Providers
{
    [ProviderPriority(200)]
    [SupportedType(typeof(BaseItem))]
    public class MBDefaultImageProvider : ImageFromMediaLocationProvider
    {
        protected string location;
        protected override string Location
        {
            get
            {
                if (location == null)
                {
                    //try the default area by type
                    location = ApplicationPaths.AppIBNPath; //reset to root
                    location = Path.Combine(location, "Default\\" + Item.GetType().Name);

                    //now we have a specific default folder for this type - be sure it exists
                    if (!Directory.Exists(location))
                    {
                        //nope - use a generic
                        string baseType = Item is Folder ? "folder" : "video";
                        location = Path.Combine(ApplicationPaths.AppIBNPath, "default\\" + baseType);
                    }
                }
                return location;
            }
        }
    }
}
