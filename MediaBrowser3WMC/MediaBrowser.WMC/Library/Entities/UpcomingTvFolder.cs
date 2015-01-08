using System.Collections.Generic;
using System.Linq;
using MediaBrowser.ApiInteraction;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class UpcomingTvFolder : Folder
    {
        private int _viewBy;

        public override bool HasMedia
        {
            get
            {
                return true;
            }
        }

        protected override bool HideEmptyFolders
        {
            get
            {
                return false;
            }
        }

        public override Folder QuickList
        {
            get { return new IndexFolder(OurChildren.OrderBy(c => c.PremierDate).Take(Config.Instance.RecentItemCount).ToList()); }
        }

        public override string DisplayPreferencesId
        {
            get
            {
                return ("UpcomingTv" + Kernel.CurrentUser.Name).GetMD5().ToString();
            }
            set
            {
                base.DisplayPreferencesId = value;
            }
        }

        public int ViewBy
        {
            get { return _viewBy; }
            set { _viewBy = value; ViewByChanged(); }
        }

        public void ViewByChanged()
        {
            OnChildrenChanged(new ChildrenChangedEventArgs());
        }
        protected List<BaseItem> OurChildren { get; set; } 

        public List<BaseItem> ByDate { get { return OurChildren.GroupBy(c => c.PremierDate).Select(g => new UtvDateFolder(g.Key, g)).OrderBy(d => d.PremierDate).Cast<BaseItem>().ToList(); } }
        public List<BaseItem> ByShow { get { return OurChildren.OfType<Episode>().GroupBy(c => c.Series.Id).Select(g => new UtvSeriesFolder(g.First().Series, g)).Cast<BaseItem>().ToList(); } } 

        protected override List<BaseItem> GetCachedChildren()
        {
            var parms = new QueryStringDictionary { { "userId", Kernel.CurrentUser.ApiId }, {"Limit", 200} };
            parms.Add("Fields", MB3ApiRepository.StandardFields.Select(f => f.ToString()));
            var url = Kernel.ApiClient.GetApiUrl("Shows/Upcoming", parms);

            using (var stream = Kernel.ApiClient.GetSerializedStream(url))
            {
                var result = Kernel.ApiClient.DeserializeFromStream<ItemsResult>(stream);
                OurChildren = result.Items.Select(i => Kernel.Instance.MB3ApiRepository.GetItem(i, i.Type)).Where(i => i != null).ToList();
                return OurChildren;
            }
        }

        public override string PrimaryImagePath
        {
            get
            {
                return base.PrimaryImagePath ?? "resx://MediaBrowser/MediaBrowser.Resources/plaintv";
            }
            set
            {
                base.PrimaryImagePath = value;
            }
        }

        protected override List<BaseItem> ActualChildren
        {
            get
            {
                if (OurChildren == null) GetCachedChildren();
                return ViewBy == 0 ? ByDate : ByShow;
            }
        }

        public override string CustomUI
        {
            get
            {
                return "resx://MediaBrowser/MediaBrowser.Resources/UpcomingTvView";
            }
            set
            {
                base.CustomUI = value;
            }
        }
    }
}
