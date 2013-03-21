using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Globalization;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Logging;


namespace MediaBrowser.Library.Localization
{
    public class LocalizedStringData
    {

        public string FileName; //this is public so it will serialize and we know where to save ourselves
        public string Version = ""; //this will get saved so we can check it against us for changes


        protected LocalizedStringData(string file)
        {
            this.FileName = file;
        }

        protected LocalizedStringData()
        {
        }

        public static string GetFileName()
        {
            return GetFileName("");
        }

        public static string GetFileName(string prefix)
        {
            string path = ApplicationPaths.AppLocalizationPath;
            string name = Path.Combine(path, prefix+"strings-" + CultureInfo.CurrentCulture + ".xml");
            if (File.Exists(name))
            {
                return name;
            }
            else
            {
                name = Path.Combine(path, prefix+"strings-" + CultureInfo.CurrentCulture.Parent + ".xml");
                if (File.Exists(name))
                {
                    return name;
                }
                else
                {
                    //just return default
                    return Path.Combine(path, prefix+"strings-en.xml");
                }
            }
        }


        public void Save()
        {
            Save(FileName);
        }

        public void Save(string file)
        {
            XmlSerializer xs = new XmlSerializer(this.GetType());
            using (FileStream fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                xs.Serialize(fs, this);
            }
        }
    }
}
