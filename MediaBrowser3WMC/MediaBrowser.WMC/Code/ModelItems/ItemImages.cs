using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using MediaBrowser.Library;
using System.Threading;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.ImageManagement;
using System.Reflection;
using System.IO;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Factories;
using System.Diagnostics;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Threading;
using System.Runtime.InteropServices;
using MediaBrowser.Library.Logging;
using Image = Microsoft.MediaCenter.UI.Image;
using Size = Microsoft.MediaCenter.UI.Size;

namespace MediaBrowser.Library
{
    public partial class Item
    {
        public bool HasBannerImage
        {
            get
            {
                return (BaseItem.BannerImagePath != null) ||
                    (PhysicalParent != null ? PhysicalParent.HasBannerImage : false);
            }
        }

        AsyncImageLoader bannerImage = null;
        public Image BannerImage
        {
            get
            {
                if (!HasBannerImage)
                {
                    if (PhysicalParent != null)
                    {
                        return PhysicalParent.BannerImage;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (bannerImage == null)
                {
                    bannerImage = new AsyncImageLoader(
                        () => baseItem.BannerImage,
                        null,
                        () => this.FirePropertiesChanged("PreferredImage", "PreferredImageSmall", "BannerImage"));
                }
                return bannerImage.Image;
            }
        }

        public bool HasBackdropImage
        {
            get
            {
                return baseItem.BackdropImagePath != null;
            }
        }

        AsyncImageLoader backdropImage = null;
        public Image BackdropImage
        {
            get
            {
                if (!HasBackdropImage)
                {
                    if (PhysicalParent != null)
                    {
                        return PhysicalParent.BackdropImage;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (backdropImage == null)
                {
                    if (Config.Instance.RandomizeBackdrops)
                    {
                        getRandomBackdropImage();
                    }
                    else
                    {
                        getFirstBackdropImage();
                    }
                }
                if (backdropImage != null) //may not have had time to fill this in yet - if not, a propertychanged event will fire it again
                {
                    if (backdropImage.IsCorrupt)
                    {
                        baseItem.BackdropImagePaths = null;
                        return null;
                    }
                    return backdropImage.Image;
                }
                else
                {
                    return null;
                }
            }
        }

        AsyncImageLoader primaryBackdropImage = null;
        private void getPrimaryBackdropImage()
        {
            if (primaryBackdropImage == null)
            {
                primaryBackdropImage = backdropImage = new AsyncImageLoader(
                    () => baseItem.PrimaryBackdropImage,
                    null,
                    () => this.FirePropertiesChanged("PrimaryBackdropImage"));
                backdropImage.LowPriority = true;
            }
        }

        private void getFirstBackdropImage()
        {
            backdropImage = new AsyncImageLoader(
                () => baseItem.PrimaryBackdropImage,
                null,
                () => this.FirePropertiesChanged("BackdropImage"));
            backdropImage.LowPriority = true;
        }

        private void getRandomBackdropImage()
        {
            if (Config.Instance.RotateBackdrops && baseItem.BackdropImages.Count > 1)
            {
                //start the rotation so we don't get the first one twice
                GetNextBackDropImage();
            }
            else
            {
                //just a single one required
                if (baseItem.BackdropImages.Count > 0)
                {
                    backdropImageIndex = randomizer.Next(baseItem.BackdropImages.Count);
                    backdropImage = new AsyncImageLoader(
                        () => baseItem.BackdropImages[backdropImageIndex],
                        null,
                        () => this.FirePropertyChanged("BackdropImage"));
                    backdropImage.LowPriority = true;
                }
            }
        }

        public Image PrimaryBackdropImage
        {
            get
            {
                getPrimaryBackdropImage();
                return primaryBackdropImage.Image;
            }
        }

        public string PrimaryBackdropImagePath
        {
            get
            {
                return BaseItem.BackdropImagePath ?? "";
            }
        }

        public List<string> BackdropImagePaths
        {
            get
            {
                return BaseItem.BackdropImagePaths ?? new List<string>();
            }
        }

        List<AsyncImageLoader> backdropImages = null;
        public List<Image> BackdropImages
        {
            get
            {
                if (!HasBackdropImage)
                {
                    if (PhysicalParent != null)
                    {
                        return PhysicalParent.BackdropImages;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (backdropImages == null)
                {
                    EnsureAllBackdropsAreLoaded();
                }

                lock (backdropImages)
                {
                    return backdropImages.Select(asyncLoader => asyncLoader.Image).ToList();
                }
            }
        }

        private void EnsureAllBackdropsAreLoaded()
        {
            if (backdropImages == null)
            {
                backdropImages = new List<AsyncImageLoader>();

                Async.Queue("Backdrop Loader", () =>
                {
                    foreach (var image in baseItem.BackdropImages)
                    {
                        // this is really subtle, we need to capture the image otherwise they will all be the same
                        var captureImage = image;
                        var asyncImage = new AsyncImageLoader(
                             () => captureImage,
                             null,
                             () => this.FirePropertiesChanged("BackdropImages", "BackdropImage"));

                        lock (backdropImages)
                        {
                            backdropImages.Add(asyncImage);
                            // trigger a load
                            var ignore = asyncImage.Image;
                        }
                    }
                });
            }
        }

        int backdropImageIndex = 0;
        Random randomizer = new Random();
        public void GetNextBackDropImage()
        {
            if (!Config.Instance.RotateBackdrops) return; // only do this if we want to rotate

            EnsureAllBackdropsAreLoaded();
            var images = new List<AsyncImageLoader>();
            lock (backdropImages)
            {
                images.AddRange(backdropImages);
            }

            if (images != null && images.Count > 1)
            {
                if (Config.Instance.RandomizeBackdrops)
                {
                    int lastOne = backdropImageIndex;
                    while (backdropImageIndex == lastOne)
                    {
                        backdropImageIndex = randomizer.Next(images.Count);
                    }
                }
                else
                {

                    backdropImageIndex++;
                    backdropImageIndex = backdropImageIndex % images.Count;
                }
                if (images[backdropImageIndex].Image != null)
                {
                    backdropImage = images[backdropImageIndex];
                    FirePropertyChanged("BackdropImage");
                }
            }
        }

        AsyncImageLoader primaryImage = null;
        public Image PrimaryImage
        {
            get
            {
                if (baseItem.PrimaryImagePath == null)
                {
                    return DefaultImage;
                }
                EnsurePrimaryImageIsSet();
                return primaryImage.Image;
            }
        }

        private void EnsurePrimaryImageIsSet()
        {
            if (primaryImage == null)
            {
                primaryImage = new AsyncImageLoader(
                    () => baseItem.PrimaryImage,
                    DefaultImage,
                    PrimaryImageChanged);
                var ignore = primaryImage.Image;
            }
        }

        void PrimaryImageChanged()
        {
            FirePropertiesChanged("PrimaryImage", "PreferredImage", "PrimaryImageSmall", "PreferredImageSmall");
        }

        AsyncImageLoader secondaryImage = null;
        public Image SecondaryImage
        {
            get
            {
                if (baseItem.SecondaryImagePath == null)
                {
                    return PrimaryImage;
                }
                EnsureSecondaryImageIsSet();
                return secondaryImage.Image;
            }
        }

        private void EnsureSecondaryImageIsSet()
        {
            if (secondaryImage == null)
            {
                secondaryImage = new AsyncImageLoader(
                    () => baseItem.SecondaryImage,
                    DefaultImage,
                    SecondaryImageChanged);
                var ignore = secondaryImage.Image;
            }
        }

        void SecondaryImageChanged()
        {
            FirePropertiesChanged("SecondaryImage");
        }

        AsyncImageLoader primaryImageSmall = null;
        // these all come in from the ui thread so no sync is required. 
        public Image PrimaryImageSmall
        {
            get
            {

                if (baseItem.PrimaryImagePath != null)
                {
                    EnsurePrimaryImageIsSet();

                    if (primaryImage.IsLoaded &&
                        preferredImageSmallSize != null &&
                        (preferredImageSmallSize.Width > 0 ||
                        preferredImageSmallSize.Height > 0))
                    {

                        if (primaryImageSmall == null)
                        {
                            LoadSmallPrimaryImage();
                        }
                    }
                    else
                    {
                        //Logger.ReportWarning("Primary image small size not set: " + Name);
                    }
                    return primaryImageSmall != null ? primaryImageSmall.Image : PrimaryImage;
                }
                else
                {
                    return DefaultImage;
                }


            }
        }

        private void LoadSmallPrimaryImage()
        {
            float aspect = primaryImage.Size.Height / (float)primaryImage.Size.Width;
            float constraintAspect = aspect;

            if (preferredImageSmallSize.Height > 0 && preferredImageSmallSize.Width > 0)
            {
                constraintAspect = preferredImageSmallSize.Height / (float)preferredImageSmallSize.Width;
            }

            primaryImageSmall = new AsyncImageLoader(
                () => baseItem.PrimaryImage,
                DefaultImage,
                PrimaryImageChanged);

            if (aspect == constraintAspect)
            {
                smallImageIsDistorted = false;
            }
            else
            {
                smallImageIsDistorted = Math.Abs(aspect - constraintAspect) < Config.Instance.MaximumAspectRatioDistortion;
            }

            if (smallImageIsDistorted)
            {
                primaryImageSmall.Size = preferredImageSmallSize;
            }
            else
            {

                int width = preferredImageSmallSize.Width;
                int height = preferredImageSmallSize.Height;

                if (aspect > constraintAspect || width <= 0)
                {
                    width = (int)((float)height / aspect);
                }
                else
                {
                    height = (int)((float)width * aspect);
                }

                primaryImageSmall.Size = new Size(width, height);
            }

            FirePropertyChanged("SmallImageIsDistorted");
        }

        bool smallImageIsDistorted = false;
        public bool SmallImageIsDistorted
        {
            get
            {
                return smallImageIsDistorted;
            }
        }

        public Image PreferredImage
        {
            get
            {
                return preferBanner ? BannerImage ?? PrimaryImage : PrimaryImage;
            }
        }


        public Image PreferredImageSmall
        {
            get
            {
                return preferBanner ? BannerImage ?? PrimaryImageSmall : PrimaryImageSmall;
            }
        }

        Microsoft.MediaCenter.UI.Size preferredImageSmallSize;
        public Microsoft.MediaCenter.UI.Size PreferredImageSmallSize
        {
            get
            {
                return preferredImageSmallSize;
            }
            set
            {
                if (value != preferredImageSmallSize)
                {
                    preferredImageSmallSize = value;
                    primaryImageSmall = null;
                    FirePropertyChanged("PreferredImageSmall");
                    FirePropertyChanged("PrimaryImageSmall");
                }
            }
        }

        AsyncImageLoader logoImage = null;
        public Image LogoImage
        {
            get
            {
                if (!HasLogoImage)
                {
                    return null;
                }
                EnsureLogoImageIsSet();
                return logoImage.Image;
            }
        }

        private void EnsureLogoImageIsSet()
        {
            if (logoImage == null)
            {
                logoImage = new AsyncImageLoader(
                    () => baseItem.LogoImage,
                    DefaultImage,
                    LogoImageChanged);
                var ignore = logoImage.Image;
            }
        }

        void LogoImageChanged()
        {
            FirePropertyChanged("LogoImage");
        }

        public bool HasLogoImage
        {
            get
            {
                return baseItem.LogoImagePath != null || (PhysicalParent != null ? PhysicalParent.HasLogoImage : false);
            }
        }


        AsyncImageLoader artImage = null;
        public Image ArtImage
        {
            get
            {
                if (!HasArtImage)
                {
                    return null;
                }
                EnsureArtImageIsSet();
                return artImage.Image;
            }
        }

        private void EnsureArtImageIsSet()
        {
            if (artImage == null)
            {
                artImage = new AsyncImageLoader(
                    () => baseItem.ArtImage,
                    DefaultImage,
                    ArtImageChanged);
                var ignore = artImage.Image;
            }
        }

        void ArtImageChanged()
        {
            FirePropertyChanged("ArtImage");
        }

        public bool HasArtImage
        {
            get
            {
                return baseItem.ArtImagePath != null || (PhysicalParent != null && PhysicalParent.HasArtImage);
            }
        }

        AsyncImageLoader thumbnailImage = null;
        public Image ThumbnailImage
        {
            get
            {
                if (!HasThumbnailImage)
                {
                    return null;
                }
                EnsureThumbnailImageIsSet();
                return thumbnailImage.Image;
            }
        }

        private void EnsureThumbnailImageIsSet()
        {
            if (thumbnailImage == null)
            {
                thumbnailImage = new AsyncImageLoader(
                    () => baseItem.ThumbnailImage,
                    DefaultImage,
                    ThumbnailImageChanged);
                var ignore = thumbnailImage.Image;
            }
        }

        void ThumbnailImageChanged()
        {
            FirePropertyChanged("ThumbnailImage");
        }

        public bool HasThumbnailImage
        {
            get
            {
                return baseItem.ThumbnailImagePath != null || (PhysicalParent != null && PhysicalParent != TopParent && PhysicalParent.HasThumbnailImage);
            }
        }

        AsyncImageLoader discImage = null;
        public Image DiscImage
        {
            get
            {
                if (!HasDiscImage)
                {
                    return null;
                }
                EnsureDiscImageIsSet();
                return discImage.Image;
            }
        }

        private void EnsureDiscImageIsSet()
        {
            if (discImage == null)
            {
                discImage = new AsyncImageLoader(
                    () => baseItem.DiscImage,
                    DefaultImage,
                    DiscImageChanged);
                var ignore = discImage.Image;
            }
        }

        void DiscImageChanged()
        {
            FirePropertyChanged("DiscImage");
        }

        public bool HasDiscImage
        {
            get
            {
                return baseItem.DiscImagePath != null;
            }
        }

        public bool HasPrimaryImage
        {
            get { return baseItem.PrimaryImagePath != null; }
        }

        public bool HasSecondaryImage
        {
            get { return baseItem.SecondaryImagePath != null; }
        }

        public bool HasPreferredImage
        {
            get { return (HasPrimaryImage); }
        }

        bool preferBanner;
        public bool PreferBanner
        {
            get
            {
                return preferBanner;
            }
            set
            {
                preferBanner = value;
                FirePropertyChanged("HasPreferredImage");
                FirePropertyChanged("PreferredImage");
            }
        }


        internal float PrimaryImageAspect
        {
            get
            {
                return GetAspectRatio(baseItem.PrimaryImagePath);
            }
        }

        internal float BannerImageAspect
        {
            get
            {
                return GetAspectRatio(baseItem.BannerImagePath);
            }
        }

        float GetAspectRatio(string path)
        {

            float aspect = 0;
            if (path != null)
            {
                LibraryImage image;
                if (BaseItem is Media)
                {
                    image = LibraryImageFactory.Instance.GetImage(path, true, BaseItem);
                }
                else
                {
                    image = LibraryImageFactory.Instance.GetImage(path);
                }
                aspect = ((float)image.Height) / (float)image.Width;
            }
            return aspect;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);


        public void SetPrimarySmallToTiny()
        {
            var windowSize = GetWindowSize(new Size(1280, 720));
            this.preferredImageSmallSize = new Size(-1, windowSize.Height / 8);
        }


        // I can not figure out any way to pass the size of an element to the code
        // so I cheat 
        public void SetPreferredImageSmallToEstimatedScreenSize()
        {

            var folder = this as FolderModel;
            if (folder == null) return;

            Size size = GetWindowSize(new Size(1280, 720));

            size.Width = -1;
            size.Height = size.Height / 3;

            foreach (var item in folder.Children)
            {
                item.PreferredImageSmallSize = size;
            }

        }

        private static Size GetWindowSize(Size size)
        {

            try
            {

                // find ehshell 
                var ehshell = Process.GetProcessesByName("ehshell").First().MainWindowHandle;

                if (ehshell != IntPtr.Zero)
                {

                    RECT windowSize;
                    GetWindowRect(ehshell, out windowSize);

                    size = new Size(
                        (windowSize.Right - windowSize.Left),
                        (windowSize.Bottom - windowSize.Top)
                        );

                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Failed to gather size information, made a guess ", e);
            }
            return size;
        }

        public Image RottenTomatoImage
        {
            get { return HasCriticRating ? CriticRating >= 60 ? RtFreshImage : RtRottenImage : null; }
        }

        static readonly Image DefaultVideoImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/DefaultVideo");
        static readonly Image DefaultActorImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/MissingPerson");
        static readonly Image DefaultStudioImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/BlankGraphic");
        static readonly Image DefaultFolderImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/folder");
        static readonly Image DefaultUserImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/UserLoginDefault");
        static readonly Image DefaultAlbumImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/DefaultAlbum");
        static readonly Image DefaultSongImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/DefaultSong");
        static readonly Image DefaultChapterImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/Frames");
        static readonly Image RtFreshImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/RTFresh");
        static readonly Image RtRottenImage = new Image("resx://MediaBrowser/MediaBrowser.Resources/RTRotten");
 
        public Image DefaultImage
        {
            get
            {
                Image image = DefaultFolderImage;

                if (baseItem is Video)
                {
                    image = DefaultVideoImage;
                }
                else if (baseItem is Song)
                {
                    image = DefaultSongImage;
                }
                else if (baseItem is Person)
                {
                    image = DefaultActorImage;
                }
                else if (baseItem is Studio)
                {
                    image = DefaultStudioImage;
                }
                else if (baseItem is User)
                {
                    image = DefaultUserImage;
                }
                else if (baseItem is MusicArtist)
                {
                    image = DefaultActorImage;
                }
                else if (baseItem is MusicAlbum)
                {
                    image = DefaultAlbumImage;
                }
                else if (baseItem is Chapter)
                {
                    image = DefaultChapterImage;
                }

                return image;
            }
        }

    }
}
