using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Logging;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities
{
    public class IndexFolder : Series
    {
        public IndexFolder()
            : base()
        { }

        public IndexFolder(List<BaseItem> children)
        {
            //create an index folder with a specified set of children
            this.ActualChildren.Clear();
            this.AddChildren(children);
        }

        public string IndexId { get; set; }

        public override string ApiId
        {
            get
            {
                return IndexId;
            }
        }

        public override void ValidateChildren()
        {
            // If our children haven't been retrieved yet - get them
            if (!this.ActualChildren.Any()) RetrieveChildren();
        }

        public void AddChild(BaseItem child)
        {
            this.ActualChildren.Add(child);
            child.Parent = this;
        }

        public void AddChildren(List<BaseItem> children)
        {
            this.ActualChildren.AddRange(children);
            foreach (var child in children) child.Parent = this;
        }

        public override bool RefreshMetadata(Metadata.MetadataRefreshOptions options)
        {
            return false; //we don't have real metadata...
        }
        /// <summary>
        /// We save display prefs in our local cache
        /// </summary>
        /// <param name="prefs"></param>
        public override void SaveDisplayPrefs(DisplayPreferences prefs)
        {
            Kernel.Instance.LocalRepo.SaveDisplayPreferences(prefs);
        }

        public override string DisplayPreferencesId
        {
            get
            {
                return (DisplayMediaType + Kernel.CurrentUser.Name).GetMD5().ToString();
            }
            set
            {
                base.DisplayPreferencesId = value;
            }
        }

        public override void LoadDisplayPreferences()
        {
            Logger.ReportVerbose("Loading display prefs from local repo for " + this.Name + "/" + DisplayMediaType);

            var dp = new DisplayPreferences(DisplayPreferencesId, this);
            dp = Kernel.Instance.LocalRepo.RetrieveDisplayPreferences(dp) ?? LoadDefaultDisplayPreferences();

            this.DisplayPreferences = new Model.Entities.DisplayPreferences { ViewType = dp.ViewType.Chosen.ToString(), SortBy = dp.SortOrder, 
                ScrollDirection = dp.VerticalScroll.Value ? ScrollDirection.Vertical : ScrollDirection.Horizontal, CustomPrefs = dp.CustomParms,
            PrimaryImageHeight = dp.ThumbConstraint.Value.Height, PrimaryImageWidth = dp.ThumbConstraint.Value.Width, ShowBackdrop = dp.UseBackdrop.Value};
        }

        protected DisplayPreferences LoadDefaultDisplayPreferences()
        {
            var dp = new DisplayPreferences(DisplayPreferencesId, this);
            dp.LoadDefaults();
            return dp;
        }
    }
}
