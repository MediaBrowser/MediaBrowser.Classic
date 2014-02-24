using System;
using System.Collections.Generic;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library
{
    public class ItemFactory 
    {
        public static readonly ItemFactory Instance = new ItemFactory();
        public delegate bool IsOne(BaseItem baseItem);
        static Dictionary<IsOne,Type> itemFactoryItems;
 
        private ItemFactory() 
        {
            itemFactoryItems = new Dictionary<IsOne,Type>();            
        }

        public void AddFactory(IsOne isOne, Type type)
        {
            itemFactoryItems.Add(isOne,type);
        }

        public Item Create(BaseItem baseItem) 
        {
            Item item = null;

            foreach (IsOne isOne in itemFactoryItems.Keys)
                if (isOne(baseItem))
                    item = (Item)Activator.CreateInstance(itemFactoryItems[isOne]);

            if (item == null)
                if (baseItem is User)
                {
                    item = new UserItem();
                } else
                if (baseItem is Folder) {
                    item = new FolderModel();
                } else {
                    item = new Item();
                }
            item.Assign(baseItem);
            return item;
        }

    }
}
