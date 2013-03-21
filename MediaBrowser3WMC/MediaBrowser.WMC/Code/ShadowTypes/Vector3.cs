using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Code.ShadowTypes {
    public class Vector3 {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Microsoft.MediaCenter.UI.Vector3 ToMediaCenterVector3() {
            return new Microsoft.MediaCenter.UI.Vector3(X, Y,Z);
        }

        public static Vector3 FromMediaCenterVector3(Microsoft.MediaCenter.UI.Vector3 vector) {
            return new Vector3() { X = vector.X, Y = vector.Y, Z=vector.Z };
        }
    }
}
