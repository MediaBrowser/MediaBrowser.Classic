using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Filesystem;

namespace Configurator {

    public class VirtualFolder {

        VirtualFolderContents contents;

        string path;

        public VirtualFolder(string path) {
            this.path = path;
            contents = new VirtualFolderContents(File.ReadAllText(path));
        }

        public string Path { get { return path; } }

        public void RemoveFolder(string folder) {
            contents.RemoveFolder(folder);
            Save();
        }

        public void AddFolder(string folder) {
            contents.AddFolder(folder);
            Save();
        }

        public void Save() {
            File.WriteAllText(path, contents.Contents);
        }

        public List<string> Folders { get { return contents.Folders; } }

        public string ImagePath {
            get { return contents.ImagePath; }
            set { contents.ImagePath = value; Save(); }
        }

        public string Name {
            get { return System.IO.Path.GetFileNameWithoutExtension(path); }
            set {
                //first change name of xml if its there
                try
                {
                    string xmlPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + ".folder.xml");
                    if (File.Exists(xmlPath))
                    {
                        File.Move(xmlPath, System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), value + ".folder.xml"));
                    }
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error attempting to rename folder.xml file", ex);
                }
                //now actual file
                try
                {
                    string newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), value + ".vf");
                    File.Move(path, newPath);
                    path = newPath;
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error attempting to rename virtual folder", ex);
                }

            }
        }

        public string SortName
        {
            get { return contents.SortName; }
            set { contents.SortName = value; Save(); }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
