using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Library.Entities
{
    public class PersonLetterFolder : ApiSourcedFolder<PersonsQuery>
    {
        protected string[] PersonTypes { get; set; }
        protected bool UseLessThan { get; set; }
        protected string CompareString { get; set; }

        public PersonLetterFolder(string letter, bool? lessThan = null, string searchParentId = null, string[] personTypes = null, string[] includeTypes = null, string[] excludeTypes = null, Folder parent = null) :
            base(new BaseItem {Name = letter, Id = letter.GetMD5()}, searchParentId, includeTypes, excludeTypes, parent)
        {
            PersonTypes = personTypes;
            UseLessThan = lessThan ?? false;
            CompareString = String.Compare(letter, "A", StringComparison.Ordinal) < 0 ? "A" : letter;
        }

        public override PersonsQuery Query
        {
            get
            {
                var query = new PersonsQuery
                           {
                               UserId = Kernel.CurrentUser.ApiId,
                               ParentId = SearchParentId,
                               Recursive = true,
                               Fields = new[] {ItemFields.SortName},
                               SortBy = new[] {"SortName"},
                               PersonTypes = PersonTypes,
                           };
                if (UseLessThan)
                {
                    query.NameLessThan = CompareString;
                }
                else
                {
                    query.NameStartsWith = CompareString;
                }

                return query;
            }
        }

        protected override System.Collections.Generic.List<BaseItem> GetCachedChildren()
        {
            return Kernel.Instance.MB3ApiRepository.RetrievePersons(Query).Select(p => new ApiPersonFolder(p, SearchParentId, PersonTypes, null, null, this)).Cast<BaseItem>().ToList();
        }

        protected string GetCustomImagePath(string letter)
        {
            // see if we already have the image created in our cache
            var id = ("MBLETTERIMAGE" + letter).GetMD5();
            var path = CustomImageCache.Instance.GetImagePath(id, false);
            if (path != null) return path; //was already cached

            // create bitmap and draw letter on it
            var temp = new Bitmap(300, 300);
            var work = Graphics.FromImage(temp);
            work.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            work.DrawString(letter, new Font("Arial Black", 192), Brushes.White, 0, 0);
            work.Dispose();

            // now cache and return new path
            return CustomImageCache.Instance.CacheImage(id, temp);

        }


        public override string PrimaryImagePath
        {
            get
            {
                return GetCustomImagePath(Name);
            }
            set
            {
                base.PrimaryImagePath = value;
            }
        }
    }
}