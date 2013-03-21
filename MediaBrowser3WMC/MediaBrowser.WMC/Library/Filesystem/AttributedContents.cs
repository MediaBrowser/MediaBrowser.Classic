using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace MediaBrowser.Library.Filesystem {
    public class AttributedContents {

        Dictionary<string, List<string>> info = new Dictionary<string, List<string>>();

        public AttributedContents() { 
        }

        public AttributedContents(string contents) {
            // splitting on \n cause I want this to work for VFs edited in linux. 
            foreach (var line in contents.Split('\n')) {
                var colonPos = line.IndexOf(':');
                if (colonPos <= 0) {
                    continue;
                }

                var type = line.Substring(0, colonPos).Trim();
                var data = line.Substring(colonPos + 1).Trim();

                if (!string.IsNullOrEmpty(data)) // don't put empty values in
                {
                    if (!info.ContainsKey(type))
                    {
                        info[type] = new List<string>();
                    }

                    info[type].Add(data);
                }
            }
        }


        public string GetSingleAttribute(string key) {
            string rval = null;
            if (info.ContainsKey(key)) { 
                rval =  info[key].FirstOrDefault();
            }
            return rval;
        }

        public void SetSingleAttribute(string key, string value) {
            info[key] = new List<string>() { value }; 
        } 

        public IList<string> GetMultiAttribute(string key) {
            return info.ContainsKey(key) ? info[key] : null;
        }

        public void SetMultiAttribute(string key, IEnumerable<string> values) {
            info[key] = new List<string>(values);
        } 


        public string Contents {
            get {
                StringBuilder contents = new StringBuilder();

                foreach (var pair in info) {
                    foreach (var item in pair.Value) {
                        contents.AppendLine(pair.Key + ": " + item);
                    }
                }
                return contents.ToString();
            }
        }

    }
}