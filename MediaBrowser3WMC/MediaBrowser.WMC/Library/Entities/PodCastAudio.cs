using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities
{
    class PodCastAudio : Song
    {
        new public IContainer MainContainer { get { return Parent as VodCast ?? RetrieveParent() ?? new VodCast {Name = "Unknown"}; } }

        protected VodCast RetrieveParent()
        {
            return !string.IsNullOrEmpty(ApiParentId) ? Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(ApiParentId)) as VodCast : null;
        }


    }
}
