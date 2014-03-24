using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Query;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Library.Entities;
using System.Collections;
using MediaBrowser.Library;
using System.Diagnostics;
using System.Threading;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Metadata;
using MediaBrowser.Library.Logging;
using MediaBrowser.Util;

namespace MediaBrowser.Code.ModelItems {
    public class FolderChildren : BaseModelItem, IList, ICollection, IList<Item>, IDisposable{

        FolderModel folderModel;
        Folder folder;
        Dictionary<Guid, Item> items = new Dictionary<Guid, Item>();
        IList<BaseItem> currentChildren = new List<BaseItem> ();
        Action onChildrenChanged;
        public bool FolderIsIndexed = false;
        float childImageAspect = 1;
        IComparer<BaseItem> sortFunction = new BaseItemComparer(SortOrder.Name);

        public void Assign(FolderModel folderModel, Action onChildrenChanged) {

            lock (this) {
                Debug.Assert(this.folderModel == null);
                Debug.Assert(this.folder == null);

                this.onChildrenChanged = onChildrenChanged;
                if (folderModel.Folder == this.folder && folderModel == this.folderModel) return;
                if (folder != null) StopListeningForChanges();
                this.folderModel = folderModel;
                this.folder = folderModel.Folder;
                this.sortFunction = folder.SortFunction;  //make sure this is in sync
                ListenForChanges();
            }

        }

        public static void LoadChildren(FolderChildren children) {
            children.folder.EnsureChildrenLoaded();
            
        }


        public static void VerifyChildren(FolderChildren children) {

            using (var profiler = new Profiler("Verify Children (UI Triggered) " + children.folder.Name))
            {

                // This is hairy, I do not want to change sigs, I also do not want to trigger expensive metadata refreshes UNLESS we detect changes in the folder.

                bool changed = false;
                var handler = new EventHandler<ChildrenChangedEventArgs>(
                    (source, args) =>
                    {
                        if (args != null && args.FolderContentChanged)
                        {
                            changed = true;
                        }
                        return;
                    }
                 );

                children.folder.ChildrenChanged += handler;
                children.folder.ValidateChildren();
                children.folder.ChildrenChanged -= handler;

            }
        }



        public void RefreshAsap() {
            LoadChildren(this);
            RefreshChildren();
        }

        public void ListenForChanges() {
            folder.ChildrenChanged += new EventHandler<ChildrenChangedEventArgs>(folder_ChildrenChanged);
        }

        public void StopListeningForChanges() {
            folder.ChildrenChanged -= new EventHandler<ChildrenChangedEventArgs>(folder_ChildrenChanged);
        }

        /// <summary>
        /// Creates a shallow clone to trick the binder into updating the list 
        /// </summary>
        /// <returns></returns>
        public FolderChildren Clone() {
            lock (this) {
                var clone = new FolderChildren();
                clone.folderModel = folderModel;
                clone.folder = folder;
                clone.items = items;
                clone.currentChildren = currentChildren;
                clone.onChildrenChanged = onChildrenChanged;
                clone.FolderIsIndexed = FolderIsIndexed;
                clone.childImageAspect = childImageAspect;
                clone.sortFunction = sortFunction;
                return clone;
            }
            
        }

        public void RefreshChildren()
        {
            this.folder_ChildrenChanged(this, null);
        }

        void folder_ChildrenChanged(object sender, ChildrenChangedEventArgs e) {
            if (!FolderIsIndexed) {
                lock (this) {
                    currentChildren = folder.Children;
                }
            }

            if (onChildrenChanged != null) onChildrenChanged();
        }

        // trigger a re-sort
        public void Sort() {
            Sort(sortFunction);
        }

        public void Sort(IComparer<BaseItem> sortFunction)
        {
            if (folder != null && !FolderIsIndexed) {
                this.sortFunction = sortFunction;
                Logger.ReportVerbose("Sorting " + folder.Name);
                Async.Queue("Background Sorter", () => folder.Sort(sortFunction));
            }
        }

        public Item this[int index] {
            get {
                BaseItem baseItem;
                lock (this) {

                    if (currentChildren.Count <= index) { 
                        // compensate for defer load
                        if (!FolderIsIndexed) {
                            currentChildren = folder.Children;
                        }
                    }

                    baseItem = currentChildren[index];
                }
                return GetItem(baseItem);
            }
            set {
                throw new NotImplementedException();
            }
        }

        private Item GetItem(BaseItem baseItem) {
            Guid guid = baseItem.Id;
            Item item;
            if (!items.TryGetValue(guid, out item)) {
                item = ItemFactory.Instance.Create(baseItem);
                item.PhysicalParent = folderModel;
                items[guid] = item;
            }
            return item;
        }

        public void IndexBy(string property)
        {

            if (folder == null) return;
            using (new Profiler("==== Index " + folder.Name + " by " + property))
            {
                if (string.IsNullOrEmpty(property) || property == Library.Localization.LocalizedStrings.Instance.GetString("NoneDispPref"))
                {
                    FolderIsIndexed = false;
                    lock (this)
                    {
                        currentChildren = folder.Children;
                    }
                }
                else
                {
                    lock (this)
                    {
                        try
                        {
                            currentChildren = folder.IndexBy(property).ToList();
                            items = new Dictionary<Guid, Item>();  // clear this out to force it to re-build with new filters
                            FolderIsIndexed = true;
                        }
                        catch (Exception e)
                        {
                            Logger.ReportException("Error building index on {0}", e, folder.Name);
                            Application.CurrentInstance.MessageBox("Error Building Index");
                            return;
                        }
                    }
                }

                folder_ChildrenChanged(this, null);
            }
        }

        public int Count {
            get {
                lock (this) {
                    return currentChildren.Count;
                }
            }
        }

        public IEnumerator<Item> GetEnumerator() {
            if (folder != null) {
                lock (this) {
                    foreach (var baseItem in currentChildren) {
                        yield return GetItem(baseItem);
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }

        int ICollection.Count {
            get { return this.Count; }
        }

        object IList.this[int index] {
            get {
                return this[index];
            }
            set {
                throw new NotImplementedException();
            }
        }

        
        public float GetChildAspect(bool useBanner) {
            Async.Queue("Aspect Ratio Calculator", () => CalculateChildAspect(useBanner));
            return childImageAspect;
        }

        private float CalculateChildAspect(bool useBanner) {

            Func<BaseItem, float> calcAspect;
            if (useBanner) {
                calcAspect = i => i.BannerImage != null ? i.BannerImage.Aspect : 0;
            } else {
                calcAspect = i => i.PrimaryImage != null ? i.PrimaryImage.Aspect : 0;
            }

            IList<BaseItem> childrenCopy;
            lock (this) {
                childrenCopy = this.currentChildren;
            }

            var aspects = childrenCopy
                .Select(i =>
                {
                    float aspect = 0;
                    try { aspect = calcAspect(i); } 
                    catch (Exception e) {
                        Logger.ReportException("Failed to calculate aspect for what would seem a dodge image for:" + i.Path, e);  
                    }
                    return aspect;
                } )
                .Where(ratio => ratio > 0)
                .Take(4).ToArray();

            float oldAspect = childImageAspect;
            if (aspects.Length > 0) {
                childImageAspect = aspects.Average();
            }
            if (childImageAspect != oldAspect) {
                folder_ChildrenChanged(this, null);
            }

            return childImageAspect;
        }



        #region Uninmplemented interfaces that are not supported

        public int IndexOf(Item item) {
            throw new NotImplementedException();
        }

        public void Insert(int index, Item item) {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index) {
            throw new NotImplementedException();
        }


        public void Add(Item item) {
            throw new NotImplementedException();
        }

        public void Clear() {
            throw new NotImplementedException();
        }

        public bool Contains(Item item) {
            throw new NotImplementedException();
        }

        public void CopyTo(Item[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public bool IsReadOnly {
            get { throw new NotImplementedException(); }
        }

        public bool Remove(Item item) {
            throw new NotImplementedException();
        }


        void ICollection.CopyTo(Array array, int index) {
            throw new NotImplementedException();
        }


        bool ICollection.IsSynchronized {
            get { throw new NotImplementedException(); }
        }

        object ICollection.SyncRoot {
            get { throw new NotImplementedException(); }
        }

        int IList.Add(object value) {
            throw new NotImplementedException();
        }

        void IList.Clear() {
            throw new NotImplementedException();
        }

        bool IList.Contains(object value) {
            throw new NotImplementedException();
        }

        int IList.IndexOf(object value) {
            throw new NotImplementedException();
        }

        void IList.Insert(int index, object value) {
            throw new NotImplementedException();
        }

        bool IList.IsFixedSize {
            get { throw new NotImplementedException(); }
        }

        bool IList.IsReadOnly {
            get { throw new NotImplementedException(); }
        }

        void IList.Remove(object value) {
            throw new NotImplementedException();
        }

        void IList.RemoveAt(int index) {
            throw new NotImplementedException();
        }


        #endregion

        void IDisposable.Dispose() {
            
        }

    }
}
