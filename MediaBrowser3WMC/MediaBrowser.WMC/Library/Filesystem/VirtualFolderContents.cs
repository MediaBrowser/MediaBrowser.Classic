using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace MediaBrowser.Library.Filesystem {
    public class VirtualFolderContents {

        const string Image = "image";
        const string Folder = "folder";
        const string SortOrder = "sortorder";

        List<string> folders = new List<string>();

        public VirtualFolderContents(string contents) {

            var parsed = new AttributedContents(contents);
            ImagePath = parsed.GetSingleAttribute(Image);

            SortName = parsed.GetSingleAttribute(SortOrder);

            var foundFolders = parsed.GetMultiAttribute(Folder);
            if (foundFolders != null) {
                this.folders.AddRange(foundFolders);
            } 
        }

        public List<string> Folders { 
            get {
                // don't allow people to monkey with the folder list
                return folders.ToList(); 
            }
        }

        public void AddFolder(string folder) { 
            if (!folders.Contains(folder)) {
                folders.Add(folder); 
            }
        }

        public void RemoveFolder(string folder) {
            folders.Remove(folder);
        } 

        public string ImagePath { get; set; }

        public string SortName { get; set; }

        public string Contents {
            get {
                var generator = new AttributedContents();

                if (ImagePath != null) {
                    generator.SetSingleAttribute(Image, ImagePath);
                }

                if (folders.Count > 0) {
                    generator.SetMultiAttribute(Folder, folders);
                }

                if (!string.IsNullOrEmpty(SortName))
                {
                    generator.SetSingleAttribute(SortOrder, SortName);
                }

                return generator.Contents;
            }
        }
    
    }
}
