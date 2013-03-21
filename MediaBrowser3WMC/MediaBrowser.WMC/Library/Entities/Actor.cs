using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Entities {

    public class Actor  {

        [Persist]
        public string Name { get; set; }

        [Persist]
        public string Role { get; set; }

        public Person Person {
            get {
                var person = Person.GetPerson(Name);
                return person;
            }
        }

        public Guid PersonId {
            get {
                return Person.GetPersonId(Name);
            }
        }

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
