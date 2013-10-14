using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Logging;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities
{
    public class IndexFolder : Series
    {
        public IndexFolder()
            : base()
        { }

        public IndexFolder(List<BaseItem> children)
        {
            //create an index folder with a specified set of children
            this.ActualChildren.Clear();
            this.AddChildren(children);
        }

        public string IndexId { get; set; }

        public override string ApiId
        {
            get
            {
                return IndexId;
            }
        }

        public virtual string DefaultPrimaryImagePath { get; set; }

        public override void ValidateChildren()
        {
            // If our children haven't been retrieved yet - get them
            if (!this.ActualChildren.Any()) RetrieveChildren();
        }

        public void AddChild(BaseItem child)
        {
            this.ActualChildren.Add(child);
            child.Parent = this;
        }

        public void AddChildren(List<BaseItem> children)
        {
            this.ActualChildren.AddRange(children);
            foreach (var child in children) child.Parent = this;
        }

        public override bool RefreshMetadata(Metadata.MetadataRefreshOptions options)
        {
            return false; //we don't have real metadata...
        }

        public override string DisplayPreferencesId
        {
            get
            {
                return (DisplayMediaType + Kernel.CurrentUser.Name).GetMD5().ToString();
            }
            set
            {
                base.DisplayPreferencesId = value;
            }
        }

    }
}
