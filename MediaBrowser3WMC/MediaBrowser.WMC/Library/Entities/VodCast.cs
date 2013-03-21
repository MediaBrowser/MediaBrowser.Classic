using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.EntityDiscovery;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Extensions;
using System.Xml;
using System.ServiceModel.Syndication;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MediaBrowser.Library.Network;
using System.IO;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Entities {


    public class VodCast : Folder {

        // update the vodcast every 60 minutes
        const int UpdateMinuteInterval = 60;

        [Persist]
        public string Url { get; set; }
        [Persist]
        public DownloadPolicy DownloadPolicy { get; set; }
        [Persist]
        public int FilesToRetain {get; set; }  

        [Persist]
        List<BaseItem> children = new List<BaseItem>();

        [Persist]
        DateTime lastUpdated = DateTime.MinValue;

        string downloadPath; 
        public string DownloadPath {
            get {

                if (downloadPath != null) return downloadPath;

                downloadPath = System.IO.Path.Combine( 
                    System.IO.Path.GetDirectoryName(Path) ,  
                    System.IO.Path.GetFileNameWithoutExtension(Path)
                    );

                lock (this) {
                    if (!Directory.Exists(downloadPath)) {
                        Directory.CreateDirectory(downloadPath);
                        File.WriteAllText(System.IO.Path.Combine(downloadPath, FolderResolver.IGNORE_FOLDER), ""); 
                    }
                }

                return downloadPath;
            } 
        }

        public override void Assign(IMediaLocation location, IEnumerable<InitializationParameter> parameters, Guid id) {
            RefreshUserSettings(location);
            base.Assign(location, parameters, id);
        }

        private void RefreshUserSettings(IMediaLocation location) {
            VodcastContents parser = new VodcastContents(location.Contents);
            this.Url = parser.Url;
            this.DownloadPolicy = parser.DownloadPolicy;
            this.FilesToRetain = parser.FilesToRetain;
        }

        public void SaveSettings() { 
            VodcastContents generator = new VodcastContents();
            generator.DownloadPolicy = DownloadPolicy;
            generator.FilesToRetain = FilesToRetain;
            generator.Url = Url;
            Kernel.Instance.GetLocation(Path).Contents = generator.Contents;
            Kernel.Instance.ItemRepository.SaveItem(this);
        } 

        public override void ValidateChildren() {

            try {

                RefreshUserSettings(Kernel.Instance.GetLocation(Path));

                if (Math.Abs((lastUpdated - DateTime.Now).TotalMinutes) < UpdateMinuteInterval) return;

                lastUpdated = DateTime.Now;

                RSSFeed feed = new RSSFeed(Url);
                feed.Refresh();
                PrimaryImagePath = feed.ImageUrl;
                children = feed.Children.Distinct(key => key.Id).ToList();
                SetParent();

                Overview = feed.Description;

                this.FolderChildrenChanged = true;
                this.OnChildrenChanged(null);
                Kernel.Instance.ItemRepository.SaveItem(this);
            } catch (Exception e) {
                Logger.ReportException("Failed to update podcast!", e);
            }
        }

        private void SetParent() {
            foreach (var item in children) {
                item.Parent = this;
            }
        }

        // this stuff should move to AfterDeserialize 
        bool parentSet = false;
        protected override List<BaseItem> ActualChildren {
            get {
                if (!parentSet) {
                    SetParent();
                    parentSet = true;
                }
                if (lastUpdated == DateTime.MinValue) {
                    ValidateChildren();
                }
                return children;
            }
        }

        public override bool RefreshMetadata(MediaBrowser.Library.Metadata.MetadataRefreshOptions options) {
            // metadata should not be acquired through the provider framework. 
            // its all done during item validation
            return false;
        }
    }
}
