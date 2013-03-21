using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library
{
    public class StudioItemWrapper : BaseModelItem
    {
        public Studio Studio { get; private set; }
        private FolderModel parent;
        Item item = null;

        public StudioItemWrapper(Studio studio, FolderModel parent)
        {
            this.Studio = studio;
            this.parent = parent;
        }

        public StudioItemWrapper()
        { }

        public Item Item
        {
            get
            {
                if (item != null) return item;

                item = ItemFactory.Instance.Create(Studio);
                item.PhysicalParent = parent;
                return item;
            }
        }
    }
}
