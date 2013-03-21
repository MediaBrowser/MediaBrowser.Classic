using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Windows;
using System.Text;

namespace Configurator.Code
{
    class LibraryFolder
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Path { get; set; }
        public bool HasXML = false;
        public string CustomRating { get; set; }
        public string Overview { get; set; }
        private string xmlName { get; set; }
        private XmlDocument doc;

        public LibraryFolder(string folderName)
        {
            this.FullPath = folderName;
            int start = folderName.LastIndexOf("\\")+1;
            int end = folderName.LastIndexOf(".");
            if (end < start) end = folderName.Length; // if no extension on last name
            this.Name = folderName.Substring(start, end - start);
            this.Path = folderName.Substring(0, start);
            if (FullPath.ToLower().EndsWith(".vf")) {
                xmlName = Path+Name+".folder.xml";
            } else {
                xmlName = FullPath + "\\folder.xml";
            }
            if (File.Exists(xmlName)) {
                loadXML(xmlName);
            }
        }

        private void loadXML(string filename)
        {
            this.HasXML = true;
            this.xmlName = filename;
            doc = new XmlDocument();
            try
            {
                doc.Load(filename);
            } catch(XmlException e) {
                MessageBox.Show(e.Message, "Error in XML: "+filename);
            }

            this.CustomRating = doc.SafeGetString("Title/CustomRating");
            this.Overview = doc.SafeGetString("Title/Description");

        }

        public void SaveXML()
        {
            SaveXML(xmlName);
        }

        public void DeleteXML()
        {
            try
            {
                if (File.Exists(xmlName))
                {
                    File.Delete(xmlName);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Unable to delete folder.xml");
            }
        }

        public void SaveXML(string file)
        {
            XmlNode title;
            XmlNode rating;

            if (!HasXML)
            {
                HasXML = true;
                this.doc = new XmlDocument();

                title = doc.CreateElement("Title");
                rating = doc.CreateElement("CustomRating");
                rating.InnerText = CustomRating;
                title.AppendChild(rating);
                doc.AppendChild(title);
            }
            else
            {
                rating = doc.SelectNodes("Title/CustomRating")[0];
                if (rating != null)
                {
                    rating.InnerText = CustomRating;
                }
                else
                {
                    //xml not what we were expecting - create node
                    title = doc.SelectNodes("Title")[0];
                    if (title == null)
                        title = doc.CreateElement("Title");
                    rating = doc.CreateElement("CustomRating");
                    rating.InnerText = CustomRating;
                    title.AppendChild(rating);
                    doc.AppendChild(title);
                }

            }
            

            // Save the document to a file and auto-indent the output.
            try
            {
                XmlTextWriter writer = new XmlTextWriter(file, null);
                writer.Formatting = Formatting.Indented;
                doc.Save(writer);
                writer.Close();
            }
            catch
            {
                MessageBox.Show("Error writing to file " + file,"Configurator");
            }
        }


        public override string ToString()
        {
            return Name;
        }
    }
}
