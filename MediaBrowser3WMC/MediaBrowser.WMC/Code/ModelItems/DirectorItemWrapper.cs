using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Code.ModelItems;

namespace MediaBrowser.Library
{
    public class DirectorItemWrapper : BaseModelItem
    {
        public string Director{ get; private set; }
        private FolderModel parent;

        public DirectorItemWrapper(string director, FolderModel parent)
        {
            this.Director = director;
            this.parent = parent;
        }

        public Item Item
        {
            get
            {
                return null; 
            }
        }
    }
}
