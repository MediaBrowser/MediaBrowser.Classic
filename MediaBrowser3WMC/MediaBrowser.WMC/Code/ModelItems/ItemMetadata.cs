﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;
using Microsoft.MediaCenter.UI;
using System.Threading;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Metadata;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library {
    partial class Item {

        public static Item BlankItem {
            get { return blank; }
        }

        public string FirstAired {
            get { return baseItem.FirstAired ?? ""; }
        }

        public bool HasEndDate { get { return baseItem.HasEndDate; } }
        public string EndDate { get { return baseItem.EndDate.ToLocalTime().ToString("ddd d MMM, yyyy"); } }

        public string Status {
            get {
                string status = null;
                var series = baseItem as Series;
                if (series != null) {
                    status = series.Status;
                }
                return status ?? "";
            }
        }

        public string AirDay {
            get {
                string day = null;
                var series = baseItem as Series;
                if (series != null) {
                    day = series.AirDay;
                }
                return day ?? "";
            }
        }

        public string AirTime {
            get {
                string time = null;
                var series = baseItem as Series;
                if (series != null) {
                    time = series.AirTime;
                }
                return time ?? "";
            }
        }

        public bool IsHD {
            get {
                return ((this.MediaInfo.Width >= 1280) || (this.MediaInfo.Height >= 720));
            }
        }

        public int HDType {
            get {
                if ((this.MediaInfo.Width >= 1900) || (this.MediaInfo.Height >= 1050))
                    return 1080;
                else if (IsHD)
                    return 720;
                else
                    return 0;
            }
        }

        public bool HasMediaInfo {
            get {
                return MediaInfo != MediaInfoData.Empty;
            }
        }

        public MediaInfoData MediaInfo { 
            get {
                var video = baseItem as Video;
                if (video != null && video.MediaInfo != null) {
                    return video.MediaInfo;
                }
                return MediaInfoData.Empty;
            }  
        }

        public string DirectorString {
            get { return string.Join(", ", this.Directors.ToArray()); }
        }

        public string WritersString {
            get { return string.Join(", ", this.Writers.ToArray()); }
        }

        public List<string> Directors {
            get {
                List<string> directors = null;
                var show = baseItem as Show;
                if (show != null && show.Directors != null) {
                    directors = show.Directors;
                }
                return directors ?? new List<string>();
            }
        }

        public List<string> Writers {
            get {
                List<string> writers = null; 
                var episode = baseItem as Episode;
                if (episode != null) {
                    writers = episode.Writers;
                }
                return writers ?? new List<string>();
            }
        }

        public List<string> Genres {
            get {
                var show = baseItem as IShow;
                if (show != null && show.Genres != null) {
                    return show.Genres;
                }
                return new List<string>();
            }
        }
        public string TrailerPath {
            get {
                var movie = baseItem as Movie;
                if (movie != null && movie.TrailerPath != null) {
                    return movie.TrailerPath;
                }
                return "";
            }
        }

        public void ResetRunTime()
        {
            this.runtime = null;
            this.runtimestr = null;
            UIFirePropertiesChange("RunningTime", "RunningTimeString", "EndTime", "EndTimeString");
        }

        public long RunTimeTicks
        {
            get { return baseItem.RuntimeTicks; }
        }

        protected int? runtime;
        public int RunningTime
        {
            get
            {
                if (runtime == null)
                {
                    runtime = 0;
                    var episode = baseItem as Episode;
                    if (episode != null)
                    {
                        runtime = episode.RunTime > 0 ? episode.RunTime : this.MediaInfo.RunTime;
                    }
                    else
                    {
                        var show = baseItem as IShow;
                        if (show != null)
                        {
                            runtime =  show.RunningTime == null ? this.MediaInfo.RunTime : show.RunningTime.Value;
                        }
                        else
                        {
                            var folder = baseItem as Folder;
                            if (folder != null)
                            {
                                //this might take a bit...
                                Async.Queue(Async.ThreadPoolName.RuntimeCalc, () =>
                                {
                                    runtime = folder.RunTime;
                                    UIFirePropertiesChange("RunningTime", "RunningTimeString", "EndTime", "EndTimeString");
                                });
                            }
                        }
                    }

                    if (PartCount > 1)
                    {
                        runtime += AdditionalParts.Sum(p => p.RunningTime);
                    }
                }
                return runtime == null ? 0 : runtime.Value;
            }
        }

        protected string runtimestr;
        public string RunningTimeString {
            get {
                if (runtimestr == null)
                {
                    var song = baseItem as Song;
                    if (song != null)
                    {
                        var ts = TimeSpan.FromTicks(song.RuntimeTicks);
                        runtimestr = string.Format("{0}:{1}", ts.Minutes, ts.Seconds.ToString("00"));
                    }
                    else
                    {
                        runtimestr = RunningTime > 0 ? RunningTime + " " + Kernel.Instance.StringData.GetString("MinutesStr") : null;
                    }
                }
                return runtimestr ?? "";
            }
        }

        public string EndTimeString
        {
            get
            {
                var endtime = "";
                if (this.RunningTime > 0)
                {
                    endtime = Localization.LocalizedStrings.Instance.GetString("EndsStr") + " " + this.EndTime.ToShortTimeString();
                }
                return endtime;
            }
        }

        public DateTime EndTime
        {
            get
            {
                DateTime endtime = DateTime.MinValue;
                if (this.RunningTime > 0)
                {
                    endtime = (DateTime.Now + TimeSpan.FromMinutes(this.RunningTime));
                }
                return endtime;
            }
        }

        public int ProductionYear {
            get { return baseItem.ProductionYear ?? -1; }
        }

        public string ProductionYearString {
            get { return ProductionYear <= 1600 ? "" : ProductionYear.ToString(); }
        }

        public float ImdbRating {
            get { return baseItem.ImdbRating ?? -1; }
        }

        public bool IsExternalDisc {get { return baseItem.IsExternalDisc; }}
        public bool IsOffline {get { return baseItem.IsOffline; }}

        public string ImdbRatingString {
            get { return (ImdbRating).ToString("0.##"); }
        }

        public bool HasCriticRating { get { return baseItem.CriticRating != null; } }
        public float CriticRating
        {
            get { return baseItem.CriticRating ?? 0; }
        }

        public string CriticRatingString {get { return HasCriticRating ? "(" + CriticRating.ToString("##0") + "%)" : ""; }}

        public string CriticRatingSummary { get { return BaseItem.CriticRatingSummary ?? ""; } }

        public bool HasMetaScore { get { return baseItem.MetaScore != null; } }
        public float MetaScore
        {
            get { return baseItem.MetaScore ?? 0; }
        }

        /// <summary>
        /// DEPRICATED - Use Item.OfficialRating instead
        /// </summary>
        public string MpaaRating {
            get {
                return baseItem.OfficialRating;
                //IShow show = baseItem as IShow;
                //return show != null ? show.MpaaRating ?? "" : "";
            }
        }

        public string OfficialRating
        {
            get { return baseItem.OfficialRating; }
            set
            {
                if (baseItem.OfficialRating != value)
                {
                    baseItem.OfficialRating = value;
                    UIFirePropertyChange("OfficialRating");
                }
            }
        }

        public string AspectRatioString
        {
            get
            {
                IShow show = baseItem as IShow;
                if (show != null)
                {
                    if (!string.IsNullOrEmpty(show.AspectRatio))
                    {
                        return show.AspectRatio;
                    }
                }
                //calculated aspect ratios on ripped media are garbage
                return (baseItem is Video && !(baseItem as Video).ContainsRippedMedia) ? this.MediaInfo.AspectRatioString : "";
            }
        }

        public bool HasChapterInfo { get { return Chapters.Count > 0; } }
        private List<ChapterItem> _chapters;
        public List<ChapterItem> Chapters
        {
            get { return _chapters ?? (_chapters = baseItem.Chapters != null ? baseItem.Chapters.Select(c => ChapterItem.Create(c, this)).Concat(HasAdditionalParts ? AdditionalParts.SelectMany(p => p.BaseItem.Chapters.Select(i => ChapterItem.Create(i, p))) : new List<ChapterItem>()).ToList() : new List<ChapterItem>()); }
        }

        private List<ActorItemWrapper> _actors;

        public List<ActorItemWrapper> Actors
        {
            get {
                if (_actors == null)
                {
                    _actors = new List<ActorItemWrapper>(); // nulls will cause mcml to blow
                    Async.Queue(Async.ThreadPoolName.ActorLoad, () =>
                                                  {
                                                      var actors = new List<Actor>();
                                                      var show = baseItem as IShow;
                                                      if (show != null)
                                                      {

                                                          var episode = show as Episode;
                                                          if (episode != null)
                                                          {

                                                              var series = episode.Series;
                                                              var season = episode.Season;

                                                              if (series != null && series.Actors != null)
                                                              {
                                                                  actors.AddRange(series.Actors);
                                                              }

                                                              if (season != null && season.Actors != null)
                                                              {
                                                                  actors.AddRange(season.Actors);
                                                              }
                                                          }

                                                          if (show.Actors != null)
                                                          {
                                                              actors.AddRange(show.Actors);
                                                          }

                                                          actors = actors.
                                                              Where(actor => actor != null && actor.Name != null).
                                                              Distinct(actor => actor.Name.ToLower().Trim()).
                                                              Where(actor => actor.Name != null && actor.Name.Trim().Length > 0)
                                                                         .ToList();

                                                      }

                                                      _actors = actors
                                                          .Select(actor => new ActorItemWrapper(actor, this.PhysicalParent))
                                                          .ToList();

                                                      UIFirePropertyChange("Actors");

                                                  });

                }
                return _actors;
            }
        }

        public bool HasDataForDetailPage {
            get {

                var movie = baseItem as Movie;
                if (movie == null) return false;

                int score = 0;
                if (Actors.Count > 0)
                    score += 2;
                if (movie.Studios != null && movie.Studios.Count > 0)
                    score += 2;
                if (Genres.Count > 0)
                    score += 2;
                if (Directors.Count > 0)
                    score += 2;
                if (Writers.Count > 0)
                    score += 2;
                if (movie.Overview != null)
                    score += 2;
                if (movie.MpaaRating != null)
                    score += 1;
                if (movie.ImdbRating != null)
                    score += 1;
                if (movie.ProductionYear != null)
                    score += 1;
                if (movie.RunningTime != null)
                    score += 1;
                return score > 5;
            }
        }

        public List<string> Studios
        {
            get
            {
                List<string> studios = null;
                var show = baseItem as IShow;
                if (show != null) 
                {
                    if (show.Studios != null)
                        studios = show.Studios;
                    else
                        if (baseItem is Episode)
                            studios = (baseItem as Episode).OurSeries.Studios;
                        else
                            if (baseItem is Season)
                                studios = (baseItem as Season).OurSeries.Studios;
                }

                return studios ?? new List<string>();
            }
        }

        readonly List<StudioItemWrapper>  _studioItems = null;

        public List<StudioItemWrapper> StudioItems
        {
            get
            {
                if (_studioItems != null) {
                    return _studioItems;
                }

                var items = Studios.Select(Studio.GetStudio).ToList();
                return items.Select(s => new StudioItemWrapper(s, this.PhysicalParent)).ToList();
            }
        }

        public List<DirectorItemWrapper> DirectorItems {
            get {
                var items = this.Directors
                    .Select(s => new DirectorItemWrapper(s, this.PhysicalParent))
                    .OrderBy(x => x.Director);
                return new List<DirectorItemWrapper>(items);
            }
        }


        /// <summary>
        /// The metadata overview if there is one.
        /// </summary>
        public virtual string Overview {
            get {
                string overview = this.BaseItem.Overview;
                if (!string.IsNullOrEmpty(overview)) {
                    overview = overview.Replace("\r\n", "\n").Replace("\n\n", "\n");
                } else {
                    overview = "";
                }
                return overview;
            }
        }

        public string ShortDescription
        {
            get
            {
                return baseItem.ShortDescription;
            }
            set
            {
                if (baseItem.ShortDescription != value)
                {
                    baseItem.ShortDescription = value;
                    UIFirePropertyChange("ShortDescription");
                }
            }
        }

        public string TagLine
        {
            get
            {
                return baseItem.TagLine;
            }
            set
            {
                if (baseItem.TagLine != value)
                {
                    baseItem.TagLine = value;
                    UIFirePropertyChange("TagLine");
                }
            }
        }

        public bool HasSubTitle
        {
            get {
                return !string.IsNullOrEmpty(baseItem.SubTitle);
            }
        }
        public string SubTitle {
            get { return baseItem.SubTitle; }
        }

        public string NameDateString
        {
            get 
            { 
                string pys = string.Empty;
                if (ProductionYear > 1850)
                    pys = string.Format(" ({0})", ProductionYear.ToString());
                return baseItem.Name + pys;
            }
        }


        private void MetadataChanged(object sender, MetadataChangedEventArgs args) {
            //Something on the server changed - reload us but no UI message
            ReLoadFromServer();

        }
    }
}
