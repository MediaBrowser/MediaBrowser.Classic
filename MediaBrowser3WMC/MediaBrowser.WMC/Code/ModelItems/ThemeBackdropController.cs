using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Playables;
using Microsoft.MediaCenter;
using Microsoft.MediaCenter.Hosting;
using Microsoft.MediaCenter.UI;
using MediaType = Microsoft.MediaCenter.MediaType;

namespace MediaBrowser.Code.ModelItems
{
    public class ThemeBackdropController : BaseModelItem
    {
        public bool IsPlaying { get; private set; }
        public bool IsPlayingVideo { get; private set; }

        protected bool HasStarted { get; set; }

        protected BaseItem CurrentScope { get; set; }

        private void EnvironmentPropertyChange(IPropertyObject sender, string property)
        {
            var env = sender as MediaCenterEnvironment;
            if (env == null) return;

            var exp = env.MediaExperience;
            if (exp == null)
            {
                Logger.ReportError("Unable to get media experience for theme backdrop playback");
                return;
            }

            var transport = exp.Transport;
            if (transport == null)
            {
                Logger.ReportError("Unable to get transport for theme backdrop playback");
                return;
            }

            Logger.ReportVerbose("Environment property changed - hooking transport");
            transport.PropertyChanged -= HandlePropertyChange;
            transport.PropertyChanged += HandlePropertyChange;
            env.PropertyChanged -= EnvironmentPropertyChange; // we're done - unhook

        }

        private void HandlePropertyChange(IPropertyObject sender, string property)
        {
            var transport = sender as MediaTransport;
            if (transport == null) return;

            var state = transport.PlayState;
            if (!HasStarted && state == PlayState.Playing) HasStarted = true;

            if (HasStarted && (state == PlayState.Finished || state == PlayState.Stopped || state == PlayState.Undefined))
            {
                IsPlaying = IsPlayingVideo = HasStarted = Application.CurrentInstance.ShowNowPlaying = false;
                Logger.ReportVerbose("Theme background stopped - unhooking");
                transport.PropertyChanged -= HandlePropertyChange;
            }
        }

        public bool Play(BaseItem item)
        {
            var coll = new MediaCollection();
            if (item.ThemeVideos != null)
            {
                for (var i = 0; i < Config.Instance.ThemeBackgroundRepeat; i++ )
                {
                    item.ThemeVideos.ForEach(v => coll.AddItem(v.Path));
                }

                IsPlayingVideo = true;
            }

            else if (item.ThemeSongs != null)
            {
                for (var i = 0; i < Config.Instance.ThemeBackgroundRepeat; i++)
                {
                    item.ThemeSongs.ForEach(a => coll.AddItem(a.Path));
                }

                IsPlayingVideo = false;
            }
            else if (Config.Instance.PlayTrailerAsBackground)
            {
                var movie = item as Movie;
                if (movie != null && movie.TrailerFiles.Any())
                {
                    for (var i = 0; i < Config.Instance.ThemeBackgroundRepeat; i++)
                    {
                        foreach (var trailerFile in movie.TrailerFiles)
                        {
                            coll.AddItem(trailerFile);
                        }
                    }

                    IsPlayingVideo = true;
                }
            }

            if (coll.Any())
            {
                //stop anything currently playing
                PlaybackControllerHelper.Stop();

                var mce = Application.MediaCenterEnvironment;
                mce.PropertyChanged += EnvironmentPropertyChange;

                if (mce.PlayMedia(MediaType.MediaCollection, coll, false))
                {
                    IsPlaying = true;
                    Application.CurrentInstance.ShowNowPlaying = IsPlayingVideo;
                    CurrentScope = item;
                    return true;
                }
                else
                {
                    mce.PropertyChanged -= EnvironmentPropertyChange;
                }
            }

            return false;
        }

        public void StopIfOutOfScope(BaseItem item)
        {
            if (!IsPlaying) return;

            //if the passed in item is our current scope or our current scope is a parent then we are still in scope and continue playing
            if (item != CurrentScope && !IsParent(CurrentScope, item))
            {
                PlaybackControllerHelper.Stop();
            }
        }

        protected bool IsParent(BaseItem parent, BaseItem potential)
        {
            if (potential == null || potential == Kernel.Instance.RootFolder || parent == null) return false;

            return parent == potential || IsParent(parent, potential.Parent);
        }
    }
}
