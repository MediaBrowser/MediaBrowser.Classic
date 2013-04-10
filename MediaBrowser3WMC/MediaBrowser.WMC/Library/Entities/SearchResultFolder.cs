using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Entities
{
    /// <summary>
    /// This exists just so that we can have separate display prefs
    /// </summary>
    public class SearchResultFolder : IndexFolder
    {
        public SearchResultFolder()
            : base()
        { }

        public SearchResultFolder(List<BaseItem> children)
            : base(children)
        { }
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

            this.DisplayPreferences = new Model.Entities.DisplayPreferences { ViewType = dp.ViewType.Chosen.ToString(), SortBy = dp.SortOrder };
        }

        protected DisplayPreferences LoadDefaultDisplayPreferences()
        {
            var dp = new DisplayPreferences(DisplayPreferencesId, this);
            dp.LoadDefaults();
            return dp;
        }
    }
}
