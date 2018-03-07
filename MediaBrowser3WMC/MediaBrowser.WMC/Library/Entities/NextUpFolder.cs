using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Library.Localization;

namespace MediaBrowser.Library.Entities
{
    class NextUpFolder : ApiCollectionFolder
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

        protected override List<BaseItem> GetCachedChildren()
        {
            return Kernel.Instance.MB3ApiRepository.RetrieveNextUpItems(SearchParentId).ToList();
        }
    }
}
