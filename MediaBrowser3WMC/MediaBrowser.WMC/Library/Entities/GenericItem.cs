using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Entities
{
    // cause years need metadata too ...
    public class GenericItem : BaseItem
    {
        public static Guid GetItemId(string name)
        {
            return ("generic" + name.Trim()).GetMD5();
        }

        public static GenericItem GetItem(string name)
        {
            Guid id = GetItemId(name);
            var item = Kernel.Instance.ItemRepository.RetrieveItem(id) as GenericItem;
            if (item == null || item.Name == null)
            {
                item = new GenericItem(id, name.Trim());
                Kernel.Instance.ItemRepository.SaveItem(item);
            }
            return item;
        }

        public GenericItem()
        {
        }

        [Persist]
        [NotSourcedFromProvider]
        string name;

        public override string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }

        public GenericItem(Guid id, string name)
        {
            this.name = name;
            this.Id = id;
        }
    }

}
