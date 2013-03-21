using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.MediaCenter.UI;

namespace MediaBrowser.Code.ModelItems {
    public class BaseModelItem : IModelItem, IDisposable {

        #region IModelItem Members

        string description = string.Empty;
        public string Description {
            get {
                return description;
            }
            set {
                description = value;
            }
        }
        bool selected;
        public bool Selected {
            get {
                return selected;
            }
            set {
                selected = value;
            }
        }

        Guid uniqueId = Guid.NewGuid();
        public Guid UniqueId {
            get {
                return uniqueId;
            }
            set {
                uniqueId = value;
            }
        }

        #endregion

        #region IPropertyObject Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region IModelItemOwner Members

        protected void FireAllPropertiesChanged() {
            RunInUIThread(() =>
            {
                foreach (var property in this.GetType().GetProperties()) {
                    FirePropertyChanged(property.Name);
                }
            });
        }

        protected void FirePropertiesChanged(params string[] properties) {
            RunInUIThread(() =>
            {
                if (PropertyChanged != null && properties != null) {
                    foreach (var property in properties) {
                        PropertyChanged(this, property);
                    }
                }
            });
        }

        void RunInUIThread(Action action) {
            if (Microsoft.MediaCenter.UI.Application.ApplicationThread != System.Threading.Thread.CurrentThread) {
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => action());
            } else {
                action();
            }
        }


        protected void FirePropertyChanged(string property) {
           RunInUIThread(() =>
           {
               if (PropertyChanged != null) {
                   PropertyChanged(this, property);
               }
           });
        }

        List<ModelItem> items = new List<ModelItem>();

        public void RegisterObject(ModelItem modelItem) {
            items.Add(modelItem);
        }

        public void UnregisterObject(ModelItem modelItem) {
            if (items.Exists((i) => i == modelItem)) {
                modelItem.Dispose();
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing) {
            if (isDisposing) {
                foreach (var item in items) {
                    item.Dispose();
                }
            }
        }

        ~BaseModelItem() {
            Dispose(false);
        }

        #endregion
    }
}
