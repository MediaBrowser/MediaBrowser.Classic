using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;
using System.IO;
using System.Xml.XPath;
using Microsoft.MediaCenter.UI;
using System.Diagnostics;

using MediaBrowser.Library;

namespace MediaBrowser
{
    /// <summary>
    /// This provides information to the root page on-screen display. You have the option of adding
    /// one-time or recurring messages.
    /// </summary>
    public class MultiBackdrop : ModelItem
    {
        Item _item;
        Timer cycle;

        // Parameterless constructor for mcml
        public MultiBackdrop()
        {
        }

        public void BeginRotation(Item item)
        {
            this._item = item;
            if (cycle == null) {
                cycle = new Timer(this);
                cycle.Interval = (Config.Instance.BackdropRotationInterval * 1000); //Convert to millisecs
                cycle.Tick += delegate { OnRefresh(); };
                cycle.Enabled = true;
            }
            // new item - restart timer
            cycle.Stop();
            cycle.Start();
        }

        private void OnRefresh()
        {
            _item.GetNextBackDropImage();
        }

    }
}
