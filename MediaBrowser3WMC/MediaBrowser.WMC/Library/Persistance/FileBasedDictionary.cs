using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaBrowser.Library.Factories;
using System.Diagnostics;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Threading;
using System.Threading;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Persistance {
    public class FileBasedDictionary<T> : IDisposable where T : class {

        readonly string FastLoadFile;
        Dictionary<Guid, DatedObject> dictionary = new Dictionary<Guid, DatedObject>();
        FileSystemWatcher watcher;
        string path;
        ManualResetEvent asyncValidationDone = new ManualResetEvent(false);
        bool enableAsyncValidation;

#if DEBUG
        public string TrackingId { get; set; }
#endif

        class IdentifiableData {
            [Persist]
            public Guid Guid { get; set; }

            [Persist]
            public T Data { get; set; }
        }

        class FastLoadData {
            [Persist]
            public List<IdentifiableData> Items { get; set; }
        }

        struct DatedObject {
            public DateTime FileDate;
            public T Data;
        }


        public FileBasedDictionary(string path)
            : this(path, true) {

        }

        internal FileBasedDictionary(string path, bool enableAsyncValidation) {
            Debug.Assert(Directory.Exists(path));

            this.enableAsyncValidation = enableAsyncValidation;

            FastLoadFile = Path.Combine(path, "FastLoad");

            this.path = path;
            watcher = new FileSystemWatcher(path);
            watcher.Changed += new FileSystemEventHandler(DirectoryChanged);
            watcher.EnableRaisingEvents = true;

            if (enableAsyncValidation) {
                Async.Queue("Fast Load Loader",() =>
                {
                    using (new MediaBrowser.Util.Profiler("Dictionary loading and validation"))
                    {
                        LoadFastLoadData();
                        Validate();
                    }
                },
                    () => asyncValidationDone.Set()
                 );
            }

        }

        // marked internal for testing (perhaps we should use reflection to test) 
        internal void LoadFastLoadData() {

            FastLoadData data = null;
            try {

                using (var stream = ProtectedFileStream.OpenSharedReader(FastLoadFile)) {
                    data = Serializer.Deserialize<object>(stream) as FastLoadData;
                }
            } catch (Exception e) {
                Logger.ReportException("Failed to load fast load data: ", e);
            }

            if (data != null && data.Items != null) {
                lock (dictionary) {
                    foreach (var item in data.Items) {
                        if (!dictionary.ContainsKey(item.Guid)) {
                            dictionary[item.Guid] = new DatedObject() { FileDate = DateTime.MinValue, Data = item.Data };
                        }

                    }
                }

                Logger.ReportInfo("Successfully loaded fastload data for : " + path + " " + typeof(T).ToString());
            }
        }

        void DirectoryChanged(object sender, FileSystemEventArgs e) {
            if (e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created) {
                if (e.FullPath != FastLoadFile) {
                    var data = LoadFile(e.FullPath);
                    if (data != null) {
                        SetInternalData(GetGuid(e.FullPath).Value, data, Kernel.Instance.GetLocation(path).DateModified);
                    }
                }
            }
        }

        public void Validate() {
            var loadedData = new Dictionary<Guid, T>();
            var directory = Kernel.Instance.GetLocation<IFolderMediaLocation>(path);

            List<Guid> validChildren = new List<Guid>();
            foreach (var item in directory.Children.OrderBy(key => key.DateModified).Reverse()) {

                if (item is IFolderMediaLocation) continue;
                if (item.Path == FastLoadFile) continue;

                var guid = GetGuid(item.Path);
                DatedObject data;

                if (guid != null) {
                    lock (dictionary) {
                        if (dictionary.TryGetValue(guid.Value, out data)) {
                            if (data.FileDate == item.DateModified) {
                                validChildren.Add(guid.Value);
                                continue;
                            }
                        }
                    }
                }

                T obj = LoadFile(item.Path);

                if (obj != null) {
                    SetInternalData(guid.Value, obj, item.DateModified);
                    validChildren.Add(guid.Value);
                }
            }

            lock (dictionary) {
                foreach (var key in dictionary.Keys.Except(validChildren).ToArray()) {
                    dictionary.Remove(key);
                }
            }

            // Save the fastload file

            FastLoadData fastLoadData;
            lock (dictionary) {
                fastLoadData = new FastLoadData()
                {
                    Items = dictionary.Select(pair => new IdentifiableData() { Guid = pair.Key, Data = pair.Value.Data })
                        .ToList()
                };
            }

            using (var stream = ProtectedFileStream.OpenExclusiveWriter(FastLoadFile)) {
                Serializer.Serialize<object>(stream, fastLoadData);
            }

            Logger.ReportInfo("Finished validating : " + path + " " + typeof(T).ToString());

        }

        private void SetInternalData(Guid guid, T data, DateTime date) {

            lock (dictionary) {
                dictionary[guid] = new DatedObject() { Data = data, FileDate = date };
            }

            if (data is IPersistableChangeNotifiable) {
                (data as IPersistableChangeNotifiable).OnChanged();
            }
        }

        public T this[Guid guid] {
            get {
                return GetData(guid);
            }
            set {
                SetData(guid, value);
            }
        }

        public IEnumerable<object> AllItems
        {
            get
            {
                lock (dictionary)
                {
                    foreach (var obj in dictionary.Values)
                    {
                        yield return obj.Data;
                    }
                }
            }
        }

        private T GetData(Guid guid) {
            DatedObject dataObject;
            if (dictionary.TryGetValue(guid, out dataObject)) {
                return dataObject.Data;
            }

            // during load we may have an incomplete cache
            string filename = GetFilename(guid);
            var location = Kernel.Instance.GetLocation(filename);
            if (location != null) {
                var data = LoadFile(location.Path);
                if (data != null) {
                    SetInternalData(guid, data, location.DateModified);
                    return data;
                }
            }

            return null;
        }

        private void SetData(Guid guid, T value) {
            var filename = GetFilename(guid);
            using (var stream = ProtectedFileStream.OpenExclusiveWriter(filename)) {
                Serializer.Serialize<object>(stream, value);
            }
            SetInternalData(guid, value, DateTime.MinValue);
        }

        private string GetFilename(Guid guid) {
            var filename = Path.Combine(path, guid.ToString("N"));
            return filename;
        }

        private Guid? GetGuid(string path) {
            Guid? guid = null;
            try {
                guid = new Guid(Path.GetFileName(path));
            } catch (FormatException) { }

            if (guid == null) {
                Logger.ReportWarning("Attempting to load invalid file! All files in the directory should be guids");
                return null;
            }

            return guid;
        }

        private T LoadFile(string path) {

            var guid = GetGuid(path);
            if (guid == null) return null;

            T data = null;

            try {
                // we have a guid
                
                using (var stream = ProtectedFileStream.OpenSharedReader(path)) {
                    data = Serializer.Deserialize<object>(stream) as T;
                }

                if (data == null) {
                    Logger.ReportWarning("Invalid data was detected in the file : " + path);
                    guid = null;
                } else {
                    DatedObject current;
                    lock (dictionary) {
                        if (dictionary.TryGetValue(guid.Value, out current)) {
                            Serializer.Merge(data, current.Data, true);
                            data = current.Data;
                        }
                    }
                }
            } catch (Exception e) {
                Logger.ReportException("Failed to load date: ", e);
            }

            return data;
        }



        #region IDisposable Members

        public void Dispose() {
            
            if (enableAsyncValidation) asyncValidationDone.WaitOne();
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= new FileSystemEventHandler(DirectoryChanged);
            watcher.Dispose();
        }

        #endregion
    }
}
