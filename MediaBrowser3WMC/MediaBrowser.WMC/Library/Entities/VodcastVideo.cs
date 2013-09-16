using System;

namespace MediaBrowser.Library.Entities {
    public class VodCastVideo : RemoteVideo, IGroupInIndex
    {
        public IContainer MainContainer
        {
            get { return Parent as VodCast ?? RetrieveParent() ?? new VodCast {Name = "Unknown"}; }
        }

        protected VodCast RetrieveParent()
        {
            return !string.IsNullOrEmpty(ApiParentId) ? Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(ApiParentId)) as VodCast : null;
        }

    }
}
