using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities {

    public class Actor  {

        public Actor(BaseItemPerson a)
        {
            this.Name = a.Name;
            this.Role = a.Role ?? (a.Type == PersonType.GuestStar ? "Guest Star" : "");
            this.PersonId = new Guid(a.Id);
            this.PrimaryImageTag = a.HasPrimaryImage ? a.PrimaryImageTag : null;
        }

        [Persist]
        public string Name { get; set; }

        [Persist]
        public string Role { get; set; }

        public string PrimaryImageTag { get; set; }

        private Person _person;
        public Person Person {
            get {
                return _person ?? (_person = new Person(this));
            }
        }

        public Guid PersonId { get; set; }

        public string DisplayName {
            get {
                if (string.IsNullOrEmpty(this.Role))
                    return Name;
                else
                    return Name;
            }
        }

        public Boolean HasRole {
            get {
                return (Role != null);
            }
        }

        public String DisplayRole {
            get {
                if (Role != null) {
                    return Role;
                } else
                    return "";
            }
        }

        public string DisplayString {
            get {
                if (string.IsNullOrEmpty(this.Role))
                    return Name;
                else
                    return Name + "..." + Role;
            }
        }
    }
}
