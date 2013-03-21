using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Code
{
    [Serializable]
    public class KeyFile
    {

        // for the serializer
        public KeyFile ()
	    {
	    }
        public string SupporterKey = "";

        public KeyFile(string file)
        {
            this.file = file;
            this.settings = XmlSettings<KeyFile>.Bind(this, file);
        }

        [SkipField]
        string file;

        [SkipField]
        XmlSettings<KeyFile> settings;

        public static KeyFile FromFile(string file)
        {
            return new KeyFile(file);
        }

        public void Save() {
            this.settings.Write();
        } 
    }
}
