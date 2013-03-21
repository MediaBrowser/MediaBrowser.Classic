using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Code.ModelItems;

namespace MediaBrowser.Library
{
    public class SizeRef : BaseModelItem
    {
        public SizeRef()
        {
        }

        public SizeRef(Size s)
        {
            this.val = s;
        }
        private Size val;
        public Size Value
        {
            get { return this.val; }
            set
            {
                if (this.val != value)
                {
                    this.val = value;
                    FirePropertyChanged("Value");
                }
            }
        }
    }
}
