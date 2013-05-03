using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class FavoritesTypeFolder : LocalIbnSourcedCacheFolder
    {
        protected List<BaseItem> OurChildren;
        protected string[] OurTypes = new string[] {};
        protected string DisplayType;

        public FavoritesTypeFolder(string[] types, string display)
        {
            OurTypes = types;
            DisplayType = display;
            Id = (GetType().FullName + string.Concat(OurTypes)).GetMD5();
        }

        public override string Name
        {
            get { return "Favorite " + DisplayType; }
            set
            {
                base.Name = value;
            }
        }

        public void Clear()
        {
            OurChildren = null;
            OnChildrenChanged(null);
        }

        protected override string DefaultPrimaryImagePath
        {
            get
            {
                return "resx://MediaBrowser/MediaBrowser.Resources/Fav" + DisplayType;
            }
        }

        public override bool IsFavorite
        {
            get
            {
                return false;
            }
            set
            {
                // do nothing
            }
        }

        public override BaseItem ReLoad()
        {
            return new FavoritesTypeFolder(OurTypes, DisplayType);
        }

        protected override List<BaseItem> ActualChildren
        {
            get
            {
                return OurChildren ?? (OurChildren = Kernel.Instance.MB3ApiRepository.RetrieveItems(new ItemQuery
                                                                                                        {
                                                                                                            UserId = Kernel.CurrentUser.Id.ToString(),
                                                                                                            IncludeItemTypes = OurTypes,
                                                                                                            Recursive = true,
                                                                                                            Fields = MB3ApiRepository.StandardFields,
                                                                                                            Filters = new[] {ItemFilter.IsFavorite,}
                                                                                                        }).ToList());
            }
        }
    }
}
