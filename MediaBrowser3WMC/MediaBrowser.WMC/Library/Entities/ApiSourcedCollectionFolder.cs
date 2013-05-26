using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ApiSourcedCollectionFolder : LocalIbnSourcedCacheFolder
    {
        private List<BaseItem> _children;
        protected virtual ItemQuery Query { get { return new ItemQuery(); } }

        protected override string RalParentId
        {
            get { return Kernel.Instance.RootFolder.ApiId; }
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
