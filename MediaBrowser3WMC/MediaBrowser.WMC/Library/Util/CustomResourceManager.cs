using System.Collections.Generic;
using Microsoft.MediaCenter.Hosting;
using Microsoft.MediaCenter;
using System.Diagnostics;
using System.IO;
using System;
using System.Threading;
using MediaBrowser.LibraryManagement;
using System.Xml;
using System.Reflection;
using Microsoft.MediaCenter.UI;
using System.Text;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library;


namespace MediaBrowser.Library.Util
{
    public static class CustomResourceManager
    {
        private const string STYLES_FILE = "Styles_DoNotEdit.mcml";
        private const string CUSTOM_STYLE_FILE = "CustomStyles.mcml";
        private const string FONTS_FILE = "Fonts_DoNotEdit.mcml";
        private const string CUSTOM_FONTS_FILE = "CustomFonts.mcml";

        public static void SetupFontsMcml(AddInHost host)
        {
            try
            {
                string file = Path.Combine(ApplicationPaths.AppConfigPath, FONTS_FILE);
                string custom = Path.Combine(ApplicationPaths.AppConfigPath, CUSTOM_FONTS_FILE);
                if (File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }
                if (File.Exists(custom))
                {
                    Logger.ReportInfo("Using custom fonts mcml");
                    if (!VerifyXmlResource(custom, Resources.FontsDefault))
                    {
                        host.MediaCenterEnvironment.Dialog(Application.CurrentInstance.StringData("FontsMissingDial"), CUSTOM_FONTS_FILE, DialogButtons.Ok, 100, true);
                    }
                    File.Copy(custom, file);
                }
                else
                {
                    switch (Config.Instance.FontTheme)
                    {
                        case "Small":
                            File.WriteAllBytes(file, Resources.FontsSmall);
                            break;
                        case "Default":
                        default:
                            File.WriteAllBytes(file, Resources.FontsDefault);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error creating Fonts_DoNotEdit.mcml", ex);
                throw;
            }
        }

        public static void SetupStylesMcml(AddInHost host)
        {
            try
            {
                string file = Path.Combine(ApplicationPaths.AppConfigPath, "Styles_DoNotEdit.mcml");
                string custom = Path.Combine(ApplicationPaths.AppConfigPath, CUSTOM_STYLE_FILE);
                if (File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }
                if (File.Exists(custom))
                {
                    Logger.ReportInfo("Using custom styles mcml");
                    if (!VerifyXmlResource(custom, Resources.StylesDefault))
                    {
                        host.MediaCenterEnvironment.Dialog(string.Format(Application.CurrentInstance.StringData("StyleMissingDial"), CUSTOM_STYLE_FILE), CUSTOM_STYLE_FILE, DialogButtons.Ok, 100, true);
                    }
                    File.Copy(custom, file);
                }
                else
                {
                    // new options must be added to the ThemeModel choice in configpage.mcml
                    switch (Config.Instance.Theme)
                    {
                        case "Black":
                            File.WriteAllBytes(file, Resources.StylesBlack);
                            break;
                        case "Extender Default":
                            File.WriteAllBytes(file, Resources.StylesDefaultExtender);
                            break;
                        case "Extender Black":
                            File.WriteAllBytes(file, Resources.StylesBlackExtender);
                            break;
                        case "Default":
                        default:
                            File.WriteAllBytes(file, Resources.StylesDefault);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error creating Styles_DoNotEdit.mcml", ex);
                throw;
            }

        }

        /// <summary>
        /// Adds fonts to the font file used by MB.  Will discover custom font file named [prefix]CustomFonts.mcml.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="stdFontResource"></param>
        /// <returns></returns>
        public static bool AppendFonts(string prefix, byte[] stdFontResource)
        {
            return AppendFonts(prefix, stdFontResource, stdFontResource);
        }

        /// <summary>
        /// Adds fonts to the font file used by MB.  Will discover custom font file named [prefix]CustomFonts.mcml.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="stdFontResource"></param>
        /// <param name="smallFontResource"></param>
        /// <returns></returns>
        public static bool AppendFonts(string prefix, byte[] stdFontResource, byte[] smallFontResource)
        {
            string file = Path.Combine(ApplicationPaths.AppConfigPath, FONTS_FILE);
            string custom = Path.Combine(ApplicationPaths.AppConfigPath, prefix + CUSTOM_FONTS_FILE);
            if (File.Exists(custom))
            {
                Logger.ReportInfo("Using custom fonts file: " + custom);
                if (!VerifyXmlResource(custom, stdFontResource))
                {
                    Logger.ReportWarning(custom + " has been patched with missing values");
                }
                try
                {
                    AppendXML(file, File.ReadAllBytes(custom));
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error appending fonts file with " + custom, ex);
                    return false;
                }
            }
            else
            {
                try
                {
                    switch (Config.Instance.FontTheme)
                    {
                        case "Small":
                            AppendXML(file, smallFontResource);
                            break;
                        case "Default":
                        default:
                            AppendXML(file, stdFontResource);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error appending fonts file", ex);
                    return false;
                }
            }
            return true;
        }

        public static bool AppendStyles(string prefix, byte[] stdStyleResource, byte[] blackStyleResource)
        {
            string file = Path.Combine(ApplicationPaths.AppConfigPath, STYLES_FILE);
            string custom = Path.Combine(ApplicationPaths.AppConfigPath, prefix + CUSTOM_STYLE_FILE);
            if (File.Exists(custom))
            {
                Logger.ReportInfo("Using custom styles file: " + custom);
                if (!VerifyXmlResource(custom, stdStyleResource))
                {
                    Logger.ReportWarning(custom + " has been patched with missing values");
                }
                try
                {
                    AppendXML(file, File.ReadAllBytes(custom));
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error appending styles file with " + custom, ex);
                    return false;
                }
            }
            else
            {
                try
                {
                    switch (Config.Instance.Theme)
                    {
                        case "Black":
                            AppendXML(file, blackStyleResource);
                            break;
                        case "Default":
                        default:
                            AppendXML(file, stdStyleResource);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error appending styles file", ex);
                    return false;
                }
            }
            return true;
        }

        private static void AppendXML(string sourceFile, byte[] resource)
        {
            XmlDocument fontFile = new XmlDocument();
            try
            {
                fontFile.Load(sourceFile);
            }
            catch
            {
                throw new ApplicationException(sourceFile + " is not well formed xml");
            }
            XmlDocument appendData = new XmlDocument();
            using (MemoryStream ms = new MemoryStream(resource))
            {
                appendData.Load(ms);
            }
            List<XmlNode> newFonts = new List<XmlNode>();
            foreach (XmlNode n in appendData.DocumentElement.ChildNodes)
            {
                newFonts.Add(n);
            }
            foreach (XmlNode n in newFonts)
            {
                fontFile.DocumentElement.AppendChild(fontFile.ImportNode(n, true));
            }
            fontFile.Save(sourceFile);
        }



        public static bool VerifyXmlResource(string filename, byte[] resource)
        {
            XmlDocument custom = new XmlDocument();
            try
            {
                custom.Load(filename);
            }
            catch
            {
                throw new ApplicationException(filename + " is not well formed xml");
            }
            XmlDocument def = new XmlDocument();
            using (MemoryStream ms = new MemoryStream(resource))
            {
                def.Load(ms);
            }
            List<XmlNode> missingNodes = new List<XmlNode>();
            foreach (XmlNode node in def.SelectNodes("//*[@Name]"))
            {
                if (custom.SelectSingleNode(string.Format("//*[@Name='{0}']", node.Attributes["Name"].Value)) == null)
                    missingNodes.Add(node);
            }
            if (missingNodes.Count > 0)
            {
                foreach (XmlNode n in missingNodes)
                {
                    custom.DocumentElement.AppendChild(custom.ImportNode(n, true));
                }
                custom.Save(filename);
                return false;
            }
            try
            {
                Type m = Type.GetType("Microsoft.MediaCenter.UI.Template.MarkupSystem,Microsoft.MediaCenter.UI");
                MethodInfo mi = m.GetMethod("Load", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                object sys = Activator.CreateInstance(m);
                object r = mi.Invoke(sys, new object[] { "file://" + filename });
                LoadResult lr = (LoadResult)r;
                if (lr.Status != LoadResultStatus.Success)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (string s in lr.Errors)
                        sb.AppendLine(s);
                    throw new ApplicationException("Error loading " + filename + "\n" + sb.ToString());
                }
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Error attempting to verify custom mcml files. Microsoft may have changed the internals of Media Center.\n" + ex.ToString());
            }
            return true;
        }

    }
}
