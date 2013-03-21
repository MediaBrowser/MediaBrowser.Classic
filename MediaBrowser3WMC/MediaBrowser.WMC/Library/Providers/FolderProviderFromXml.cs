using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Diagnostics;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Providers.Attributes;

namespace MediaBrowser.Library.Providers
{
    [SupportedType(typeof(Folder))]
    class FolderProviderFromXml : BaseMetadataProvider
    {

        [Persist]
        DateTime lastWriteTime = DateTime.MinValue;

        [Persist]
        string folderFile;

        #region IMetadataProvider Members

        public override bool NeedsRefresh()
        {
            // fake stuff like itunes trailers may have no path 
            if (string.IsNullOrEmpty(Item.Path)) { return false; }

            string lastFile = folderFile;

            string mfile = XmlLocation();
            if (!File.Exists(mfile))
                mfile = null;
            if (lastFile != mfile)
                return true;
            if ((mfile == null) && (lastFile == null))
                return false;

          
            DateTime modTime = new FileInfo(mfile).LastWriteTimeUtc;
            DateTime lastTime = lastWriteTime;
            if (modTime <= lastTime)
               return false;
            
            return true;
        }

        private string XmlLocation()
        {
            string location = Item.Path;
            if (File.Exists(location))
            {
                //look for specialized name (for virtual folders) - need to strip out vf name from path...
                //Logger.ReportInfo("Looking for vlocation " + vlocation);
                string cleanName = LibraryManagement.Helper.RemoveInvalidFileChars(Item.Name);
                int len = location.LastIndexOf("\\");
                return Path.Combine(location.Substring(0, len > 0 ? len : location.Length), cleanName + ".folder.xml");
            }
            else
                return Path.Combine(location, "folder.xml");
            
        }

        public override void Fetch()
        {
            var folder = Item as Folder;

            // fake stuff like itunes trailers may have no path 
            if (string.IsNullOrEmpty(Item.Path)) { return; }

            string mfile = XmlLocation();
            //Logger.ReportInfo("Looking for XML file: " + mfile);
            string location = Path.GetDirectoryName(mfile);
            if (File.Exists(mfile))
            {

                Logger.ReportInfo("Found XML file: " + mfile);
                DateTime modTime = new FileInfo(mfile).LastWriteTimeUtc;
                lastWriteTime = modTime;
                folderFile = mfile;
                XmlDocument doc = new XmlDocument();
                doc.Load(mfile);

                string s = doc.SafeGetString("Title/LocalTitle");
                if ((s == null) || (s == ""))
                    s = doc.SafeGetString("Title/OriginalTitle");
                folder.Name = s;
                folder.SortName = doc.SafeGetString("Title/SortTitle");
                
                folder.Overview = doc.SafeGetString("Title/Description");
                if (folder.Overview != null)
                    folder.Overview = folder.Overview.Replace("\n\n", "\n");
                               
             
                string front = doc.SafeGetString("Title/Covers/Front");
                if ((front != null) && (front.Length > 0))
                {
                    front = Path.Combine(location, front);
                    if (File.Exists(front))
                        Item.PrimaryImagePath = front;
                }
                
                
                //using this for logos now
                //string back = doc.SafeGetString("Title/Covers/Back");
                //if ((back != null) && (back.Length > 0))
                //{
                //    back = Path.Combine(location, back);
                //    if (File.Exists(back))
                //        Item.SecondaryImagePath = back;
                //}
               
                //Folder level security data
                if (folder.CustomRating == null)
                    folder.CustomRating = doc.SafeGetString("Title/CustomRating");

                if (folder.CustomPIN == null)
                    folder.CustomPIN = doc.SafeGetString("Title/CustomPIN");

                //
            }
        }



        #endregion
    }
}
