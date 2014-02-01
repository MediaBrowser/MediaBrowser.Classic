using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Model.Updates;

namespace MediaBrowser.Library
{
    public class PluginItemCollection : BaseModelItem
    {
        public List<PluginItem> Items { get; private set; }
        private int _selectedItemIndex = -1;

        public string Name { get; set; }
        public bool UpdatesAvailable { get { return Items != null && Items.Any(i => i.UpdateAvailable); } }

        public int SelectedItemIndex
        {
            get { return _selectedItemIndex; }
            set
            {
                if (_selectedItemIndex != value)
                {
                    _selectedItemIndex = value;
                    FirePropertiesChanged("SelectedItemIndex", "SelectedItem");
                }
            }
        }

        public PluginItem SelectedItem { get { return Items.Any() ? Items[_selectedItemIndex] : new PluginItem(new PackageInfo {name = "Unknown"}); } }

        public PluginItemCollection()
        {
        }

        public void ResetUpdatesAvailable()
        {
            FirePropertyChanged("UpdatesAvailable");
        }

        public void Remove(PluginItem plugin)
        {
            if (Items.Remove(plugin)) Items = new List<PluginItem>(Items);
            FirePropertyChanged("Items");
        }

        public PluginItemCollection(IEnumerable<PackageInfo> plugins)
        {
            Items = plugins.Select(p => new PluginItem(p)).ToList();
            FirePropertyChanged("Items");
        }

        public PluginItemCollection(IEnumerable<PluginItem> plugins)
        {
            Items = plugins.ToList();
            FirePropertyChanged("Items");
        }

        private List<PluginItemCollection> _groupedItems; 
        public List<PluginItemCollection> GroupedItems
        {
            get { return _groupedItems ?? (_groupedItems = GetGroupedItems()); }
        }

        protected List<PluginItemCollection> GetGroupedItems()
        {
            return Items.GroupBy(i => i.Category).OrderByDescending(g => g.Key).Select(g => new PluginItemCollection(g.OrderBy(i => i.Name)) {Name = g.Key}).ToList();
        }

    }
}
