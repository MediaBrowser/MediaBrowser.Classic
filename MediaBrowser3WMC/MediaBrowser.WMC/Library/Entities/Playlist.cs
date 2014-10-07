using System.Collections.Generic;
using MediaBrowser.Library.Localization;

namespace MediaBrowser.Library.Entities
{
    public class Playlist : Folder
    {
        public override Dictionary<string, IComparer<BaseItem>> SortOrderOptions
        {
            get
            {
                return new Dictionary<string, IComparer<BaseItem>>
                                       {
                                           {LocalizedStrings.Instance.GetString("NoneDispPref"), new BaseItemComparer(SortOrder.None)}
                                       };
            }
            set
            {
                base.SortOrderOptions = value;
            }
        }

        public override void Sort(IComparer<BaseItem> sortFunction)
        {
            return;
        }

        protected override bool CollapseBoxSets
        {
            get
            {
                return false;
            }
        }
    }
}