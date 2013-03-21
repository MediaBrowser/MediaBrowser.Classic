using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library.Persistance {

    public class MemoizingRepository : IItemRepository {

        IItemRepository repository;
        Dictionary<Guid, IEnumerable<IMetadataProvider>> providers = new Dictionary<Guid, IEnumerable<IMetadataProvider>>();
        Dictionary<Guid, BaseItem> items = new Dictionary<Guid, BaseItem>();
        Dictionary<Guid, IEnumerable<Guid>> children = new Dictionary<Guid, IEnumerable<Guid>>();
        Dictionary<Guid, IEnumerable<BaseItem>> fullChildren = new Dictionary<Guid, IEnumerable<BaseItem>>();        Dictionary<Guid, PlaybackStatus> playState = new Dictionary<Guid, PlaybackStatus>();
        Dictionary<Guid, DisplayPreferences> displayPrefs = new Dictionary<Guid, DisplayPreferences>();

        public MemoizingRepository(IItemRepository repository)
        {
            this.repository = repository;
        }

        private T Memoize<T>(Guid guid, Dictionary<Guid, T> dict, Func<Guid,T> getData) {
            T rval;
            lock (dict) {
                if (dict.TryGetValue(guid, out rval)) {
                    return rval;
                }
            }
            rval = getData(guid);

            lock (dict) {
                dict[guid] = rval;
            }
            return rval;
        }

        public bool BackupDatabase()
        {
            return repository.BackupDatabase();
        }

        public IEnumerable<IMetadataProvider> RetrieveProviders(Guid guid) {
            return Memoize(guid, providers, repository.RetrieveProviders); 
        }

        public void SaveProviders(Guid guid, IEnumerable<IMetadataProvider> providers) {
            repository.SaveProviders(guid, providers);
            lock (this.providers) {
                this.providers[guid] = providers;
            }
        }

        public void SaveItem(BaseItem item) {
            repository.SaveItem(item);
            lock (this.items) {
                this.items[item.Id] = item;
            }
        }

        public BaseItem RetrieveItem(Guid guid) {
            return Memoize(guid, items, repository.RetrieveItem);
        }

        public void SaveChildren(Guid ownerName, IEnumerable<Guid> children) {
            repository.SaveChildren(ownerName, children);
            lock (this.children) {
                this.children[ownerName] = children;
            }
        }

        public IEnumerable<BaseItem> RetrieveChildren(Guid id)
        {
            return Memoize(id, fullChildren, repository.RetrieveChildren);
        }

        public IList<Index> RetrieveIndex(Folder folder, string property, Func<string, BaseItem> constructor)
        {
            return repository.RetrieveIndex(folder, property, constructor);
        }

        public List<BaseItem> RetrieveSubIndex(string childTable, string property, object value)
        {
            return repository.RetrieveSubIndex(childTable, property, value);
        }


        // Do not memoize these calls, as they are shared.
        public PlaybackStatus RetrievePlayState(Guid id) {
            return repository.RetrievePlayState(id); 
        }

        public void SavePlayState(PlaybackStatus playState) {
            repository.SavePlayState(playState);

        }

        public DisplayPreferences RetrieveDisplayPreferences(DisplayPreferences dp) {
            return repository.RetrieveDisplayPreferences(dp);
        }

        public ThumbSize RetrieveThumbSize(Guid id)
        {
            return repository.RetrieveThumbSize(id);
        }

        public void SaveDisplayPreferences(DisplayPreferences prefs)
        {
            repository.SaveDisplayPreferences(prefs);
        }

        public void ShutdownDatabase()
        {
            repository.ShutdownDatabase();
        }

        public int ClearCache(string objType)
        {
            return repository.ClearCache(objType);
        }

        public bool ClearEntireCache() {
            return repository.ClearEntireCache();
        }

        public void MigratePlayState(ItemRepository repo)
        {
            repository.MigratePlayState(repo);
        }

        public void MigrateDisplayPrefs(ItemRepository repo)
        {
            repository.MigrateDisplayPrefs(repo);
        }

        public void MigrateItems()
        {
            repository.MigrateItems();
        }

    }
}
