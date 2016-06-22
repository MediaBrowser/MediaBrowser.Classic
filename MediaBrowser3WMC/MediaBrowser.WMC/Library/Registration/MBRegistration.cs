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
            
            var rec = new MBRegistrationRecord();
            Async.Queue(Async.ThreadPoolName.MBRegistration, () => //feature + " registration check", () =>
            {
                try
                {
                    var mb3Rec = Kernel.ApiClient.GetRegistrationStatus(feature, feature); // pass our feature as the MB2 Equiv
                    rec.ExpirationDate = mb3Rec.ExpirationDate;
                    rec.IsRegistered = mb3Rec.IsRegistered;
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
            
    }
}
