using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class ApiSourcedFolder<T> : IndexFolder where T : new() 
    {
        private List<BaseItem> _children;
        public virtual T Query { get { return new T(); } }
        public virtual string[] IncludeItemTypes { get; set; }
        public virtual string[] ExcludeItemTypes { get; set; }
        protected string SearchParentId { get; set; }
        protected bool PrimaryImageChecked { get; set; }
        protected bool BackdropImageChecked { get; set; }
        protected bool HasShadowItem { get; set; }

        public ApiSourcedFolder() : base()
        {
        }

        public ApiSourcedFolder(BaseItem item, string searchParentId = null, string[] includeTypes = null, string[] excludeTypes = null, Folder parent = null)
        {
            Init(item, searchParentId, includeTypes, excludeTypes, parent);
        }

        protected void Init(BaseItem item, string searchParentId = null, string[] includeTypes = null, string[] excludeTypes = null, Folder parent = null)
        {
            Name = item.Name;
            Id = item.Id;
            SearchParentId = searchParentId ?? Kernel.Instance.RootFolder.ApiId;
            PrimaryImagePath = !string.IsNullOrEmpty(item.PrimaryImagePath) ? item.PrimaryImagePath : null;
            BackdropImagePaths = item.BackdropImagePaths;
            BannerImagePath = item.BannerImagePath;
            DisplayMediaType = item.DisplayMediaType;
            IncludeItemTypes = includeTypes;
            ExcludeItemTypes = excludeTypes;
            HasShadowItem = true;
            Parent = parent;
        }

        public override string ApiId
        {
            get
            {
                return Id.ToString();
            }
        }

        protected override string RalParentId
        {
            get { return Kernel.Instance.RootFolder.ApiId; }
        }

        public override string[] RalIncludeTypes
        {
            get
            {
                return base.RalIncludeTypes ?? IncludeItemTypes;
            }
            set { base.RalIncludeTypes = value; }
        }

        protected override List<BaseItem> GetCachedChildren()
        {
            return typeof(T) == typeof(ItemQuery) ? Kernel.Instance.MB3ApiRepository.RetrieveItems(Query as ItemQuery).ToList()
                       : new List<BaseItem>();
        }

        public override BaseItem ReLoad()
        {
            RetrieveChildren();
            return this;
        }

        public override string PrimaryImagePath
        {
            get { return HasShadowItem ? base.PrimaryImagePath : base.PrimaryImagePath ?? (base.PrimaryImagePath = !PrimaryImageChecked ? GetImagePath(ImageType.Primary) : null); }
            set
            {
                base.PrimaryImagePath = value;
            }
        }

        public override List<string> BackdropImagePaths
        {
            get
            {
                return HasShadowItem ? base.BackdropImagePaths :  base.BackdropImagePaths != null && base.BackdropImagePaths.Count > 0 ? base.BackdropImagePaths : (base.BackdropImagePaths = new List<string> {!BackdropImageChecked ? GetImagePath(ImageType.Backdrop) : null });
            }
            set
            {
                base.BackdropImagePaths = value;
            }
        }

        protected virtual string GetImagePath(ImageType imageType)
        {
            if (this.Name == null) return null;

            switch (imageType)
            {
                case ImageType.Backdrop:
                    BackdropImageChecked = true;
                    break;
                case ImageType.Primary:
                    PrimaryImageChecked = true;
                    break;
            }
            //Look for it on the server IBN
            return Kernel.ApiClient.GetGeneralIbnImageUrl(this.Name, new ImageOptions { ImageType = imageType });

        }
    }
}
