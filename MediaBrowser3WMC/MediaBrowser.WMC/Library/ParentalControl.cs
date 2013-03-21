using System;
using System.Collections.Generic;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Playables;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.UI;

namespace MediaBrowser.Library
{
    public class ParentalControl
    {
        public ParentalControl()
        {
            this.Initialize();
        }

        private bool gettingNewPIN = false; // used to signal that we should replace PIN instead of validate
        private Ratings ratings;
        private Timer _relockTimer;
        private DateTime unlockedTime { get; set; }  // time library was unlocked
        private int unlockPeriod { get; set; } //private storage for unlock period
        private string customPIN { get; set; } //local storage for PIN to be checked against
        private List<Folder> enteredProtectedFolders;
        ParentalPromptCompletedCallback pinCallback;

        //item and properties to operate upon after pin entered
        private Item anItem;
        private PlayableItem playable;

        public void Initialize()
        {
            // initialize internal settings
            //setup timer for auto re-lock
            //setup timer for auto re-lock - must be done on app thread
            if (!Microsoft.MediaCenter.UI.Application.IsApplicationThread)
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(initTimer);
            else
                initTimer(null);

            // init list of folders we've gained access to
            enteredProtectedFolders = new List<Folder>();
            // construct ratings object
            ratings = new Ratings(Config.Instance.ParentalBlockUnrated);

            Logger.ReportInfo("Parental Control Initialized");

            //check to see if this is run with clean or recently created cache
            //string itemCache = Path.Combine(ApplicationPaths.AppCachePath, "items");
            //DateTime recentTime = DateTime.Now.Subtract(DateTime.Now.Subtract(DateTime.Now.AddMinutes(-1)));  //if cache dir created less than a minute ago we must've just done it
            //if (!Directory.Exists(itemCache) || DateTime.Compare(Directory.GetCreationTime(itemCache), recentTime) > 0)
            //{
            //    if (Config.Instance.ParentalBlockUnrated)
            //    {
            //        //blocking unrated content - unlock the library temporarily to allow items to populate their metadata
            //        Logger.ReportInfo("Unlocking Library to allow initial cache population.");
            //        this.Unlocked = true; //can't invoke the timer yet - it may not even be initialized
            //    }
            //}
            return;
        }

        private void initTimer(object args)
        {
            _relockTimer = new Timer();
            _relockTimer.Enabled = false; //don't need this until we unlock
            _relockTimer.Interval = 600000; //10 minutes is plenty often enough because re-lock time is in hours
            _relockTimer.Tick += new EventHandler(_relock_Timer_Tick);
        }

        void _relock_Timer_Tick(object sender, EventArgs e)
        {
            if (DateTime.UtcNow >= this.unlockedTime.AddHours(this.unlockPeriod)) this.Relock();
        }

        public void ClearEnteredList()
        {
            //Logger.ReportInfo("Cleared Entered Protected Folder List");
            enteredProtectedFolders.Clear(); //clear out the list
        }

        public bool Enabled
        {
            get
            {
                return (Config.Instance.ParentalControlEnabled);
            }
        }

        public bool Unlocked { get; set; }

        public int MaxAllowed
        {
            get { return Config.Instance.MaxParentalLevel; }
        }

        public string MaxAllowedString
        {
            get
            {
                return Ratings.ToString(MaxAllowed) ?? ""; //return something valid if not there
            }
        }

        private bool addProtectedFolder(FolderModel folder)
        {
            if (folder != null)
            {
                enteredProtectedFolders.Add(folder.Folder);
                return true;
            }
            else
                return false;
        }

        public bool ProtectedFolderEntered(Folder folder)
        {
            return enteredProtectedFolders.Contains(folder);
        }

        public bool Allowed(Item item)
        {
            if (this.Enabled && item != null)
            {
                //Logger.ReportInfo("Checking parental status on " + item.Name + " "+item.ParentalRating+" "+this.MaxAllowed.ToString());
                return (Ratings.Level(item.ParentalRating) <= this.MaxAllowed);
            }
            else return true;
        }

        public bool Allowed(BaseItem item)
        {
            if (this.Enabled && item != null)
            {
                //Logger.ReportInfo("Checking parental status on " + item.Name + " " + item.ParentalRating + " " + this.MaxAllowed.ToString());
                return (Ratings.Level(item.ParentalRating) <= this.MaxAllowed);
            }
            else return true;
        }

        public List<BaseItem> RemoveDisallowed(List<BaseItem> items)
        {
            List<BaseItem> allowedItems = new List<BaseItem>();
            foreach (BaseItem i in items)
            {
                if (this.Allowed(i))
                {
                    allowedItems.Add(i);
                }
                else
                {
                    //Logger.ReportVerbose("Removed Disallowed Item: " + i.Name + ". Rating '" + i.ParentalRating + "' Exceeds Limit of " + this.MaxAllowed.ToString() + ".");
                }
            }
            //Logger.ReportVerbose("Finished Removing PC Items");
            return allowedItems;
        }


        public void StopReLockTimer()
        {
            //called if parental control is turned off - in case the timer was going
            _relockTimer.Stop();
            return;
        }

        public void SwitchUnrated(bool block)
        {
            ratings.SwitchUnrated(block);
        }


        public void NavigateProtected(FolderModel folder)
        {
            //save parameters where we can get at them after pin entry
            this.anItem = folder;

            //now present pin screen - it will call our callback after finished
            pinCallback = NavPinEntered;
            if (folder.BaseItem.CustomPIN != "" && folder.BaseItem.CustomPIN != null)
                customPIN = folder.BaseItem.CustomPIN; // use custom pin for this item
            else
                customPIN = Config.Instance.ParentalPIN; // use global pin
            Logger.ReportInfo("Request to open protected content " + folder.Name);
            PromptForPin(pinCallback, Application.CurrentInstance.StringData("EnterPINToViewDial"));
        }

        public void NavPinEntered(bool pinCorrect)
        {
            MediaCenterEnvironment env = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
            if (pinCorrect)
            {
                Logger.ReportInfo("Opening protected content " + anItem.Name);
                //add to list of protected folders we've entered
                addProtectedFolder(anItem as FolderModel);
                Application.CurrentInstance.OpenSecure(anItem as FolderModel);
            }
            else
            {
                env.Dialog(Application.CurrentInstance.StringData("IncorrectPINDial"), Application.CurrentInstance.StringData("ContentProtected"), DialogButtons.Ok, 60, true);
                Logger.ReportInfo("PIN Incorrect attempting to open " + anItem.Name);
                Application.CurrentInstance.BackOut(); //clear the PIN page
            }
        }

        public void PlayProtected(PlayableItem playable)
        {
            //save parameters where we can get at them after pin entry
            this.playable = playable;

            //now present pin screen - it will call our callback after finished
            pinCallback = PlayPinEntered;
            if (!string.IsNullOrEmpty(playable.ParentalControlPin))
                customPIN = playable.ParentalControlPin; // use custom pin for this item
            else
                customPIN = Config.Instance.ParentalPIN; // use global pin
            Logger.ReportInfo("Request to play protected content");
            PromptForPin(pinCallback, Application.CurrentInstance.StringData("EnterPINToPlayDial"));
        }

        public void PlayPinEntered(bool pinCorrect)
        {
            Application.CurrentInstance.Back(); //clear the PIN page before playing
            MediaCenterEnvironment env = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;

            string name = playable.DisplayName;

            if (pinCorrect)
            {
                Logger.ReportInfo("Playing protected content " + name);
                Application.CurrentInstance.PlaySecure(playable);
            }
            else
            {
                env.Dialog(Application.CurrentInstance.StringData("IncorrectPINDial"), Application.CurrentInstance.StringData("ContentProtected"), DialogButtons.Ok, 60, true);
                Logger.ReportInfo("Pin Incorrect attempting to play " + name);
            }
        }


        public void EnterNewPIN()
        {
            //now present pin screen - it will call our callback after finished
            pinCallback = NewPinEntered;
            customPIN = Config.Instance.ParentalPIN; // use global pin
            Logger.ReportInfo("Request to change PIN");
            PromptForPin(pinCallback, Application.CurrentInstance.StringData("EnterCurrentPINDial"));
        }

        public void NewPinEntered(bool pinCorrect)
        {
            MediaCenterEnvironment env = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
            if (pinCorrect)
            {
                Logger.ReportInfo("Entering New PIN");
                gettingNewPIN = true; //set flag
                Application.CurrentInstance.OpenSecurityPage(Application.CurrentInstance.StringData("EnterNewPINDial"));
            }
            else
            {
                env.Dialog(Application.CurrentInstance.StringData("IncorrectPINDial"), Application.CurrentInstance.StringData("CantChangePINDial"), DialogButtons.Ok, 60, true);
                Logger.ReportInfo("PIN Incorrect attempting change PIN ");
            }
        }

        public void UnlockPinEntered(bool pinCorrect)
        {
            MediaCenterEnvironment env = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
            if (pinCorrect)
            {
                unlockLibrary();
                env.Dialog(string.Format(Application.CurrentInstance.StringData("LibraryUnlockedDial"), this.unlockPeriod.ToString()), Application.CurrentInstance.StringData("LibraryUnlockedCapDial"), DialogButtons.Ok, 60, true);
                Application.CurrentInstance.Back(); //clear PIN screen
                if (Config.Instance.HideParentalDisAllowed)
                {
                    if (Application.CurrentInstance.CurrentFolder != null && Application.CurrentInstance.CurrentFolder != Application.CurrentInstance.RootFolderModel)
                    {
                        Application.CurrentInstance.CurrentFolder.RefreshChildren();
                    }
                    if (Application.CurrentInstance.RootFolderModel != null)
                    {
                        Application.CurrentInstance.RootFolderModel.RefreshChildren();
                    }
                }
            }
            else
            {
                env.Dialog(Application.CurrentInstance.StringData("IncorrectPINDial"), Application.CurrentInstance.StringData("LibraryUnlockedCapDial"), DialogButtons.Ok, 60, true);
                Application.CurrentInstance.Back(); //clear PIN screen
                Logger.ReportInfo("PIN Incorrect attempting to unlock library.");
            }
        }

        public void Unlock()
        {
            // just kick off the enter pin page - it will call our function when complete
            pinCallback = UnlockPinEntered;
            customPIN = Config.Instance.ParentalPIN; // use global pin
            Logger.ReportInfo("Request to unlock PC");
            PromptForPin(pinCallback, Application.CurrentInstance.StringData("EnterPINDial"));
        }

        private void unlockLibrary()
        {
            Config.Instance.ParentalControlUnlocked = true;
            this.unlockedTime = DateTime.UtcNow;
            this.unlockPeriod = Config.Instance.ParentalUnlockPeriod;
            _relockTimer.Start(); //start our re-lock timer
            if (Config.Instance.HideParentalDisAllowed)
            {
                Application.CurrentInstance.RootFolderModel.RefreshChildren();
            }
        }

        private void PromptForPin(ParentalPromptCompletedCallback pe)
        {
            PromptForPin(pe, "");
        }

        private void PromptForPin(ParentalPromptCompletedCallback pe, string prompt)
        {
            gettingNewPIN = false;
            if (!Microsoft.MediaCenter.UI.Application.IsApplicationThread)
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(Application.CurrentInstance.OpenSecurityPage, prompt);
            else
                Application.CurrentInstance.OpenSecurityPage(prompt);
        }


        public void CustomPINEntered(string aPIN)
        {
            //Logger.ReportInfo("Custom PIN entered: " + aPIN);
            if (gettingNewPIN)
            {
                gettingNewPIN = false;
                Config.Instance.ParentalPIN = aPIN;
                MediaCenterEnvironment env = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
                env.Dialog(Application.CurrentInstance.StringData("PINChangedDial"), Application.CurrentInstance.StringData("PINChangedCapDial"), DialogButtons.Ok, 60, true);
                Application.CurrentInstance.Back(); //clear PIN entry screen
            }
            else
            {
                pinCallback(aPIN == customPIN);
                if (pinCallback != UnlockPinEntered && aPIN == customPIN && aPIN == Config.Instance.ParentalPIN && Config.Instance.UnlockOnPinEntry)
                {
                    //also unlock the library
                    unlockLibrary();
                    Application.CurrentInstance.Information.AddInformationString(string.Format(Application.CurrentInstance.StringData("LibraryUnLockedProf"), this.unlockPeriod.ToString())); //and display a message
                }
            }

        }

        public void Relock()
        {
            //MediaCenterEnvironment env = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
            Logger.ReportInfo("Library Re-Locked");
            _relockTimer.Stop(); //stop our re-lock timer
            Config.Instance.ParentalControlUnlocked = false;
            if (Config.Instance.HideParentalDisAllowed)
            {
                Application.CurrentInstance.BackToRoot(); //back up to home screen
                Application.CurrentInstance.RootFolderModel.RefreshChildren();
            }
            Application.CurrentInstance.Information.AddInformationString(Application.CurrentInstance.StringData("LibraryReLockedProf")); //and display a message
            //env.Dialog("Library Has Been Re-Locked for Parental Control.", "Unlock Time Expired", DialogButtons.Ok, 60, true);
        }

    }
}
