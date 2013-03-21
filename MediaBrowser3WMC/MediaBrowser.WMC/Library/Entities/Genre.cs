using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Entities.Attributes;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library.Entities {
    public class Genre : BaseItem {

        public static Guid GetGenreId(string name) {
            return ("genre" + name.Trim()).GetMD5();
        }

        public static Genre GetGenre(string name) {
            Guid id = GetGenreId(name);
            var genre = Kernel.Instance.ItemRepository.RetrieveItem(id) as Genre;
            if (genre == null || genre.Name == null) {
                genre = new Genre(id, name.Trim());
                Kernel.Instance.ItemRepository.SaveItem(genre);
            }
            return genre;
        }

        public Genre() {
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

        public Genre(Guid id, string name) {
            this.name = name;
            this.Id = id;
        }
    }
}
