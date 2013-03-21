using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Code.ShadowTypes {
    public class Size {

        public Size() { }
        public Size(int width, int height) {
            Width = width;
            Height = height;
        }

        public int Height { get; set; }
        public int Width { get; set; }

        public Microsoft.MediaCenter.UI.Size ToMediaCenterSize() {
            return new Microsoft.MediaCenter.UI.Size(Width, Height);
        }

        public static Size FromMediaCenterSize(Microsoft.MediaCenter.UI.Size size) {
            return new Size() { Height = size.Height, Width = size.Width };
        }
    }
}
