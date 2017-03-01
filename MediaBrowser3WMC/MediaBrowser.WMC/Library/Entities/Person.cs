using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities {
    public class Person : BaseItem {

        public static Guid GetPersonId(string name) {
            return ("person" + name.Trim()).GetMD5();
        }

        public static Person GetPerson(string name) {
            var person = Kernel.Instance.MB3ApiRepository.RetrievePerson(name);
                if (person == null || person.Name == null) {
                    person = new Person(GetPersonId(name), name.Trim());
                }
                return person;
        }

        public Person(Actor actor)
        {
            this.name = actor.Name;
            this.Id = actor.PersonId;
            this.primaryImageTag = actor.PrimaryImageTag;
        }
        
        public Person() {
        }

        [Persist]
        [NotSourcedFromProvider]
        string name;
        private string primaryImageTag;

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

        public override string PrimaryImagePath
        {
            get { return primaryImageTag != null ? Kernel.ApiClient.GetPersonImageUrl(Id, new ImageOptions {ImageType = ImageType.Primary, Tag = primaryImageTag, MaxWidth = 400}) : null; }
            set { base.PrimaryImagePath = value;  }
        }
    }
}
