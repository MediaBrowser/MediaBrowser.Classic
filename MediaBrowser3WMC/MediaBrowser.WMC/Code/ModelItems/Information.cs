using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;
using System.IO;
using System.Xml.XPath;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Library;
using System.Diagnostics;

namespace MediaBrowser
{
    /// <summary>
    /// This provides information to the root page on-screen display. You have the option of adding
    /// one-time or recurring messages.
    /// </summary>
    public class Information : ModelItem
    {
        private const int CycleInterval = 4000;
        Timer cycle;
        int counter = 0;

        public Information()
        {
            activityChangeTimer.Tick += majorActivityChanged;
            //AddInformation(new InfomationItem("Welcome to Media Browser.", 2)); 
            AddInformation(new InfomationItem(Library.Kernel.Instance.StringData.GetString("WelcomeProf"), 2));
            Begin();
        }

        #region fields

        string _displayText = string.Empty;
        List<InfomationItem> informationItems = new List<InfomationItem>();

        public string DisplayText
        {
            get
            {
                return _displayText;
            }
            set
            {
                _displayText = value;
                // Im a little worried about this line, if we somehow execue off the UI thread then its messed
                FirePropertyChanged("DisplayText");
            }
        }
        bool changePending = false;
        Timer activityChangeTimer = new Timer()
        {
            AutoRepeat = false,
            Interval = 1000,
            Enabled = false,
        };

        bool _majorActivity = false;
        public bool MajorActivity
        {
            get
            {
                return _majorActivity;
            }
            set
            {
                if (_majorActivity != value)
                {
                    _majorActivity = value;
                    if (!changePending)
                    {
                        FirePropertyChanged("MajorActivity");
                        changePending = true;
                        activityChangeTimer.Start();
                    }
                }
            }
        }
        #endregion

        #region methods

        private void majorActivityChanged(object sender, EventArgs args)
        {
            lock (informationItems)
            {
                FirePropertyChanged("MajorActivity");
                changePending = false;
            }
        }

        public void AddInformation(InfomationItem info)
        {
            lock (informationItems)
            {
                informationItems.Add(info);
            }
        }
        public void AddInformationString(string info)
        {
            lock (informationItems)
            {
                informationItems.Add(new InfomationItem(info));
            }
        }

        private void Begin()
        {
            lock (informationItems)
            {
                if (informationItems.Count > 0)
                    DisplayItem();
            }

            cycle = new Timer(this);
            cycle.Interval = CycleInterval;
            cycle.Tick += delegate { OnRefresh(); };
            cycle.Enabled = true;
        }

        private void OnRefresh()
        {
            lock (informationItems)
            {
                if (informationItems.Count > 0)
                {
                    DisplayItem();
                }
                else
                {
                    DisplayText = string.Empty;
                }
            }
        }

        private void DisplayItem()
        {
            // Get the Info Item from the collection
            InfomationItem ipi = (InfomationItem)informationItems[0];

            // Decrement the recurr interval, then save back to collection.
            ipi.RecurrXTimes--;
            informationItems[0] = ipi;

            // Display Text on screen, and log the message.
            DisplayText = ipi.Description;
            MediaBrowser.Library.Logging.Logger.ReportInfo("Information DisplayText: " + DisplayText);

            // Check the Recurring flag and remove this message once displayed
            if (!ipi.RecurrMessage || ipi.RecurrXTimes <= 0)
                RemoveItem(0);
        }

        private void RemoveItem(int index)
        {
            informationItems.RemoveAt(index);
            counter--;
        }

        #endregion
    }

    #region InformationItem Struct
    public struct InfomationItem
    {
        public string Description;
        public bool RecurrMessage;
        public int RecurrXTimes;

        public InfomationItem(string description)
        {
            this.Description = description;
            this.RecurrMessage = false;
            this.RecurrXTimes = 1;
        }
        public InfomationItem(string description, int recurrInterval)
        {
            this.Description = description;
            if (recurrInterval < 1)
                recurrInterval = 1;
            this.RecurrXTimes = recurrInterval;
            this.RecurrMessage = true;
        }
    }
    #endregion
}
