using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library;

namespace MediaBrowser.Library
{
    public class MultiPartPlayOption : BaseModelItem
    {
        private string _name;
        private Item _itemToPlay;
        public bool Resume { get; set; }
        public bool? PlayIntros { get; set; }

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    FirePropertyChanged("Name");
                }
            }
        }

        public Item ItemToPlay
        {
            get { return _itemToPlay; }
            set
            {
                if (_itemToPlay != value)
                {
                    _itemToPlay = value;
                    FirePropertyChanged("ItemToPlay");
                }
            }
        }
    }
}