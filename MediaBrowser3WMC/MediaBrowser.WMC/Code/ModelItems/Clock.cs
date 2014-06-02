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
            Time = DateTime.Now.ToString("t");
        }
    }

}
