using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.MediaCenter.UI;

namespace MediaBrowser {

    public class Clock : ModelItem {
        private string _time = String.Empty;
        private Timer _timer;


        public Clock() {
            _timer = new Timer(this);
            _timer.Interval = 10000;
            _timer.Tick += delegate { RefreshTime(); };
            _timer.Enabled = true;

            RefreshTime();
        }

        // Current time. 
        public string Time {
            get { return _time; }
            set {
                if (_time != value) {
                    _time = value;
                    FirePropertyChanged("Time");
                }
            }
        }

        // Try to update the time.
        private void RefreshTime() {
            // todo (decide if we want to localize) 
            // this forces the format, and I tested every possibility for a hang. 
            Time = DateTime.Now.ToString("h:mm tt");
            //time = time.AddMinutes(1);
            //Time = time.ToString("h:mm tt"); ; 
        }
    }

}
