using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MediaBrowser;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Plugins;

namespace MediaBrowser
{
    public class MBPhotoController : BaseModelItem
    {
        private static MBPhotoController _instance;
        public static MBPhotoController Instance { get { return _instance ?? (_instance = new MBPhotoController());}}

        private Microsoft.MediaCenter.UI.Image currentSlideShowImage;
        public Microsoft.MediaCenter.UI.Image CurrentSlideShowImage
        {
            get
            {
                return currentSlideShowImage;
            }
            set
            {
                currentSlideShowImage = value;
                FirePropertyChanged("CurrentSlideShowImage");
            }
        }

        public int SlideShowInterval { get { return Config.Instance.SlideShowInterval * 1000; } }

        public List<Item> CurrentSlideShowItems { get; set; }

        public int CurrentSlideShowNdx { get; set; }

        public Item CurrentSlideShowItem
        {
            get
            {
                return CurrentSlideShowItems[CurrentSlideShowNdx];
            }
        }

        public void NextSlideShowItem()
        {
            CurrentSlideShowNdx++;
            if (CurrentSlideShowNdx >= CurrentSlideShowItems.Count) CurrentSlideShowNdx = 0;
            FirePropertyChanged("CurrentSlideShowItem");
        }

        public void PrevSlideShowItem()
        {
            CurrentSlideShowNdx--;
            if (CurrentSlideShowNdx < 0) CurrentSlideShowNdx = CurrentSlideShowItems.Count - 1;
            FirePropertyChanged("CurrentSlideShowItem");
        }

        private bool audioPlaying = false;
        public void SlideShow(Item item)
        {
            SlideShow(item, false);
        }

        public void SlideShow(Item item, bool random)
        {
            var folder = item as FolderModel ?? item.PhysicalParent;
            if (folder != null)
            {
                var rnd = new Random();
                CurrentSlideShowItems = random ?
                    folder.Folder.RecursiveChildren.OfType<Photo>().OrderBy(r => rnd.Next()).Select(i => ItemFactory.Instance.Create(i)).ToList() :
                    folder.Folder.RecursiveChildren.OfType<Photo>().SkipWhile(i => i.Id != item.Id).Select(i => ItemFactory.Instance.Create(i)).ToList();
                if (CurrentSlideShowItems.Count > 0)
                {
                    Logger.ReportVerbose("***** Playing slide show items: " + CurrentSlideShowItems.Count);
                    string audioFile = GetAudioBackground(item);
                    if (audioFile != null)
                    {
                        audioPlaying = false;
                        Logger.ReportVerbose("***** Playing audio background: " + audioFile);
                        if (!Application.MediaCenterEnvironment.PlayMedia(Microsoft.MediaCenter.MediaType.Audio, audioFile, false))
                        {
                            Logger.ReportWarning("PlayMedia returned false");
                        }
                        else
                        {
                            audioPlaying = true;
                        }
                    }
                    CurrentSlideShowNdx = 0;
                    Application.CurrentInstance.OpenMCMLPage("resx://MediaBrowser/MediaBrowser.Resources/SlideShow#SlideShow", new Dictionary<string, object>() { { "Application", Application.CurrentInstance } });
                }
                else
                {
                    Application.CurrentInstance.Information.AddInformationString("No Images to display...");
                }
            }
        }

        private string GetAudioBackground(Item item)
        {
            string folderPath = item is FolderModel ?
                item.Path :
                Path.GetDirectoryName(item.Path);
            return FindPlayList(folderPath);
        }

        private string FindPlayList(string path)
        {
            if (path == null) return null;
            string playlistPath = Path.Combine(path, "SlideShow.wpl");
            return File.Exists(playlistPath) ? playlistPath : FindPlayList(Path.GetDirectoryName(path));
        }

        public void StopAudio()
        {
            if (audioPlaying)
            {
                try
                {
                    Application.MediaExperience.Transport.PlayRate = 0;
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error stopping audio.", e);
                }
            }
        }

    
    }
}
