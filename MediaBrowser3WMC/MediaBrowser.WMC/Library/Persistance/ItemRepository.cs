using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.LibraryManagement;
using System.IO;
using System.Diagnostics;
using System.Threading;
using MediaBrowser.Util;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Linq;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Util;
using System.Reflection;
using System.Drawing;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library {

    [Serializable]
    public class ThumbSize 
    {
        [Persist]
        public int Width = 0;
        [Persist]
        public int Height = 0;

        public ThumbSize(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        public ThumbSize() { } //for the serializer
    }

    public class ItemRepository : IItemRepository, IDisposable {
        public ItemRepository() {
            playbackStatus = new FileBasedDictionary<PlaybackStatus>(GetPath("playstate", userSettingPath));
            thumbSizes = new FileBasedDictionary<ThumbSize>(GetPath("thumbsizes", userSettingPath));
        }

        string rootPath = ApplicationPaths.AppCachePath;
        string userSettingPath = ApplicationPaths.AppUserSettingsPath;

        FileBasedDictionary<PlaybackStatus> playbackStatus;
        FileBasedDictionary<ThumbSize> thumbSizes;

        public List<PlaybackStatus> AllPlayStates
        { //for migration
            get
            {
                var ret = new List<PlaybackStatus>();
                foreach (var ps in playbackStatus.AllItems)
                {
                    ret.Add(ps as PlaybackStatus);
                }
                return ret;
            }
        }

        public List<Guid> AllItems
        { //for migration
            get
            {
                var ret = new List<Guid>();
                string path = GetPath("items", rootPath);
                foreach (var file in Directory.GetFiles(path))
                {
                    ret.Add(new Guid(Path.GetFileName(file)));
                }
                return ret;
            }
        }

        public bool Backup(string type)
        {
            string root = type.ToLower() == "display" || type == "playstate" ? userSettingPath : rootPath;
            string source = GetPath(type, root);
            try
            {
                Directory.Move(source, source + ".bak");
            }
            catch (Exception e)
            {
                Logger.ReportException("Failed to backup " + type, e);
                return false;
            }
            return true;
        }

        #region IItemCacheProvider Members

        public void SaveChildren(Guid id, IEnumerable<Guid> children) {
            string file = GetChildrenFilename(id);

            Guid[] childrenCopy;
            lock (children) {
                childrenCopy = children.ToArray();
            }

            using (Stream fs = WriteExclusiveFileStream(file)) {
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(childrenCopy.Length);
                foreach (var guid in childrenCopy) {
                    bw.Write(guid);
                }
            }

        }

        public void ShutdownDatabase()
        {
            //nothing to do here
            return;
        }

        public IEnumerable<Guid> RetrieveChildrenOld(Guid id)
        {

            List<Guid> children = new List<Guid>();
            string file = GetChildrenFilename(id);
            if (!File.Exists(file)) return null;

            try
            {

                using (Stream fs = ReadFileStream(file))
                {
                    BinaryReader br = new BinaryReader(fs);
                    lock (children)
                    {
                        var count = br.ReadInt32();
                        var itemsRead = 0;
                        while (itemsRead < count)
                        {
                            children.Add(br.ReadGuid());
                            itemsRead++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Failed to retrieve children:", e);
#if DEBUG
                throw;
#else
                return null;
#endif

            }

            return children.Count == 0 ? null : children;
        }

        public IEnumerable<BaseItem> RetrieveChildren(Guid id)
        {
            List<BaseItem> children = new List<BaseItem>();
            var cached = RetrieveChildrenOld(id);
            if (cached != null)
            {
                foreach (var guid in cached)
                {
                    var item = RetrieveItem(guid);
                    if (item != null)
                    {
                        children.Add(item);
                    }
                }
            }

            return children.Count == 0 ? null : children;
        }

        public IList<Index> RetrieveIndex(Folder folder, string property, Func<string, BaseItem> constructor)
        {
            //compatability with new repo
            return null;
        }

        public List<BaseItem> RetrieveSubIndex(string childTable, string property, object value)
        {
            return null;
        }

        public bool BackupDatabase()
        {
            return true;
        }

        public void MigratePlayState(ItemRepository repo)
        {
            throw new NotImplementedException();
        }

        public void MigrateDisplayPrefs(ItemRepository repo)
        {
            throw new NotImplementedException();
        }

        public void MigrateItems()
        {
            throw new NotImplementedException();
        }

        public PlaybackStatus RetrievePlayState(Guid id) {
            return playbackStatus[id]; 
        }

        public ThumbSize RetrieveThumbSize(Guid id)
        {
            return thumbSizes[id];
        }

        public DisplayPreferences RetrieveDisplayPreferences(DisplayPreferences dp) {
            string file = GetDisplayPrefsFile(dp.Id);

            if (File.Exists(file)) {
                using (Stream fs = ReadFileStream(file)) {
                    dp.ReadFromStream(new BinaryReader(fs));
                    return dp;
                }
            } 

            return null;
        }

        public void SavePlayState(PlaybackStatus playState) {
            playbackStatus[playState.Id] = playState;
        }

        public void SaveDisplayPreferences(DisplayPreferences prefs) {
            string file = GetDisplayPrefsFile(prefs.Id);
            using (Stream fs = WriteExclusiveFileStream(file)) {
                prefs.WriteToStream(new BinaryWriter(fs));
            }
            //also save the thumb size in a way we can access outside of MC
            thumbSizes[prefs.Id] = new ThumbSize(prefs.ThumbConstraint.Value.Width, prefs.ThumbConstraint.Value.Height);
        }

        public BaseItem RetrieveItem(Guid id) {
            BaseItem item = null;
            string file = GetItemFilename(id);

            try {
                using (Stream fs = ReadFileStream(file)) {
                    BinaryReader reader = new BinaryReader(fs);
                    item = Serializer.Deserialize<BaseItem>(fs);
                }
            } catch (FileNotFoundException) { 
                // we expect to be called with unknown items sometimes
            }
            return item;
        }

        public void SaveItem(BaseItem item) {
            string file = GetItemFilename(item.Id);
            using (Stream fs = WriteExclusiveFileStream(file)) {
                BinaryWriter bw = new BinaryWriter(fs);
                Serializer.Serialize(bw.BaseStream, item);
            }
        }


        // TODO implement IEnumerable serialization
        class MetadataProviderSearilizationWrapper {
            [Persist]
            public List<IMetadataProvider> Providers {get; set;}
        }

        public IEnumerable<IMetadataProvider> RetrieveProviders(Guid guid) {
            MetadataProviderSearilizationWrapper data = null;
            string file = GetProviderFilename(guid);

            try {
                using (Stream fs = ReadFileStream(file)) {
                    BinaryReader reader = new BinaryReader(fs);
                    data = (MetadataProviderSearilizationWrapper)Serializer.Deserialize<object>(fs);
                }
            } catch (FileNotFoundException) { return null; }

            return data.Providers;
        }

        public void SaveProviders(Guid guid, IEnumerable<IMetadataProvider> providers) {
            string file = GetProviderFilename(guid);
            using (Stream fs = WriteExclusiveFileStream(file)) {
                BinaryWriter bw = new BinaryWriter(fs);
                Serializer.Serialize<object>(bw.BaseStream,
                    new MetadataProviderSearilizationWrapper() { Providers = providers.ToList() });
            }
        }


        private static Stream WriteExclusiveFileStream(string file) {
            return ProtectedFileStream.OpenExclusiveWriter(file);
        }

        private static Stream ReadFileStream(string file) {
            return ProtectedFileStream.OpenSharedReader(file);
        }


        private string GetChildrenFilename(Guid id) {
            return GetFile("children", id);
        }

        private string GetItemFilename(Guid id) {
            return GetFile("items", id);
        }

        private string GetProviderFilename(Guid id) {
            return GetFile("providerdata", id);
        }

        private string GetDisplayPrefsFile(Guid id) {
            return GetFile("display", id, this.userSettingPath);
        }

        private string GetPlaystateFile(Guid id) {
            return GetFile("playstate", id, this.userSettingPath);
        }

        private string GetThumbSizeFile(Guid id)
        {
            return GetFile("thumbsizes", id, this.userSettingPath);
        }

        private string GetFile(string type, Guid id)
        {
            return GetFile(type, id, rootPath);
        }


        private string GetFile(string type, Guid id, string root) {

            return Path.Combine(GetPath(type,root), id.ToString("N"));
        }

        private string GetPath(string type, string root) {
            string path = Path.Combine(root, type);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public int ClearCache(string objType)
        {
            throw new NotImplementedException();
        }

        public bool ClearEntireCache() {
            bool success = true;
            
            // we are going to need a semaphore here.
            //lock (ProtectedFileStream.GlobalLock) {
            try
            {
                success &= DeleteFolder(Path.Combine(ApplicationPaths.AppCachePath, "items"));
                success &= DeleteFolder(Path.Combine(ApplicationPaths.AppCachePath, "providerdata"));
                success &= DeleteFolder(Path.Combine(ApplicationPaths.AppCachePath, "autoplaylists"));
                success &= DeleteFolder(Path.Combine(ApplicationPaths.AppCachePath, "children"));
            }
            catch (Exception e)
            {
                Logger.ReportException("Error attempting to clear cache.", e);
                return false;
            }
            //}
            return success;
        }

        private bool DeleteFolder(string p) {
            try {
                Directory.Delete(p, true);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            playbackStatus.Dispose();
            thumbSizes.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion




    }
}
