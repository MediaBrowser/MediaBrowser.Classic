using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Entities;
using System.Diagnostics;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Persistance {
    public class SafeItemRepository : IItemRepository {

        IItemRepository repository;

        public SafeItemRepository (IItemRepository repository)
	    {
            this.repository = repository;
	    }

        static T SafeFunc<T>(Func<T> func) {
            T obj = default(T);
            try {
                obj = func();
            } catch (Exception ex) {
                Logger.ReportException("Failed to access repository ", ex);
            }
            return obj;
        }

        static void SafeAction(Action action) {
            try {
                action();
            } catch (Exception ex) {
                Logger.ReportException("Failed to access repository ", ex);
            }

        }

        public bool BackupDatabase()
        {
            return SafeFunc(() => repository.BackupDatabase());
        }

        public IEnumerable<IMetadataProvider> RetrieveProviders(Guid guid) {
            return SafeFunc(() => repository.RetrieveProviders(guid));
        }

        public void SaveProviders(Guid guid, IEnumerable<IMetadataProvider> providers) {
            SafeAction(() => repository.SaveProviders(guid, providers));
        }

        public void SaveItem(BaseItem item) {
            SafeAction(() => repository.SaveItem(item));
        }

        public BaseItem RetrieveItem(Guid guid) {
            return SafeFunc(() => repository.RetrieveItem(guid));
        }

        public void SaveChildren(Guid ownerName, IEnumerable<Guid> children) {
            SafeAction(() => repository.SaveChildren(ownerName, children));
        }

        public IEnumerable<BaseItem> RetrieveChildren(Guid id) {
            return SafeFunc(() => repository.RetrieveChildren(id));
        }

        public IList<Index> RetrieveIndex(Folder folder, string property, Func<string, BaseItem> constructor)
        {
            return SafeFunc(() =>repository.RetrieveIndex(folder, property, constructor));
        }

        public List<BaseItem> RetrieveSubIndex(string childTable, string property, object value)
        {
            return SafeFunc(() =>repository.RetrieveSubIndex(childTable, property, value));
        }

        public PlaybackStatus RetrievePlayState(Guid id) {
            return SafeFunc(() => repository.RetrievePlayState(id));
        }

        public DisplayPreferences RetrieveDisplayPreferences(DisplayPreferences dp)
        {
            return SafeFunc(() => repository.RetrieveDisplayPreferences(dp));
        }

        public ThumbSize RetrieveThumbSize(Guid id)
        {
            return SafeFunc(() => repository.RetrieveThumbSize(id));
        }

        public void SavePlayState(PlaybackStatus playState)
        {
            SafeAction(() => repository.SavePlayState(playState));
        }

        public void SaveDisplayPreferences(DisplayPreferences prefs) {
            SafeAction(() => repository.SaveDisplayPreferences(prefs));
        }

        public void ShutdownDatabase()
        {
            SafeAction(() => repository.ShutdownDatabase());
        }

        public int ClearCache(string objType)
        {
            return SafeFunc(() => repository.ClearCache(objType));
        }

        public bool ClearEntireCache() {
            return SafeFunc(() => repository.ClearEntireCache());
        }

        public void MigratePlayState(ItemRepository repo)
        {
            SafeAction(() => repository.MigratePlayState(repo));
        }

        public void MigrateDisplayPrefs(ItemRepository repo)
        {
            SafeAction(() => repository.MigrateDisplayPrefs(repo));
        }

        public void MigrateItems()
        {
            SafeAction(() => repository.MigrateItems());
        }

    }
}
