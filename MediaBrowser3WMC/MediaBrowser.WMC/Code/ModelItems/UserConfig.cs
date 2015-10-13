using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Threading;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.MediaCenter.AddIn;
using SuperSocket.ClientEngine;

namespace MediaBrowser.Library
{
    public class UserConfig : BaseModelItem
    {
        private UserConfiguration _data;

        public UserConfig(UserConfiguration data)
        {
            this._data = data;
        }

        public bool HasChanged { get; set; }

        private List<FolderConfigItem> _availableLibraryFolders;
        public List<FolderConfigItem> AvailableLibraryFolders
        {
            get { return _availableLibraryFolders ?? (_availableLibraryFolders = RetrieveLibraryFolders()); }
            set { _availableLibraryFolders = value; FirePropertyChanged("AvailableLibraryFolders"); }
        }

        private List<FolderConfigItem> RetrieveLibraryFolders()
        {
            return Kernel.ApiClient.GetItems(new ItemQuery
                                             {
                                                 UserId = Kernel.CurrentUser.ApiId,
                                                 SortBy = new []{"SortName"}

                                             }).Items.Where(dto => dto.CollectionType != "photos").Select(dto => new FolderConfigItem(dto.Id, dto.Name)).ToList();
        }

        private List<BaseItemDto> _availableViews;
        protected List<BaseItemDto> AvailableViews
        {
            get { return _availableViews ?? (_availableViews = RetrieveViews()); }
        }

        private List<BaseItemDto> RetrieveViews()
        {
            return Kernel.ApiClient.GetUserViews(Kernel.CurrentUser.ApiId).Items.Where(dto => dto.CollectionType != "livetv").ToList();
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
            get { return _data.OrderedViews.Select(v => new FolderConfigItem(v, (AvailableViews.FirstOrDefault(i => i.Id == v) ?? new BaseItemDto()).Name)).Where(i => i.Name != null).ToList(); }

            set { _data.OrderedViews = value.Select(v => v.Id).ToArray(); }
        }

        public void MoveViewOrderUp(string id)
        {
            var ndx = _data.OrderedViews.ToList().IndexOf(id);
            if (ndx <= 0) return;

            var temp = _data.OrderedViews[ndx - 1];
            _data.OrderedViews[ndx - 1] = _data.OrderedViews[ndx];
            _data.OrderedViews[ndx] = temp;
            LandingIndex = ndx - 1;
            HasChanged = true;
            FirePropertyChanged("OrderedViews");
        }

        public int LandingIndex { get; set; }

        public bool FolderIsGrouped(string id)
        {
            return _data.GroupedFolders.Contains(id);
        }

        public void SetFolderIsGrouped(string id, bool group)
        {
            _data.GroupedFolders = group ? 
                _data.GroupedFolders.Concat(new [] {id}).Distinct().ToArray() : 
                _data.GroupedFolders.Where(i => i != id).ToArray();

            // also adjust ordered views
            _data.OrderedViews = group ? 
                _data.OrderedViews.Where(i => i != id).ToArray() :
                _data.OrderedViews.Concat(new [] {id}).Distinct().ToArray();

            HasChanged = true;
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

            // also adjust ordered views
            _data.OrderedViews = top ?
                _data.OrderedViews.Concat(new[] { id }).Distinct().ToArray() :
                _data.OrderedViews.Where(i => i != id).ToArray();

            HasChanged = true;
        }

        public bool HasNoChannels { get { return AvailableChannels.Count == 0; }}

        public bool ShowFolders
        {
            get { return _data.DisplayFoldersView; }
            set
            {
                if (value != _data.DisplayFoldersView)
                {
                    _data.DisplayFoldersView = value;
                    FirePropertyChanged("ShowFolders");

                    Async.Queue(Async.ThreadPoolName.UserConfigSave, Save);
                }
            }
        }

        public bool ShowCollections
        {
            get { return _data.DisplayCollectionsView; }
            set
            {
                if (value != _data.DisplayCollectionsView)
                {
                    _data.DisplayCollectionsView = value;
                    FirePropertyChanged("ShowCollections");

                    Async.Queue(Async.ThreadPoolName.UserConfigSave, Save);
                }
            }
        }

        public void Save()
        {
            Kernel.ApiClient.UpdateUserConfiguration(Kernel.CurrentUser.ApiId, _data);
            HasChanged = false;
            _availableViews = null;
            FirePropertiesChanged("OrderedViews");
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
