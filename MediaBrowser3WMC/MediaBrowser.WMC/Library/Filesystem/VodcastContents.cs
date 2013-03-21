using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using MediaBrowser.Library.Network;

namespace MediaBrowser.Library.Filesystem {
    public class VodcastContents {

        const string VODCAST_URL = "url";
        const string FILES_TO_RETAIN = "files_to_retain";
        const string DOWNLOAD_POLICY = "download_policy";

        public VodcastContents() { 
        } 

        public VodcastContents(string contents) {
            AttributedContents data = new AttributedContents(contents);
            Url = data.GetSingleAttribute(VODCAST_URL);

            int filesToRetain;
            int.TryParse(data.GetSingleAttribute(FILES_TO_RETAIN), out filesToRetain);
            FilesToRetain = filesToRetain;

            try {
                DownloadPolicy = (DownloadPolicy)Enum.Parse(typeof(DownloadPolicy), data.GetSingleAttribute(DOWNLOAD_POLICY));
            } catch {
                DownloadPolicy = DownloadPolicy.Stream;
            }
        }

        public DownloadPolicy DownloadPolicy { get; set; }
        public int FilesToRetain { get; set; }
        public string Url { get; set; }

        public string Contents {
            get {
                var generator = new AttributedContents();
                generator.SetSingleAttribute(VODCAST_URL, Url);
                generator.SetSingleAttribute(FILES_TO_RETAIN, FilesToRetain.ToString());
                generator.SetSingleAttribute(DOWNLOAD_POLICY, Enum.GetName(typeof(DownloadPolicy), DownloadPolicy));
                return generator.Contents;
            } 
        } 
    }
}