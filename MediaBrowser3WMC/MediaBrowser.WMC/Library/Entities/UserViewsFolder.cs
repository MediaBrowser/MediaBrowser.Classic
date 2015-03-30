using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Library.Entities
{
    public class UserViewsFolder : AggregateFolder
    {
        protected override void Sort(IComparer<BaseItem> function, bool notifyChange)
        {
            // don't allow re-sort of these views
        }

        protected override List<BaseItem> GetCachedChildren()
        {
            var items = Kernel.Instance.MB3ApiRepository.RetrieveUserViews().ToList();
            //Ensure proper sort and parent
            var num = 0;
            items.ForEach(i => 
            { 
                i.SortName = num++.ToString("0000");
                i.Parent = this;
            });
            return items;
        }
    }
}