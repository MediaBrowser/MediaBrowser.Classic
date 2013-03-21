using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            get {
                string firstAired = null;
                var episode = baseItem as Episode;
                if (episode != null) {
                    firstAired = episode.FirstAired;
                }
                return firstAired ?? "";
            }
        }

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
            FirePropertiesChanged("RunningTime", "RunningTimeString", "EndTime", "EndTimeString");
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
                            runtime = show.RunningTime == null ? this.MediaInfo.RunTime : show.RunningTime.Value;
                        }
                        else
                        {
                            var folder = baseItem as Folder;
                            if (folder != null)
                            {
                                //this might take a bit...
                                Async.Queue("runtime calc", () =>
                                {
                                    runtime = folder.RunTime;
                                    FirePropertiesChanged("RunningTime", "RunningTimeString", "EndTime", "EndTimeString");
                                });
                            }
                        }
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
                    var episode = baseItem as Episode;
                    if (episode != null)
                    {
                        runtimestr = episode.RunTime > 0 ? episode.RunTime.ToString() + " " + Kernel.Instance.StringData.GetString("MinutesStr") : this.MediaInfo.RuntimeString;
                    }
                    else
                    {
                        var show = baseItem as IShow;
                        if (show != null)
                        {
                            runtimestr = show.RunningTime == null ? this.MediaInfo.RuntimeString : show.RunningTime.ToString() + " " + Kernel.Instance.StringData.GetString("MinutesStr");
                        }
                        else
                        {
                            var folder = baseItem as Folder;
                            if (folder != null)
                            {
                                //this might take a bit...
                                Async.Queue("runningtimestr calc", () =>
                                {
                                    int totalMinutes = folder.RunTime;
                                    if (totalMinutes > 0)
                                    {
                                        if (totalMinutes <= 60)
                                        {
                                            runtimestr = totalMinutes + " " + Kernel.Instance.StringData.GetString("MinutesStr");
                                        }
                                        else
                                        {
                                            TimeSpan ts = TimeSpan.FromMinutes(totalMinutes);
                                            runtimestr = string.Format("{0} {2} {1} {3}", (int)ts.TotalHours, ts.Minutes, Kernel.Instance.StringData.GetString("HoursStr"), Kernel.Instance.StringData.GetString("MinutesStr"));
                                        }
                                    }
                                    else
                                    {
                                        runtimestr = "";
                                    }

                                    FirePropertiesChanged("RunningTime", "RunningTimeString", "EndTime", "EndTimeString");
                                });
                            }
                        }
                    }
                }
                return runtimestr == null ? "" : runtimestr;
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
            get {
                int productionYear = -1;
                var show = baseItem as Show;
                if (show != null) {
                    productionYear = show.ProductionYear ?? -1;
                }
                return productionYear;
            }
        }

        public string ProductionYearString {
            get { return ProductionYear == -1 ? "" : ProductionYear.ToString(); }
        }

        public float ImdbRating {
            get {
                float rating = -1;
                var show = baseItem as IShow;
                if (show != null) {
                    rating = show.ImdbRating ?? -1;
                    if (rating < 0.1) rating = -1;
                }
                return rating;
            }
        }

        public string ImdbRatingString {
            get { return (ImdbRating).ToString("0.##"); }
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
                    FirePropertyChanged("OfficialRating");
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
                    if (show.AspectRatio != null && show.AspectRatio != "")
                    {
                        return show.AspectRatio;
                    }
                }
                //calculated aspect ratios on ripped media are garbage
                return (baseItem is Video && !(baseItem as Video).ContainsRippedMedia) ? this.MediaInfo.AspectRatioString : "";
            }
        }

        private List<ActorItemWrapper> _actors;

        public List<ActorItemWrapper> Actors
        {
            get {
                if (_actors == null)
                {

                    List<Actor> actors = new List<Actor>();

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

                        if (actors.Count > 0)
                        {

                            Async.Queue("Actor Loader", () =>
                            {
                                foreach (var actor in actors.Distinct())
                                {
                                    if (actor.Person.RefreshMetadata(MetadataRefreshOptions.FastOnly))
                                    {
                                        Kernel.Instance.ItemRepository.SaveItem(actor.Person);
                                    }
                                }

                                foreach (var actor in actors.Distinct())
                                {
                                    if (actor.Person.RefreshMetadata())
                                    {
                                        Kernel.Instance.ItemRepository.SaveItem(actor.Person);
                                    }
                                }
                            });
                        }

                    }

                    _actors = actors
                        .Select(actor => new ActorItemWrapper(actor, this.PhysicalParent))
                        .ToList();
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

        List<StudioItemWrapper>  studioItems = null;

        public List<StudioItemWrapper> StudioItems
        {
            get
            {
                if (studioItems != null) {
                    return studioItems;
                }

                var studioStrs = this.Studios;
                List<Studio> items = new List<Studio>();

                foreach (string q in studioStrs)
                    items.Add(Studio.GetStudio(q));

                Async.Queue("Studio Item Loader", () =>
                {
                    foreach (Studio studio in items)
                    {
                        if (studio.PrimaryImage == null)
                        {
                            var image = new MediaBrowser.Code.ModelItems.AsyncImageLoader(
                                () => studio.PrimaryImage,
                                DefaultImage,
                                () => this.FirePropertyChanged("PrimaryImage"));
                        }
                    }
                });

                var siw = items
                    .Select(s => new StudioItemWrapper(s, this.PhysicalParent));
                    // http://community.mediabrowser.tv/permalinks/1356/studio-icons-in-name-order
                    // .OrderBy(x => x.Studio.Name);


                if (items.Count > 0) {

                    Async.Queue("Studio Item Loader", () =>
                    {
                        foreach (var studio in items.Distinct()) {
                            if (studio.RefreshMetadata(MetadataRefreshOptions.FastOnly)) {
                                Kernel.Instance.ItemRepository.SaveItem(studio);
                            }
                        }

                        foreach (var studio in items.Distinct()) {
                            if (studio.RefreshMetadata()) {
                                Kernel.Instance.ItemRepository.SaveItem(studio);
                            }
                        }
                    });
                }

                studioItems =  new List<StudioItemWrapper>(siw);
                return studioItems;
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
                    FirePropertyChanged("ShortDescription");
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
                    FirePropertyChanged("TagLine");
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
            if (!Microsoft.MediaCenter.UI.Application.IsApplicationThread) {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke( _ => MetadataChanged(sender, args));
                return;
            }

            FirePropertyChanged("Name");
            FirePropertyChanged("Overview");
            FirePropertyChanged("PrimaryImage"); 
            FirePropertyChanged("PrimaryImageSmall"); 
            FirePropertyChanged("PrimaryImage"); 
            FirePropertyChanged("PreferredImageSmall"); 
        }
    }
}
