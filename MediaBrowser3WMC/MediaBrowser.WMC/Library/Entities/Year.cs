using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Entities {
    // cause years need metadata too ...
    public class Year : BaseItem {
         public static Guid GetYearId(string name) {
            return ("year" + name.Trim()).GetMD5();
        }

        public static Year GetYear(string name) {
            Guid id = GetYearId(name);
            var year = Kernel.Instance.ItemRepository.RetrieveItem(id) as Year;
            if (year == null || year.Name == null) {
                year = new Year(id, name.Trim());
                Kernel.Instance.ItemRepository.SaveItem(year);
            }
            return year;
        }

        public Year() {
        }

        [Persist]
        [NotSourcedFromProvider]
        string name;

        public override string Name {
            get {
                return name;
            }
            set {
                name = value;
            }
        }

        public Year(Guid id, string name) {
            this.name = name;
            this.Id = id;
        }
    }
    
}
