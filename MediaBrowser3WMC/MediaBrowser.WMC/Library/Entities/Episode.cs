using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Entities {
    public class Episode : Show, IGroupInIndex {

        [Persist]
        public string EpisodeNumber { get; set; }

        [Persist]
        public string SeasonNumber { get; set; }

        [Persist]
        public string FirstAired { get; set; }

        public override string SortName {
            get {
                if (EpisodeNumber != null && EpisodeNumber.Length < 3) {
                    return (EpisodeNumber.PadLeft(3, '0') + " - " + Name.ToLower());
                } else {
                    return base.SortName;
                }
            }
            set {
                base.SortName = value;
            }
        }

        public Season Season {
            get {
                if (Parent is Season)
                    return Parent as Season;
                else
                    return this.RetrieveSeason();
            }
        }

        public Series Series {
            get {
                Series found = null;
                if (Parent != null) {
                    if (Parent.GetType() == typeof(Season)) {
                        found = Parent.Parent as Series;
                    } else {
                        found = Parent as Series;
                    }
                }
                if (found == null)
                {
                    //we may have been loaded out of context - retrieve from repo
                    found = RetrieveSeries();
                    //Logging.Logger.ReportVerbose("Episode series loaded from out of context: " + (found != null ? found.Name : ""));
                }
                return found;
            }
        }

        public override Series OurSeries
        {
            get
            {
                return Series ?? Series.BlankSeries;
            }
        }

        public override string LongName {
            get {
                string longName = base.LongName;
                if (Season != null) {
                    longName = Season.Name + " - " + longName;
                }
                if (Series != null) {
                    longName = Series.Name + " - " + longName;
                }
                return longName;
            }
        }

        public override string OfficialRating
        {
            get
            {
                if (Series != null)
                {
                    return Series.OfficialRating;
                }
                else return "None";
            }
        }

        public override bool RefreshMetadata(MediaBrowser.Library.Metadata.MetadataRefreshOptions options)
        {
            bool changed = base.RefreshMetadata(options);
            if (this.Series != null && this.Series.BannerImage != null && (options & MediaBrowser.Library.Metadata.MetadataRefreshOptions.Force) == MediaBrowser.Library.Metadata.MetadataRefreshOptions.Force)
            {
                //we cleared our our series banner image - re-cache it
                var ignore = this.Series.BannerImage.GetLocalImagePath();
            }
            return changed;
        }

        Season seasonItem;
        public Season RetrieveSeason()
        {
            if (seasonItem == null && !string.IsNullOrEmpty(this.Path))
            {
                //derive id of what would be our season - hate this but don't have to store and maintain pointers this way
                string parentPath = System.IO.Path.GetDirectoryName(this.Path);
                Guid seasonId = (typeof(Season).FullName + parentPath.ToLower()).GetMD5();
                seasonItem = Kernel.Instance.ItemRepository.RetrieveItem(seasonId) as Season;
            }
            return seasonItem;
        }

        Series seriesItem;
        public Series RetrieveSeries()
        {
            if (seriesItem == null && !string.IsNullOrEmpty(this.Path))
            {
                string parentPath = System.IO.Path.GetDirectoryName(this.Path);
                string grandparentPath = System.IO.Path.GetDirectoryName(parentPath); //parent of parent is series
                Guid seriesId = (typeof(Series).FullName + (grandparentPath != null ? grandparentPath : parentPath).ToLower()).GetMD5();
                seriesItem = Kernel.Instance.ItemRepository.RetrieveItem(seriesId) as Series;
            }
            return seriesItem;
        }

        public override int RunTime
        {
            get
            {
                if (this.MediaInfo == null || this.MediaInfo.RunTime == 0)
                {
                    return OurSeries.RunningTime == null ? 0 : OurSeries.RunningTime.Value;
                }
                return this.MediaInfo != null ? this.MediaInfo.RunTime : 0;
            }
        }

        public int TrueSequenceNumber
        {
            get
            {
                try
                {
                    return (Convert.ToInt32(SeasonNumber) * 1000) + (Convert.ToInt32(EpisodeNumber));
                }
                catch
                {
                    return 0;
                }
            }
        }

        #region IGroupInIndex Members

        public IContainer MainContainer
        {
            get { return OurSeries; }
        }

        #endregion
    }
}
