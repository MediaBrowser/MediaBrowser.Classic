using System;
using System.Linq;
using MediaBrowser.Library;
using MediaBrowser.Library.Entities;
using Microsoft.MediaCenter.UI;

namespace MediaBrowser.Library
{
    public class UpcomingTvFolderModel : FolderModel
    {
        public UpcomingTvFolderModel()
        {
            ViewBy.ChosenChanged += ViewByChanged;
    
        }

        private UpcomingTvFolder _ourFolder;

        public static bool IsOne(BaseItem item)
        {
            return item is UpcomingTvFolder;
        }

        protected UpcomingTvFolder OurFolder
        {
            get { return _ourFolder ?? (_ourFolder = Folder as UpcomingTvFolder); }
        }

        private Choice _viewBy = new Choice { Options = new[] { "Date", "Show" } };
        public Choice ViewBy {get { return _viewBy; }}

        protected void ViewByChanged(object sender, EventArgs e)
        {
            OurFolder.ViewBy = ViewBy.ChosenIndex;
        }

        protected override void FireChildrenChangedEvents()
        {
            // cascade our children loading
            foreach (var folderChild in Children.OfType<FolderModel>())
            {
                folderChild.NavigatingInto();
            }

            base.FireChildrenChangedEvents();
        }
    }
}
