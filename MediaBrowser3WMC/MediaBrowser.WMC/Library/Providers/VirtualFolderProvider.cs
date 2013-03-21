using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Providers.Attributes;

namespace MediaBrowser.Library.Providers
{

    [ProviderPriority(10)]
    [SupportedType(typeof(Folder))]
    class VirtualFolderProvider : BaseMetadataProvider
    {
        [Persist]
        string imagePath;

        // to simplify things we could consider moving this into folder ... 
        private VirtualFolderContents VirtualFolder {
            get {
                VirtualFolderContents virtualfolder = null;
                var folder = Item as MediaBrowser.Library.Entities.Folder;
                if (folder != null) {
                    var location = folder.FolderMediaLocation as VirtualFolderMediaLocation;
                    if (location != null) {
                        virtualfolder = location.VirtualFolder;
                    } 
                }
                return virtualfolder;
            }
        }

        public override void Fetch()
        {
            if (Item.Path == null) return;

            var virtualFolder = VirtualFolder;
            if (virtualFolder != null) {
                Item.PrimaryImagePath = imagePath = virtualFolder.ImagePath;
                Item.SortName = virtualFolder.SortName;
            }
        }

        bool firstTime = true; 
        public override bool NeedsRefresh()
        {
            if (Item.Path == null) return false;

            bool changed = false;
            var virtualFolder = VirtualFolder;
            if (virtualFolder != null) {
                //changed = firstTime; // we need to refresh once per entry to be sure sort is right
                //firstTime = false;
                changed |= imagePath != virtualFolder.ImagePath;
                changed |= Item.PrimaryImagePath == null && virtualFolder.ImagePath != null;
                changed |= Item.SortName != virtualFolder.SortName;
            }
            return changed;
        }
    }

}
