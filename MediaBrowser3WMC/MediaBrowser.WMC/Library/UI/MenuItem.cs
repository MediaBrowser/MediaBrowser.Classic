using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.MediaCenter.UI;

namespace MediaBrowser.Library
{
    /// <summary>
    /// Object to hold information for dynamic menu options (for context menu)
    /// </summary>
    public class MenuItem : ModelItem
    {
        public delegate void anAction(Item item);
        public delegate string aResource(Item item);

        protected int sortOrder = 0;
        protected string text = "Menu Option";
        protected string imageSource = "resx://MediaBrowser/MediaBrowser.Resources/BlankGraphic";
        protected bool enabled = true;
        protected bool available = true;
        protected anAction command;
        protected aResource dynamicText;
        protected aResource dynamicImage;
        protected List<Type> supportedTypes;
        protected List<MenuType> supportedMenus = new List<MenuType>() { MenuType.Item, MenuType.Detail };  //default supports item/detail as play is special case
        protected string itemType = "action";

        public string ItemType { get { return itemType; } }

        public int SortOrder { get { return sortOrder; } }

        public MenuItem()
        {
        }

        public MenuItem(string name, string image, anAction action)
        {
            text = name;
            imageSource = image;
            command = action;
        }

        public MenuItem(string name, string image, anAction action, List<MenuType> supports)
        {
            text = name;
            imageSource = image;
            command = action;
            supportedMenus = supports;
        }

        public MenuItem(string name, string image, anAction action, List<Type> supportsTypes, List<MenuType> supportsMenus)
        {
            text = name;
            imageSource = image;
            command = action;
            supportedTypes = supportsTypes;
            supportedMenus = supportsMenus;
        }

        public MenuItem(aResource name, string image, anAction action)
        {
            dynamicText = name;
            imageSource = image;
            command = action;
        }

        public MenuItem(aResource name, string image, anAction action, List<Type> supports)
        {
            dynamicText = name;
            imageSource = image;
            command = action;
            supportedTypes = supports;
        }

        public MenuItem(aResource name, string image, anAction action, List<Type> supportsTypes, List<MenuType> supportsMenus)
        {
            dynamicText = name;
            imageSource = image;
            command = action;
            supportedTypes = supportsTypes;
            supportedMenus = supportsMenus;
        }

        public MenuItem(string name, string image, anAction action, List<Type> supports)
        {
            text = name;
            imageSource = image;
            command = action;
            supportedTypes = supports;
        }

        public MenuItem(string name, string image, anAction action, int sort)
        {
            text = name;
            imageSource = image;
            command = action;
            sortOrder = sort;
        }

        public MenuItem(aResource name, string image, anAction action, int sort)
        {
            dynamicText = name;
            imageSource = image;
            command = action;
            sortOrder = sort;
        }

        public MenuItem(aResource name, aResource image, anAction action, int sort)
        {
            dynamicText = name;
            dynamicImage = image;
            command = action;
            sortOrder = sort;
        }

        public MenuItem(aResource name, aResource image, anAction action)
        {
            dynamicText = name;
            dynamicImage = image;
            command = action;
        }

        public string Text
        {
            get
            {
                if (dynamicText != null)
                    return dynamicText.Invoke(Application.CurrentInstance.CurrentItem);
                else
                    return text;
            }
            set { text = value; }
        }

        public Image Icon
        {
            get
            {
                if (dynamicImage != null)
                    return new Image (dynamicImage.Invoke(Application.CurrentInstance.CurrentItem));
                else
                    return new Image(imageSource);
            }
        }

        public virtual bool Enabled
        {
            get { return enabled; }
            set
            {
                if (value != enabled)
                {
                    enabled = value;
                    FirePropertyChanged("Enabled");
                    FirePropertyChanged("Text"); //in case we want to change text/icon as well
                    FirePropertyChanged("Icon");
                }
            }
        }

        public virtual bool Available
        {
            get { return available && IsSupported; }
            set
            {
                if (value != available)
                {
                    available = value;
                    FirePropertyChanged("Available");
                }
            }
        }

        public bool Supports(Type aType)
        {
            if (supportedTypes != null)
            {
                return supportedTypes.Contains(aType);
            }
            else return true;
        }

        public bool Supports(MenuType aMenuType)
        {
            if (supportedMenus != null)
            {
                return supportedMenus.Contains(aMenuType);
            }
            else return true;
        }

        public bool IsSupported
        {
            get
            {
                return Supports(Application.CurrentInstance.CurrentItem.BaseItem.GetType());
            }
        }

        public void DoCommand(Item item)
        {
            command.Invoke(item);
        }
    }

    public class SubMenu : MenuItem
    {
        protected List<MenuItem> menuItems;

        public SubMenu(string name, string image, List<MenuItem> items)
            : base(name, image, null)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public SubMenu(string name, string image, List<MenuItem> items, List<MenuType> supports)
            : base(name, image, null, supports)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public SubMenu(string name, string image, List<MenuItem> items, List<Type> supportsTypes, List<MenuType> supportsMenus)
            : base(name, image, null, supportsTypes, supportsMenus)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public SubMenu(aResource name, string image, List<MenuItem> items)
            : base(name, image, null)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public SubMenu(aResource name, string image, List<MenuItem> items, List<Type> supportsTypes, List<MenuType> supportsMenus)
            : base(name, image, null, supportsTypes, supportsMenus)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public SubMenu(string name, string image, List<MenuItem> items, List<Type> supports)
            : base(name, image, null, supports)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public SubMenu(string name, string image, List<MenuItem> items, int sort)
            : base(name, image, null, sort)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public SubMenu(aResource name, string image, List<MenuItem> items, int sort)
            : base(name, image, null, sort)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public SubMenu(aResource name, aResource image, List<MenuItem> items, int sort)
            : base(name, image, null, sort)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public SubMenu(aResource name, aResource image, List<MenuItem> items)
            : base(name, image, null)
        {
            menuItems = items;
            command = this.SetMenu;
            itemType = "menu";
        }

        public List<MenuItem> Items
        {
            get
            {
                return menuItems;
            }
        }

        private void SetMenu(Item ignore)
        {
            Application.CurrentInstance.ContextMenu = menuItems.Where(m => m.Available).ToList();
        }
    }

}
