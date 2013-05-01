using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Entities
{
    public class LocalCacheFolder : IndexFolder
    {
        public virtual bool AllowRemoteChildren { get { return true; } }

        public LocalCacheFolder() : base()
        {
        }

        public LocalCacheFolder(List<BaseItem> list) : base(list)
        {
        }

        public override BaseItem ReLoad()
        {
            return Kernel.Instance.ItemRepository.RetrieveItem(Id);
        }

        protected override List<BaseItem> GetCachedChildren()
        {
            List<BaseItem> items = null;
            //using (new MediaBrowser.Util.Profiler(this.Name + " child retrieval"))
            {
                //Logger.ReportInfo("Getting Children for: "+this.Name);
                var children = Kernel.Instance.LocalRepo.RetrieveChildren(Id);
                items = children != null ? children.ToList() : AllowRemoteChildren ? GetRemoteChildren() : new List<BaseItem>();
            }
            return items;
        }

        /// <summary>
        /// Get the list of children locally but then the items themselves from the server
        /// </summary>
        /// <returns></returns>
        protected List<BaseItem> GetRemoteChildren()
        {
            Logging.Logger.ReportVerbose("Getting children remotely for {0}", Name);
            return Kernel.Instance.MB3ApiRepository.RetrieveSpecificItems(Kernel.Instance.LocalRepo.RetrieveChildList(Id).ToArray()).ToList();
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
            Logger.ReportVerbose("Loading display prefs from local repo for " + this.Name + "/"+DisplayMediaType);

            var dp = new DisplayPreferences(DisplayPreferencesId, this);
            dp = Kernel.Instance.LocalRepo.RetrieveDisplayPreferences(dp) ?? LoadDefaultDisplayPreferences();

            this.DisplayPreferences = new Model.Entities.DisplayPreferences {ViewType = dp.ViewType.Chosen.ToString(), SortBy = dp.SortOrder};
        }

        protected DisplayPreferences LoadDefaultDisplayPreferences()
        {
            var dp = new DisplayPreferences(DisplayPreferencesId, this);
            dp.LoadDefaults();
            return dp;
        }
    }
}
