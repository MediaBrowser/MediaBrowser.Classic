using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ApiSourcedFolder : LocalIbnSourcedCacheFolder
    {
        private List<BaseItem> _children;
        public virtual ItemQuery Query { get { return new ItemQuery(); } }
        public virtual string[] IncludeItemTypes { get; set; }
        public virtual string[] ExcludeItemTypes { get; set; }

        public ApiSourcedFolder() : base()
        {
        }

        public ApiSourcedFolder(BaseItem item, string[] includeTypes = null, string[] excludeTypes = null)
        {
            Init(item, includeTypes, excludeTypes);
        }

        protected void Init(BaseItem item, string[] includeTypes = null, string[] excludeTypes = null)
        {
            Name = item.Name;
            Id = item.Id;
            PrimaryImagePath = item.PrimaryImagePath;
            BackdropImagePaths = item.BackdropImagePaths;
            DisplayMediaType = item.DisplayMediaType;
            IncludeItemTypes = includeTypes;
            ExcludeItemTypes = excludeTypes;
            
        }

        protected override string RalParentId
        {
            get { return Kernel.Instance.RootFolder.ApiId; }
        }

        protected override string[] RalIncludeTypes
        {
            get
            {
                return IncludeItemTypes;
            }
        }

        protected override List<BaseItem> ActualChildren
        {
            get { return _children ?? (_children = Kernel.Instance.MB3ApiRepository.RetrieveItems(Query).ToList()); }
        }

        public override BaseItem ReLoad()
        {
            _children = null;
            return this;
        }

    }
}
