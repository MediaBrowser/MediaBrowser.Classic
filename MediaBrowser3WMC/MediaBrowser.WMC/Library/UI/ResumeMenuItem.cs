using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library
{
    class ResumeMenuItem : MenuItem
    {
        public ResumeMenuItem(string name, string image, anAction action, List<MenuType> supports)
        {
            text = name;
            imageSource = image;
            command = action;
            supportedMenus = supports;
        }

        public override bool Available
        {
            get
            {
                return (Application.CurrentInstance.CurrentItem.CanResume && base.Available);
            }
            set
            {
                base.Available = value;
            }
        }
    }
}
