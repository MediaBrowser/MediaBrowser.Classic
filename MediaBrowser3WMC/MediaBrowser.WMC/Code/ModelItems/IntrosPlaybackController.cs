using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.UI;
using Microsoft.MediaCenter.Hosting;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Code.ModelItems
{
    public class IntrosPlaybackController : ModelItem
    {
        protected MediaCollection ItemsToPlay = new MediaCollection();

        private static bool _finished = true;
        public static bool StoppedByUser = false;

        public void Init(List<string> items)
        {
            ItemsToPlay.Clear();
            var pos = 0;
            foreach (var item in items)
            {
                ItemsToPlay.AddItem(item, pos, 0xffff);
                pos++;
            }
        }

        void UpdateStatus(IPropertyObject sender, string property)
        {
            //Logger.ReportVerbose("MBI Property changed called with property: " + property);
        }

        public void Play()
        {
            try
            {
                Logger.ReportInfo("Intros controller about to start all items. Number of items to play: "+ItemsToPlay.Count);
                if (!AddInHost.Current.MediaCenterEnvironment.PlayMedia(MediaType.MediaCollection, ItemsToPlay, false))
                {
                    Logger.ReportInfo("Intros PlayMedia returned false");
                    return;
                }
                _finished = false;
                StoppedByUser = false;
                GoToFullScreen();
                WaitForStream(MediaTransport);
                Attach();
                WaitUntilFinished();
                Disconnect();
            }
            catch (Exception ex)
            {
                Logger.ReportException("MBIntros Playing media failed.", ex);
                _finished = true;
                return;
            }
        }

        protected void PlayNext()
        {
            try
            {
                ItemsToPlay.CurrentIndex++;
                Logger.ReportInfo("Intros controller advancing to item: "+ItemsToPlay.CurrentIndex);
                if (!AddInHost.Current.MediaCenterEnvironment.PlayMedia(MediaType.MediaCollection, ItemsToPlay, false))
                {
                    Logger.ReportInfo("Intros PlayMedia returned false");
                    return;
                }
                //we don't need to attach or wait this thread because the main one is still going but we do need to re-launch back to fullscreen
                GoToFullScreen();
            }
            catch (Exception ex)
            {
                Logger.ReportException("Intros Playing media failed.", ex);
                _finished = true;
            }
        }

        private static void WaitUntilFinished()
        {
            while (!_finished) { Thread.Sleep(100); }
        }

        private static void WaitForStream(MediaTransport transport)
        {
            var i = 0;
            while ((i++ < 15) && (transport != null && transport.PlayState != PlayState.Playing))
            {
                // settng the position only works once it is playing and on fast multicore machines we can get here too quick!
                Thread.Sleep(100);
            }
        }

        public virtual bool GoToFullScreen()
        {
            var mce = MediaExperience;
            Logger.ReportInfo("Intros controller going full screen.");

            if (mce != null)
            {
                mce.GoToFullScreen();
                return true;
            }
            else
            {
                Logger.ReportError("Intros AddInHost.Current.MediaCenterEnvironment.MediaExperience is null, we have no way to go full screen!");
                return false;

            }
        }

        protected MediaExperience MediaExperience
        {
            get
            {
                var mce = AddInHost.Current.MediaCenterEnvironment.MediaExperience;

                // great window 7 has bugs, lets see if we can work around them 
                if (mce == null)
                {
                    System.Threading.Thread.Sleep(200);
                    mce = AddInHost.Current.MediaCenterEnvironment.MediaExperience;
                    if (mce == null)
                    {
                        try
                        {
                            var fi = AddInHost.Current.MediaCenterEnvironment.GetType()
                                .GetField("_checkedMediaExperience", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (fi != null)
                            {
                                fi.SetValue(AddInHost.Current.MediaCenterEnvironment, false);
                                mce = AddInHost.Current.MediaCenterEnvironment.MediaExperience;
                            }

                        }
                        catch (Exception e)
                        {
                            // give up ... I do not know what to do 
                            Logger.ReportException("MBIntros AddInHost.Current.MediaCenterEnvironment.MediaExperience is null", e);
                        }

                    }
                }

                return mce;
            }
        }

        private MediaTransport mediaTransport;
        protected MediaTransport MediaTransport
        {
            get
            {
                if (mediaTransport != null) return mediaTransport;
                try
                {
                    MediaExperience experience = MediaExperience;

                    if (experience != null)
                    {
                        mediaTransport = experience.Transport;
                    }
                    else
                    {
                        Logger.ReportError("Intros MediaExperience is null");
                        _finished = true; // make sure we get out
                    }
                }
                catch (InvalidOperationException e)
                {
                    // well if we are inactive we are not allowed to get media experience ...
                    Logger.ReportException("EXCEPTION : ", e);
                    _finished = true;
                }
                return mediaTransport;
            }
        }

        protected virtual void Attach()
        {
            var transport = MediaTransport;
            if (transport != null)
            {
                Logger.ReportInfo("Intros Attaching...");
                transport.PropertyChanged += new PropertyChangedEventHandler(TransportPropertyChanged);
            }
        }

        protected virtual void Disconnect()
        {
            var transport = MediaTransport;
            if (transport != null)
            {
                Logger.ReportInfo("Intros Disconnecting...");
                transport.PropertyChanged -= new PropertyChangedEventHandler(TransportPropertyChanged);
            }
        }

        DateTime lastCall = DateTime.Now;

        void TransportPropertyChanged(IPropertyObject sender, string property)
        {
            // protect against really agressive calls
            var diff = (DateTime.Now - lastCall).TotalMilliseconds;
            if (diff < 1000 && diff >= 0 || property != "PlayState")
            {
                return;
            }

            Logger.ReportVerbose("Intros TransportPropertyChanged was called with property = " + property+" Title is: "+MediaExperience.MediaMetadata["Title"]);

            lastCall = DateTime.Now;
            var transport = MediaTransport;
            if (transport.PlayState == PlayState.Stopped || transport.PlayState == PlayState.Finished || transport.PlayState == PlayState.Undefined)
            {
                if (!ItemsToPlay.IsActive)
                {
                    //if we finish normally, IsActive will still be true - if we are stopped by the user it will be false
                    //transport.Playstate appears to always be undefined when stopped whether finished normally or stopped on purpose
                    Logger.ReportInfo("MBIntros stopped by user.");
                    if (ItemsToPlay.CurrentIndex < ItemsToPlay.Count - 1)
                    {
                        //advance to next item in list
                        this.PlayNext();
                        return;
                    }
                    else
                    {
                        StoppedByUser = true;
                    }
                }
                Logger.ReportInfo("Playback finished. " + _finished);
                Thread.Sleep(500); // give the player time to exit
                _finished = true;
            }
        }
    }
}
