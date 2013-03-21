using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Interfaces;

namespace MediaBrowser.Library
{
    public interface IItemRepository
    {
        IEnumerable<IMetadataProvider> RetrieveProviders(Guid guid);
        void SaveProviders(Guid guid, IEnumerable<IMetadataProvider> providers);

        void SaveItem(BaseItem item);
        BaseItem RetrieveItem(Guid name);
        void SaveChildren(Guid ownerName, IEnumerable<Guid> children);
        IEnumerable<BaseItem> RetrieveChildren(Guid id);
        IList<Index> RetrieveIndex(Folder folder, string property, Func<string, BaseItem> constructor);
        List<BaseItem> RetrieveSubIndex(string childTable, string property, object value);
        bool BackupDatabase();

        PlaybackStatus RetrievePlayState(Guid id);
        DisplayPreferences RetrieveDisplayPreferences(DisplayPreferences dp);
        ThumbSize RetrieveThumbSize(Guid id);

        void MigratePlayState(ItemRepository repo);
        void MigrateDisplayPrefs(ItemRepository repo);
        void MigrateItems();
        
        void SavePlayState( PlaybackStatus playState);
        void SaveDisplayPreferences(DisplayPreferences prefs);
        void ShutdownDatabase();

        int ClearCache(string objType);

        bool ClearEntireCache();
        
    }
}
