using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library
{
    public class ActorItemWrapper : BaseModelItem
    {
        public Actor Actor { get; set; }
        private FolderModel parent;
        private Item item = null;

        // to keep mcml happy
        public ActorItemWrapper() {
        }

        public ActorItemWrapper(Actor actor, FolderModel parent)
        {
            this.Actor = actor;
            this.parent = parent;
        }

        public Item Item
        {
            get
            {
                if (item != null) return item;

                item = ItemFactory.Instance.Create(Actor.Person);
                item.PhysicalParent = parent;
                return item;
              
            }
        }
    }
}
