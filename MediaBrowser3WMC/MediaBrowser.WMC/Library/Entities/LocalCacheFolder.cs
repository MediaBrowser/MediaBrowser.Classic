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
        public LocalCacheFolder() : base()
        {
        }

        public LocalCacheFolder(List<BaseItem> list) : base(list)
        {
        }

        protected override List<BaseItem> GetCachedChildren()
        {
            List<BaseItem> items = null;
            //using (new MediaBrowser.Util.Profiler(this.Name + " child retrieval"))
            {
                //Logger.ReportInfo("Getting Children for: "+this.Name);
                var children = Kernel.Instance.LocalRepo.RetrieveChildren(Id);
                items = children != null ? children.ToList() : GetRemoteChildren();
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
            return Kernel.Instance.ItemRepository.RetrieveSpecificItems(Kernel.Instance.LocalRepo.RetrieveChildList(Id).ToArray()).ToList();
        }

        /// <summary>
        /// We save display prefs in our local cache
        /// </summary>
        /// <param name="prefs"></param>
        public override void SaveDisplayPrefs(DisplayPreferences prefs)
        {
            Kernel.Instance.LocalRepo.SaveDisplayPreferences(prefs);
        }

        public override void LoadDisplayPreferences()
        {
            Logger.ReportVerbose("Loading display prefs from local repo for " + this.Name + "/"+DisplayMediaType);

            var id = (this.DisplayMediaType + Kernel.CurrentUser.Name).GetMD5();

            var dp = new DisplayPreferences(DisplayPreferencesId, this);
            dp = Kernel.Instance.LocalRepo.RetrieveDisplayPreferences(dp);
            if (dp == null)
            {
                LoadDefaultDisplayPreferences(ref id, ref dp);
            }

            this.DisplayPreferences = new Model.Entities.DisplayPreferences {ViewType = dp.ViewType.Chosen.ToString(), SortBy = dp.SortOrder};
        }

        protected void LoadDefaultDisplayPreferences(ref Guid id, ref DisplayPreferences dp)
        {
            dp = new DisplayPreferences(DisplayPreferencesId, this);
            dp.LoadDefaults();
        }
    }
}
