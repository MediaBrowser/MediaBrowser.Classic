using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.MediaCenter.UI;

namespace MediaBrowser.Library.UI
{
    public class ConfigPanel
    {
        private string resource = "";
        private ModelItem config;

        public ConfigPanel(string resourceName, ModelItem configObject)
        {
            resource = resourceName;
            config = configObject;
        }

        public ConfigPanel(string resourceName)
        {
            resource = resourceName;
        }

        public string Resource { get { return resource; } }
        public ModelItem ConfigObject { get { return config; } }

    }
}
