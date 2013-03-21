using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Entities {
    public class Person : BaseItem {

        public static Guid GetPersonId(string name) {
            return ("person" + name.Trim()).GetMD5();
        }

        public static Person GetPerson(string name) {
            Guid id = GetPersonId(name);
            var person = Kernel.Instance.ItemRepository.RetrieveItem(id) as Person;
                if (person == null || person.Name == null) {
                    person = new Person(id, name.Trim());
                    Kernel.Instance.ItemRepository.SaveItem(person);
                }
                return person;
        }
        
        public Person() {
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

        public override string SortName
        {
            get
            {
                return Sorting.SortHelper.GetSortableName(Name);
            }
            set
            {
                base.SortName = value;
            }
        }

        public Person(Guid id, string name) {
            this.name = name;
            this.Id = id;
        }
    }
}
