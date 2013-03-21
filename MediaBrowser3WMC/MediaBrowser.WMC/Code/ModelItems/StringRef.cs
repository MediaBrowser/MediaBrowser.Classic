using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Code.ModelItems;

namespace MediaBrowser.Library
{
    public class StringRef : BaseModelItem
    {
        private string val;
        public string Value
        {
            get { return this.val; }
            set
            {
                this.val = value;
                FirePropertyChanged("Value");
            }
        }
    }
}
