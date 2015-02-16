using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library
{
    public class UserConfig : BaseModelItem
    {
        private UserConfiguration _data;

        public UserConfig(UserConfiguration data)
        {
            this._data = data;
        }

        private List<BaseItemDto> _availableLibraryFolders;
        public List<BaseItemDto> AvailableLibraryFolders
        {
            get { return _availableLibraryFolders ?? (_availableLibraryFolders = RetrieveLibraryFolders()); }
            set { _availableLibraryFolders = value; FirePropertyChanged("AvailableLibraryFolders"); }
        }

        private List<BaseItemDto> RetrieveLibraryFolders()
        {
            return Kernel.ApiClient.GetItems(new ItemQuery
                                             {
                                                 UserId = Kernel.CurrentUser.ApiId,
                                                 SortBy = new []{"SortName"}

                                             }).Items.ToList();
        }

        private List<BaseItemDto> _availableViews;
        protected List<BaseItemDto> AvailableViews
        {
            get { return _availableViews ?? (_availableViews = RetrieveViews()); }
        }

        private List<BaseItemDto> RetrieveViews()
        {
            return Kernel.ApiClient.GetUserViews(Kernel.CurrentUser.ApiId).Items.ToList();
        }

        private List<FolderConfigItem> _availableChannels;
        public List<FolderConfigItem> AvailableChannels
        {
            get { return _availableChannels ?? (_availableChannels = RetrieveChannels().Select(c => new FolderConfigItem(c.Id, c.Name)).ToList()); }
        }

        private IEnumerable<BaseItemDto> RetrieveChannels()
        {
            return Kernel.ApiClient.GetChannels(Kernel.CurrentUser.ApiId).Items;
        }

        public List<FolderConfigItem> OrderedViews
        {
            get { return _data.OrderedViews.Select(v => new FolderConfigItem(v, AvailableViews.First(i => i.Id == v).Name)).ToList(); }

            set { _data.OrderedViews = value.Select(v => v.Id).ToArray(); }
        }

        public void MoveViewOrderUp(int ndx)
        {
            if (ndx <= 0) return;

            var temp = _data.OrderedViews[ndx - 1];
            _data.OrderedViews[ndx - 1] = _data.OrderedViews[ndx];
            _data.OrderedViews[ndx] = temp;
            FirePropertyChanged("OrderedViews");
        }

        public bool FolderIsGrouped(string id)
        {
            return !_data.ExcludeFoldersFromGrouping.Contains(id);
        }

        public void SetFolderIsGrouped(string id, bool group)
        {
            _data.ExcludeFoldersFromGrouping = group ? 
                _data.ExcludeFoldersFromGrouping.Where(i => i != id).ToArray() : 
                _data.ExcludeFoldersFromGrouping.Concat(new [] {id}).Distinct().ToArray();
        }

        public bool ChannelAtTop(string id)
        {
            return _data.DisplayChannelsWithinViews.Contains(id);
        }

        public void SetChannelAtTop(string id, bool top)
        {
            _data.DisplayChannelsWithinViews = top ?
                _data.DisplayChannelsWithinViews.Concat(new[] {id}).Distinct().ToArray() :
                _data.DisplayChannelsWithinViews.Where(i => i != id).ToArray();
        }

        public void Save()
        {
            Kernel.ApiClient.UpdateUserConfiguration(Kernel.CurrentUser.ApiId, _data);
            _availableViews = null;
            FireAllPropertiesChanged();
        }
    }

    public class FolderConfigItem : BaseModelItem
    {
        private string _id;
        private string _name;

        public FolderConfigItem(string id, string name)
        {
            _id = id;
            _name = name;
        }

        public string Id
        {
            get { return _id; }
            set { _id = value; FirePropertyChanged("Id"); }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; FirePropertyChanged("Name"); }
        }
    }
}
