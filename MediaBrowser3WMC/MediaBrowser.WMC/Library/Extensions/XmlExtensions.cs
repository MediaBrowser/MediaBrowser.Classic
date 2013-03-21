using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Globalization;

namespace System.Xml
{
    public static class XmlExtensions
    {

        public static int SafeGetInt32(this XmlDocument doc, string path)
        {
            return SafeGetInt32(doc, path, 0);
        }

        public static int SafeGetInt32(this XmlDocument doc, string path, int defaultInt)
        {
            XmlNode rvalNode = doc.SelectSingleNode(path);
            if (rvalNode != null && rvalNode.InnerText.Length > 0)
            {
                int rval;
                if (Int32.TryParse(rvalNode.InnerText, out rval))
                {
                    return rval;
                }

            }
            return defaultInt;
        }

        private static CultureInfo _usCulture = new CultureInfo("en-US");

        public static float SafeGetSingle(this XmlDocument doc, string path, float minValue, float maxValue)
        {
            XmlNode rvalNode = doc.SelectSingleNode(path);
            if (rvalNode != null && rvalNode.InnerText.Length > 0)
            {
                float rval;
                // float.TryParse is local aware, so it can be probamatic, force us culture
                if (float.TryParse(rvalNode.InnerText, NumberStyles.AllowDecimalPoint, _usCulture, out rval))
                {
                    if (rval >= minValue && rval <= maxValue)
                    {
                        return rval;
                    }
                }

            }
            return minValue;
        }


        public static string SafeGetString(this XmlDocument doc, string path)
        {
            return SafeGetString(doc, path, null);
        }

        public static string SafeGetString(this XmlDocument doc, string path, string defaultString)
        {
            XmlNode rvalNode = doc.SelectSingleNode(path);
            if (rvalNode != null && rvalNode.InnerText.Trim().Length > 0)
            {
                return rvalNode.InnerText;
            }
            return defaultString;
        }

        public static string SafeGetString(this XmlNode doc, string path)
        {
            return SafeGetString(doc, path, null);
        }

        public static string SafeGetString(this XmlNode doc, string path, string defaultValue)
        {
            XmlNode rvalNode = doc.SelectSingleNode(path);
            if (rvalNode != null && rvalNode.InnerText.Length > 0)
            {
                return rvalNode.InnerText;
            }
            return defaultValue;
        }
    }

}