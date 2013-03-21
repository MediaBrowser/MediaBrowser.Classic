using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.LibraryManagement;
using System.IO;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Configuration;

namespace MediaBrowser.Library.Providers
{
    [ProviderPriority(20)]
    [SupportedType(typeof(BaseItem))]
    class ImageByNameProvider : ImageFromMediaLocationProvider
    {
        protected string location;
        protected override string Location
        {
            get {
                if (location == null)
                {

                    location = ApplicationPaths.AppIBNPath;

                    //sub-folder is based on the type of thing we're looking for
                    if (Item is Genre)
                            location = Path.Combine(location, "Genre");
                    else if (Item is Person)
                            location = Path.Combine(location, "People");
                    else if (Item is Studio)
                            location = Path.Combine(location, "Studio");
                    else if (Item is Year)
                            location = Path.Combine(location, "Year");
                    else
                            location = Path.Combine(location, "General");


                    char[] invalid = Path.GetInvalidFileNameChars();

                    string name = Item.Name;
                    foreach (char c in invalid)
                        name = name.Replace(c.ToString(), "");
                    location = Path.Combine(location, name);

                }
                return location;
            }
        }

        
    }
}
