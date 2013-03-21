using System;
using System.Text;
using System.Drawing;
using System.Reflection;
using Microsoft.MediaCenter.UI;

namespace MediaBrowser
{
    [Flags, MarkupVisible]
    public enum FontStyles
    {
        None, 
        Bold, 
        Italic
    }

    [MarkupVisible]
    public class FancyTextHelper : ModelItem
    {
        public FancyTextHelper() { }

        private bool useDynamic = false;
        private string content = string.Empty;

        public string FontName { get; set; }
        public float FontSize { get; set; }
        private FontStyles fontStyle;
        public object FontStyle
        {
            set
            { 
                this.fontStyle = (FontStyles)value; 
            }
        }
        public string Content 
        {   get { return content;}
            set
            {
                if (content != value)
                {
                    content = value;
                    CalculateDisplay();
                }
            }
        }
        public System.Single MaximumWidth { get; set; }
        public int MaximumWidthInt
        {
            get { return Convert.ToInt32(this.MaximumWidth); }
            set
            {
                this.MaximumWidth = Convert.ToSingle(value) + 50 ;
                //TODO: margins are currently hard coded...;             
            }
        }
        
        public bool UseDynamic
        {
            get 
            { 
                return this.useDynamic; 
            }
            set 
            {
                if (this.useDynamic != value)
                {
                    this.useDynamic = value;
                    FirePropertyChanged("UseDynamic");
                }
            }
        }

        int abc = 0;
        public int ABC
        {
            get
            {
                return this.abc;
            }
            set
            {
                if (this.abc != value)
                {
                    this.abc = value;
                    FirePropertyChanged("ABC");
                }
            }
        }

        public int MeasureDisplayStringWidth(Graphics graphics, string text, Font font)
        {
            text = text.ToUpper();
            System.Drawing.StringFormat format = new StringFormat(StringFormat.GenericTypographic);
            System.Drawing.RectangleF rect = new System.Drawing.RectangleF(0, 0, 1000, 1000);
            System.Drawing.CharacterRange[] ranges = { new System.Drawing.CharacterRange(0, text.Length) }; 
            System.Drawing.Region[] regions = new System.Drawing.Region[1];
            format.SetMeasurableCharacterRanges(ranges); 
            regions = graphics.MeasureCharacterRanges(text, font, rect, format);
            rect = regions[0].GetBounds(graphics);
            
            return (int)(rect.Right + 1.0f);
            
        }

        public void CalculateDisplay()
        {
            Bitmap bmp = new Bitmap(1, 1);
            Graphics graphics = Graphics.FromImage(bmp);
            Font f = new Font(FontName, FontSize);
            if (this.Content.Length > 0)
            {
                int measured = MeasureDisplayStringWidth(graphics, this.Content, f);

                if ((measured > MaximumWidth) && (MaximumWidth > 0))
                {
                    this.UseDynamic = true;
                    ABC = 1;
                }
                else
                {
                    this.useDynamic = false;
                    ABC = 0;
                }
                //Application.CurrentInstance.Information.AddInformationString("useDynamic:" + this.UseDynamic.ToString() + " measured:" + measured + " maximum:" + MaximumWidth.ToString());
            }
            else
            {
                this.useDynamic = false;
                ABC = 0;
            }
        }
        
    }
}
