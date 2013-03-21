using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Localization;

/*
 This is a rough index implementation, this name can be an actor or genre or year. 
   Its not persisted, but will call the metadata provider to extract things like pictures
 */

namespace MediaBrowser.Library.Entities {
    public class Index : Folder {

        BaseItem shadowItem;
        string indexProperty;
        string childTableName;
        object propertyValue;

        public override string Name {
            get {
                return shadowItem.Name;
            }
            set {
                shadowItem.Name = value;
            }
        }

        public override string DisplayMediaType
        {
            get
            {
                return shadowItem.DisplayMediaType;
            }
            set
            {
                shadowItem.DisplayMediaType = value;
            }
        }

        public override List<string> BackdropImagePaths {
            get {
                return shadowItem.BackdropImagePaths;
            }
            set {
                shadowItem.BackdropImagePaths = value;
            }
        }

        public override string BannerImagePath {
            get {
                return shadowItem.BannerImagePath;
            }
            set {
                shadowItem.BannerImagePath = value;
            }
        }

        public override string PrimaryImagePath {
            get {
                return shadowItem.PrimaryImagePath;
            }
            set {
                shadowItem.PrimaryImagePath = value;
            }
        }

        public override string SecondaryImagePath {
            get {
                return shadowItem.SecondaryImagePath;
            }
            set {
                shadowItem.SecondaryImagePath = value;
            }
        }

        public override string SortName {
            get {
                return shadowItem.SortName;
            }
            set {
                shadowItem.SortName = value;
            }
        }

        List<BaseItem> children;

        public Index(BaseItem item, List<BaseItem> children) {
            this.children = children;
            this.Id = Guid.NewGuid();
            this.shadowItem = item;
            //don't want to allow indexing an index
            this.IndexByOptions = new Dictionary<string, string>() {
                    {LocalizedStrings.Instance.GetString("NoneDispPref"), ""} 
            };
        }

        public Index(BaseItem item, string childTable, string property, object value)
        {
            this.children = null;
            this.Id = Guid.NewGuid();
            this.indexProperty = property;
            this.childTableName = childTable;
            this.propertyValue = value;
            this.shadowItem = item;
            //don't want to allow indexing an index
            this.IndexByOptions = new Dictionary<string, string>() {
                    {LocalizedStrings.Instance.GetString("NoneDispPref"), ""} 
            };
        }

        protected override List<BaseItem> ActualChildren
        {
            get
            {
                if (children == null)
                {
                    children = Kernel.Instance.ItemRepository.RetrieveSubIndex(childTableName, indexProperty, propertyValue);
                }

                return children;
            }
        }

        public override void ValidateChildren() {
            // do nothing
        }

        public override void EnsureChildrenLoaded() {
            // do nothing
        }

        public override bool RefreshMetadata(MediaBrowser.Library.Metadata.MetadataRefreshOptions options) {
            return shadowItem.RefreshMetadata(options);
        }

    }
}
