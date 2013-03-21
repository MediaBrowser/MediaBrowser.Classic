using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Code.ShadowTypes {
    public class Inset {
        public int Bottom { get; set; }
        public int Left { get; set; }
        public int Right { get; set; }
        public int Top { get; set; }

        public Microsoft.MediaCenter.UI.Inset ToMediaCenterInset() {
            return new Microsoft.MediaCenter.UI.Inset(Left, Top, Right, Bottom);
        }

        public static Inset FromMediaCenterInset(Microsoft.MediaCenter.UI.Inset inset) {
            return new Inset() {Left = inset.Left, Bottom = inset.Bottom, Right = inset.Right, Top = inset.Top };
        }
    }
}
