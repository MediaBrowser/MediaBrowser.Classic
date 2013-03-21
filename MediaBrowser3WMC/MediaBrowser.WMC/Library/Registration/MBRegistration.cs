using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Threading;

namespace MediaBrowser.Library.Registration
{
    public class MBRegistrationRecord
    {
        public DateTime ExpirationDate = DateTime.MaxValue;
        public bool IsRegistered = false;
        public bool RegChecked = false;
        public bool RegError = false;
        private bool? isInTrial = null;
        public bool TrialVersion
        {
            get
            {
                if (isInTrial == null)
                {
                    if (!RegChecked) return false; //don't set this until we've successfully obtained exp date
                    isInTrial = ExpirationDate > DateTime.Now;
                }
                return (isInTrial.Value && !IsRegistered);
            }
        }
        public bool IsValid
        {
            get
            {
                if (RegChecked)
                    return (IsRegistered || TrialVersion);
                else
                    return true;
            }
        }
    }

    public static class MBRegistration
    {
        /// <summary>
        /// Get registration information for a given feature and version.  Will return a MBRegistrationRecord instance.
        /// The return will be immediate, but the registration status will be filled in asynchronously.  The 'RegChecked'
        /// attribute of the returned record will be set to true when validation is finished.  'RegError' is set to true
        /// if an error is encountered.
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="version"></param>
        /// <returns>A MBRegistrationRecord (filled in asynchronously)</returns>
        public static MBRegistrationRecord GetRegistrationStatus(string feature, System.Version version)
        {
            MBRegistrationRecord rec = new MBRegistrationRecord();
            Async.Queue(feature + " registration check", () =>
            {
                try
                {
                    rec.ExpirationDate = GetExpirationDate(feature, version);
                    rec.IsRegistered = Validate(feature);
                }
                catch
                {
                    //we reported on the error in the routine that caused it.  Just mark our record.
                    rec.RegError = true;
                }
                rec.RegChecked = true;
            });
            return rec;
        }
            
        private static DateTime GetExpirationDate(string feature, System.Version version)
        {
            try
            {
                string path = "http://www.mediabrowser.tv/registration/hits?feature=" + feature + "&version=" + version.ToString() + "&key=" + Kernel.Instance.ConfigData.SupporterKey + "&mac_addr=" + Helper.GetMACAddress();
                WebRequest request = WebRequest.Create(path);
                var response = request.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    byte[] buffer = new byte[11];
                    stream.Read(buffer, 0, 11);
                    response.Close();
                    stream.Close();
                    return DateTime.ParseExact(System.Text.Encoding.ASCII.GetString(buffer).Trim(), "yyyy-MM-dd", new System.Globalization.CultureInfo("en-US"));
                }
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error obtaining expiration date for feature: " + feature, ex);
                throw new ApplicationException();
            }
        }

        private static bool Validate(string feature)
        {
            string path = "http://www.mediabrowser.tv/registration/registrations?feature="+feature+"&key=" + Kernel.Instance.ConfigData.SupporterKey;
            bool valid = false;
            try
            {
                WebRequest request = WebRequest.Create(path);
                var response = request.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    byte[] buffer = new byte[5];
                    stream.Read(buffer, 0, 5);
                    response.Close();
                    stream.Close();
                    string res = System.Text.Encoding.ASCII.GetString(buffer).Trim();
                    //Logger.ReportInfo("MB validation result: " + res);
                    valid = (res.StartsWith("true"));
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error checking registration status of " + feature, e);
                throw new ApplicationException();
            }
            return valid;
        }
    }
}
