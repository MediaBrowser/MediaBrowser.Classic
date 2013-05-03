using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Model.Entities;
using Microsoft.MediaCenter.UI;
using System.IO;
using System.Collections;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Extensions;

namespace MediaBrowser.Library
{


    public class DisplayPreferences : BaseModelItem
    {
        private static readonly byte Version = 3;

        readonly Choice viewType = new Choice();
        readonly BooleanChoice showLabels;
        readonly BooleanChoice verticalScroll;
        readonly Choice sortOrders = new Choice();
        readonly Choice indexBy = new Choice();
        readonly BooleanChoice useBanner;
        readonly BooleanChoice useCoverflow;
        readonly BooleanChoice useBackdrop;
        private bool saveEnabled = true;
        SizeRef thumbConstraint = new SizeRef(Config.Instance.DefaultPosterSize);
        private Dictionary<string, IComparer<BaseItem>> sortDict;
        private Dictionary<string, string> indexDict;
        private Dictionary<string, string> customParms;

        public Guid Id { get; set; }
        protected Folder Folder { get; set; }

        public DisplayPreferences(string id, Folder folder)
        {
            this.Id = new Guid(id);
            Folder = folder;

            ArrayList list = new ArrayList();
            foreach (ViewType v in Enum.GetValues(typeof(ViewType)))
                list.Add(ViewTypeNames.GetName(v));
            viewType.Options = list;

            try
            {
                this.viewType.Chosen = folder.DisplayPreferences != null ? folder.DisplayPreferences.ViewType ?? "Poster" : "Poster";
            }
            catch (ArgumentException)
            {
                Logging.Logger.ReportError("Invalid view type {0} stored for {1}.  Setting to Poster.",folder.DisplayPreferences.ViewType, folder.Name);
                viewType.Chosen = "Poster";
            }

            //set our dynamic choice options
            this.sortDict = folder.SortOrderOptions;
            this.sortOrders.Options = sortDict.Keys.ToArray();
            this.indexDict = folder.IndexByOptions;
            this.indexBy.Options = folder.IndexByOptions.Keys.ToArray();
 
            verticalScroll = new BooleanChoice {Value = folder.DisplayPreferences != null && folder.DisplayPreferences.ScrollDirection == ScrollDirection.Vertical};

            useBanner = new BooleanChoice();

            showLabels = new BooleanChoice();

            useCoverflow = new BooleanChoice {Value = false};

            useBackdrop = new BooleanChoice {Value = Config.Instance.ShowBackdrop};

            if (folder.DisplayPreferences != null)
            {
                var width = folder.DisplayPreferences.PrimaryImageWidth > 0 ? folder.DisplayPreferences.PrimaryImageWidth : Config.Instance.DefaultPosterSize.Width;
                var height = folder.DisplayPreferences.PrimaryImageHeight > 0 ? folder.DisplayPreferences.PrimaryImageHeight : Config.Instance.DefaultPosterSize.Height;
            
                customParms = folder.DisplayPreferences.CustomPrefs ?? new Dictionary<string, string>();
                thumbConstraint = new SizeRef(new Size(width, height));
                useBanner.Value = (customParms.GetValueOrDefault("MBCUseBanner", "false") == "true");
                showLabels.Value = (customParms.GetValueOrDefault("MBCShowLabels", "false") == "true");
            }

            try
            {
                sortOrders.Chosen = folder.DisplayPreferences != null ? folder.DisplayPreferences.SortBy ?? "Name" : "Name";
            }
            catch (ArgumentException)
            {
                Logging.Logger.ReportError("Invalid sort by stored for {1}.  Setting to Name.", folder.Name);
                sortOrders.Chosen = "Name";
            }

            ListenForChanges();
        }

        public void ListenForChanges()
        {
            sortOrders.ChosenChanged += new EventHandler(sortOrders_ChosenChanged);
            indexBy.ChosenChanged += new EventHandler(indexBy_ChosenChanged);
            viewType.ChosenChanged += new EventHandler(viewType_ChosenChanged);
            showLabels.ChosenChanged += new EventHandler(showLabels_ChosenChanged);
            verticalScroll.ChosenChanged += new EventHandler(verticalScroll_ChosenChanged);
            useBanner.ChosenChanged += new EventHandler(useBanner_ChosenChanged);
            useCoverflow.ChosenChanged += new EventHandler(useCoverflow_ChosenChanged);
            useBackdrop.ChosenChanged += new EventHandler(useBackdrop_ChosenChanged);
            thumbConstraint.PropertyChanged += new PropertyChangedEventHandler(thumbConstraint_PropertyChanged);
        }


        public void StopListeningForChanges()
        {
            sortOrders.ChosenChanged -= new EventHandler(sortOrders_ChosenChanged);
            indexBy.ChosenChanged -= new EventHandler(indexBy_ChosenChanged);
            viewType.ChosenChanged -= new EventHandler(viewType_ChosenChanged);
            showLabels.ChosenChanged -= new EventHandler(showLabels_ChosenChanged);
            verticalScroll.ChosenChanged -= new EventHandler(verticalScroll_ChosenChanged);
            useBanner.ChosenChanged -= new EventHandler(useBanner_ChosenChanged);
            useCoverflow.ChosenChanged -= new EventHandler(useCoverflow_ChosenChanged);
            useBackdrop.ChosenChanged -= new EventHandler(useBackdrop_ChosenChanged);
            thumbConstraint.PropertyChanged -= new PropertyChangedEventHandler(thumbConstraint_PropertyChanged);
        }

        void useCoverflow_ChosenChanged(object sender, EventArgs e)
        {
            Save();
        }

        void useBanner_ChosenChanged(object sender, EventArgs e)
        {
            Save();
        }

        void indexBy_ChosenChanged(object sender, EventArgs e)
        {
            FirePropertyChanged("IndexBy");
            Save();
        }

        void thumbConstraint_PropertyChanged(IPropertyObject sender, string property)
        {
            Save();
        }

        void showLabels_ChosenChanged(object sender, EventArgs e)
        {
            Save();
        }

        void verticalScroll_ChosenChanged(object sender, EventArgs e)
        {
            Save();
        }

        void viewType_ChosenChanged(object sender, EventArgs e)
        {
            FirePropertyChanged("ViewTypeString");
            Save();
        }

        void sortOrders_ChosenChanged(object sender, EventArgs e)
        {
            FirePropertyChanged("SortOrder");
            Save();
        }

        void useBackdrop_ChosenChanged(object sender, EventArgs e)
        {
            Save();
        }



        public void WriteToStream(BinaryWriter bw)
        {
            bw.Write(Version);
            bw.SafeWriteString(ViewTypeNames.GetEnum((string)this.viewType.Chosen).ToString());
            bw.Write(this.showLabels.Value);
            bw.Write(this.verticalScroll.Value);
            bw.SafeWriteString((string)this.SortOrder.ToString());
            bw.SafeWriteString((string)this.IndexByString);
            bw.Write(this.useBanner.Value);
            bw.Write(this.thumbConstraint.Value.Width);
            bw.Write(this.thumbConstraint.Value.Height);
            bw.Write(this.useCoverflow.Value);
            bw.Write(this.useBackdrop.Value);
        }

        public DisplayPreferences ReadFromStream(BinaryReader br)
        {
            this.saveEnabled = false;
            byte version = br.ReadByte();
            try
            {
                this.viewType.Chosen = ViewTypeNames.GetName((ViewType)Enum.Parse(typeof(ViewType), br.SafeReadString()));
            }
            catch
            {
                this.viewType.Chosen = ViewTypeNames.GetName(MediaBrowser.Library.ViewType.Poster);
            }
            this.showLabels.Value = br.ReadBoolean();
            this.verticalScroll.Value = br.ReadBoolean();
            try
            {
                this.SortOrder = br.SafeReadString();
            }
            catch { }
            try
            {
                this.IndexBy = br.SafeReadString();
            }
            catch { }
            if (!Config.Instance.RememberIndexing)
                this.IndexBy = Localization.LocalizedStrings.Instance.GetString("NoneDispPref");
            this.useBanner.Value = br.ReadBoolean();
            this.thumbConstraint.Value = new Size(br.ReadInt32(), br.ReadInt32());

            if (version >= 2)
                this.useCoverflow.Value = br.ReadBoolean();

            if (version >= 3)
                this.useBackdrop.Value = br.ReadBoolean();

            this.saveEnabled = true;
            return this;
        }

        public Choice SortOrders
        {
            get { return this.sortOrders; }
        }

        public IComparer<BaseItem> SortFunction
        {
            get
            {
                return sortDict[sortOrders.Chosen.ToString()];
            }
        }

        public string SortOrder
        {
            get { return sortOrders.Chosen.ToString(); }
            set
            {
                this.SortOrders.Chosen = value.ToString();
                this.SortOrders.Default = this.SortOrders.Chosen;
            }
        }

        public string IndexBy
        {
            get { return indexDict[indexBy.Chosen.ToString()]; }
            set
            {
                this.IndexByChoice.Chosen = value.ToString();
                this.IndexByChoice.Default = this.IndexByChoice.Chosen;
            }
        }

        public string IndexByString
        {
            get
            {
                return this.indexBy.Chosen.ToString();
            }
        }

        public Choice IndexByChoice
        {
            get { return this.indexBy; }
        }

        public Choice ViewType
        {
            get { return this.viewType; }
        }

        public string ViewTypeString
        {
            get
            {
                return ViewTypeNames.GetEnum((string)this.viewType.Chosen).ToString();
            }
        }

        public BooleanChoice ShowLabels
        {
            get { return this.showLabels; }
        }

        public BooleanChoice VerticalScroll
        {
            get { return this.verticalScroll; }
        }

        public BooleanChoice UseBanner
        {
            get { return this.useBanner; }
        }

        public BooleanChoice UseCoverflow
        {
            get { return this.useCoverflow; }
        }

        public SizeRef ThumbConstraint
        {
            get
            {
                return this.thumbConstraint;
            }
        }

        public void IncreaseThumbSize()
        {
            Size s = this.ThumbConstraint.Value;
            s.Height += 20;
            s.Width += 20;
            this.ThumbConstraint.Value = s;
        }

        public void DecreaseThumbSize()
        {
            Size s = this.ThumbConstraint.Value;
            s.Height -= 20;
            s.Width -= 20;
            if (s.Height < 60)
                s.Height = 60;
            if (s.Width < 60)
                s.Width = 60;
            this.ThumbConstraint.Value = s;
        }

        public BooleanChoice UseBackdrop
        {
            get { return this.useBackdrop; }
        }

        public Dictionary<string, string> CustomParms
        {
            get
            {
                return customParms;
            }
        }

        public void SetCustomParm(string key, string value)
        {
            customParms[key] = value;
            Save();
            FirePropertyChanged("CustomParms");
        }

        internal void LoadDefaults()
        {

        }

        public void Save()
        {
            if ((!saveEnabled) || (this.Id == Guid.Empty))
                return;
            if (Folder.DisplayPreferences == null) Folder.DisplayPreferences = new Model.Entities.DisplayPreferences();
            Folder.DisplayPreferences.IndexBy = this.indexBy.Chosen.ToString();
            Folder.DisplayPreferences.ViewType = this.viewType.Chosen.ToString();
            Folder.DisplayPreferences.SortBy = this.SortOrder;
            Folder.DisplayPreferences.RememberIndexing = Kernel.Instance.ConfigData.RememberIndexing;
            Folder.DisplayPreferences.PrimaryImageHeight = ThumbConstraint.Value.Height;
            Folder.DisplayPreferences.PrimaryImageWidth = thumbConstraint.Value.Width;
            Folder.DisplayPreferences.ScrollDirection = this.VerticalScroll.Value ? ScrollDirection.Vertical : ScrollDirection.Horizontal;
            this.CustomParms["MBCUseBanner"] = UseBanner.Value ? "true" : "false";
            this.CustomParms["MBCShowLabels"] = ShowLabels.Value ? "true" : "false";
            Folder.DisplayPreferences.CustomPrefs = this.CustomParms;

            Folder.SaveDisplayPrefs(this);
        }

        public void ToggleViewTypes()
        {
            this.ViewType.NextValue(true);
            Save();
            FirePropertyChanged("DisplayPrefs");
        }
    }

    public enum ViewType
    {
        CoverFlow,
        Detail,
        Poster,
        Thumb,
        ThumbStrip
    }

    public class ViewTypeNames
    {
        //private static readonly string[] Names = { "Cover Flow","Detail", "Poster", "Thumb", "Thumb Strip"};
        private static readonly string[] Names = { Kernel.Instance.StringData.GetString("CoverFlowDispPref"), 
                                                   Kernel.Instance.StringData.GetString("DetailDispPref"), 
                                                   Kernel.Instance.StringData.GetString("PosterDispPref"), 
                                                   Kernel.Instance.StringData.GetString("ThumbDispPref"), 
                                                   Kernel.Instance.StringData.GetString("ThumbStripDispPref") };

        public static string GetName(ViewType type)
        {
            return Names[(int)type];
        }

        public static ViewType GetEnum(string name)
        {
            return (ViewType)Array.IndexOf<string>(Names, name);
        }
    }

    public class SortOrderNames
    {
        private static readonly string[] Names = { Kernel.Instance.StringData.GetString("NameDispPref"), 
                                                   Kernel.Instance.StringData.GetString("DateDispPref"), 
                                                   Kernel.Instance.StringData.GetString("RatingDispPref"), 
                                                   Kernel.Instance.StringData.GetString("RuntimeDispPref"), 
                                                   Kernel.Instance.StringData.GetString("UnWatchedDispPref"), 
                                                   Kernel.Instance.StringData.GetString("YearDispPref") };

        public static string GetName(SortOrder order)
        {
            return Names[(int)order];
        }

        public static SortOrder GetEnum(string name)
        {
            return (SortOrder)Array.IndexOf<string>(Names, name);
        }
    }

    public class IndexTypeNames
    {
        private static readonly string[] Names = { Kernel.Instance.StringData.GetString("NoneDispPref"), 
                                                   Kernel.Instance.StringData.GetString("ActorDispPref"), 
                                                   Kernel.Instance.StringData.GetString("GenreDispPref"), 
                                                   Kernel.Instance.StringData.GetString("DirectorDispPref"),
                                                   Kernel.Instance.StringData.GetString("YearDispPref"), 
                                                   Kernel.Instance.StringData.GetString("StudioDispPref") };

        public static string GetName(IndexType order)
        {
            return Names[(int)order];
        }

        public static IndexType GetEnum(string name)
        {
            return (IndexType)Array.IndexOf<string>(Names, name);
        }
    }
}
