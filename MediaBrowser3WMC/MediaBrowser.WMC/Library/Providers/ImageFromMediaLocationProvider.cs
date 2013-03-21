using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Providers.Attributes;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library.Providers
{
    [ProviderPriority(15)]
    [SupportedType(typeof(BaseItem))]
    [SkipSerializationValidation]
    public class ImageFromMediaLocationProvider : BaseMetadataProvider
    {

        const string Primary = "folder";
        const string Banner = "banner";
        const string Backdrop = "backdrop";
        const string Logo = "logo";
        const string Art = "clearart";
        const string Thumbnail = "thumb";
        const string Disc = "disc";
        

        [Persist]
        List<string> backdropPaths;
        [Persist]
        string bannerPath;
        [Persist]
        string primaryPath;
        [Persist]
        string logoPath;
        [Persist]
        string artPath;
        [Persist]
        string thumbPath;
        [Persist]
        string discPath;


        protected virtual string Location { get { return Item.Path == null ? "" : Item.Path.ToLower(); } }

        public override void Fetch()
        {
            if (Location == null) return;

            bool isDir = Directory.Exists(Location);

            if (isDir || File.Exists(Location))
            {
                Item.PrimaryImagePath = primaryPath = FindImage(Primary);
            }
            if (isDir)
            {
                Item.BannerImagePath = bannerPath = FindImage(Banner);
                Item.LogoImagePath = logoPath = FindImage(Logo);
                Item.ArtImagePath = artPath = FindImage(Art);
                Item.ThumbnailImagePath = thumbPath = FindImage(Thumbnail);
                Item.DiscImagePath = discPath = FindImage(Disc);
                backdropPaths = FindImages(Backdrop);
                if (backdropPaths.Count > 0) {
                    Item.BackdropImagePaths = backdropPaths;
                }
            }
        }

        private List<string> FindImages(string name) {
            var paths = new List<string>();

            string postfix = "";
            int index = 1;

            do
            {
                string currentImage = FindImage(name + postfix);
                if (currentImage == null) break;
                paths.Add(currentImage);
                postfix = index.ToString();
                index++;

            } while (true);

            return paths;
        }

        private string FindImage(string name)
        {
            string primaryExt = ".jpg";
            string secondaryExt = ".png";

            if (Config.Instance.PNGTakesPrecedence)
            {
                primaryExt = ".png";
                secondaryExt = ".jpg";
            }

            string file = Path.Combine(Location, name + primaryExt);
            if (File.Exists(file))
            {
                return file;
            }
            else
            {
                file = Path.Combine(Location, name + secondaryExt);
                if (File.Exists(file))
                    return file;
            }

            if (name == "folder") // we will also look for images that match by name in the same location for the primary image
            {
                var dir = Path.GetDirectoryName(Location);
                var filename_without_extension = Path.GetFileNameWithoutExtension(Location);

                // dir was null for \\10.0.0.4\dvds - workaround
                if (dir != null && filename_without_extension != null)
                {
                    file = Path.Combine(dir, filename_without_extension);
                    if (File.Exists(file + primaryExt))
                        return (file + primaryExt).ToLower();
                    if (File.Exists(file + secondaryExt))
                        return (file + secondaryExt).ToLower();
                }
            }
            return null;
        }

        Dictionary<string, string> PathLookup {
            get {
                var dict = new Dictionary<string, string>();
                dict[Primary] = primaryPath;
                dict[Banner] = bannerPath;
                return dict;
            }
        }

        public override bool NeedsRefresh()
        {
            // nothing we can do with empty location 
            if (string.IsNullOrEmpty(Location)) return false;

            // image moved or image deleted
            bool changed = FindImage(Primary) != primaryPath;
            if (changed) MediaBrowser.Library.Logging.Logger.ReportVerbose("primary image changed. primaryPath: "+primaryPath);
            changed |= FindImage(Banner) != bannerPath;
            if (changed) MediaBrowser.Library.Logging.Logger.ReportVerbose("banner changed");
            changed |= FindImage(Logo) != logoPath;
            if (changed) MediaBrowser.Library.Logging.Logger.ReportVerbose("logo changed");
            changed |= FindImage(Thumbnail) != thumbPath;
            if (changed) MediaBrowser.Library.Logging.Logger.ReportVerbose("thumb changed");
            changed |= FindImage(Art) != artPath;
            if (changed) MediaBrowser.Library.Logging.Logger.ReportVerbose("art changed");
            changed |= FindImage(Disc) != discPath;
            if (changed) MediaBrowser.Library.Logging.Logger.ReportVerbose("disc changed");

            var realBackdrops = FindImages(Backdrop);
            changed |= realBackdrops.Except(backdropPaths ?? new List<string> ()).Count() != 0;
            if (changed) MediaBrowser.Library.Logging.Logger.ReportVerbose("backdrops changed");

            // Basic item corruption fix
            if (!changed) {
                changed |= primaryPath != null && Item.PrimaryImagePath == null;
                if (changed) MediaBrowser.Library.Logging.Logger.ReportVerbose("provider and item paths don't match:");
            }

            return changed;
        }

    }
}
