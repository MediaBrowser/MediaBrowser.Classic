using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities {
    public class Episode : Show, IGroupInIndex {

        [Persist]
        public string EpisodeNumber { get; set; }

        [Persist]
        public string SeasonNumber { get; set; }

        public string SeriesId { get; set; }
        public string SeasonId { get; set; }

        public override bool IsMissing
        {
            get { return LocationType == LocationType.Virtual && PremierDate <= DateTime.UtcNow; }
        }

        public override bool IsFuture
        {
            get { return LocationType == LocationType.Virtual && PremierDate > DateTime.UtcNow; }
        }

        
        public Season Season {
            get { return Parent as Season ?? RetrieveSeason() ?? Season.BlankSeason; }
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
                if (found == null || found.GetType() != typeof(Series))
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
                if (SeasonNumber != null) {
                    longName = Localization.LocalizedStrings.Instance.GetString("Season") + " " + SeasonNumber + " - " + longName;
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
            var sid = SeasonId ?? ApiParentId;
            if (seasonItem == null && !string.IsNullOrEmpty(sid))
            {
                var seasonId = new Guid(sid);
                seasonItem = Kernel.Instance.MB3ApiRepository.RetrieveItem(seasonId) as Season;
            }
            return seasonItem;
        }

        Series seriesItem;
        public Series RetrieveSeries()
        {
            if (seriesItem == null && !string.IsNullOrEmpty(this.SeriesId))
            {
                seriesItem = Kernel.Instance.MB3ApiRepository.RetrieveItem(new Guid(SeriesId)) as Series;
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

        public override string PrimaryImagePath
        {
            get
            {
                return base.PrimaryImagePath ?? Season.ThumbnailImagePath ?? Series.ThumbnailImagePath ?? Series.PrimaryImagePath;
            }
            set
            {
                base.PrimaryImagePath = value;
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
